using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using ReleaseShip.Data.Models;
using ReleaseShip.Data.Relational;

namespace ReleaseShip.Data.Services
{
    internal sealed class ContainerRegistryService : IContainerRegistryService
    {
        private readonly IDatabaseContext ctx;
        private readonly IContainerRepositoryService repositories;
        private readonly IContainerStorageService storage;
        private readonly IContainerTagPolicyService tagPolicy;

        public ContainerRegistryService(
            IDatabaseContext ctx,
            IContainerRepositoryService repositories,
            IContainerStorageService storage,
            IContainerTagPolicyService tagPolicy)
        {
            this.ctx = ctx;
            this.repositories = repositories;
            this.storage = storage;
            this.tagPolicy = tagPolicy;
        }

        public Task<ContainerRepositoryDetail?> GetRepositoryDetailAsync(string repositoryName, CancellationToken token)
        {
            return this.repositories.GetRepositoryDetailAsync(repositoryName, token);
        }

        public async Task<ContainerManifestReference?> GetManifestAsync(string repositoryName, string reference, CancellationToken token)
        {
            var repository = await RequireRepositoryAsync(repositoryName, token);
            using var conn = await this.ctx.GetConnection(token);

            if (IsDigest(reference))
            {
                var manifest = await conn.QuerySingleOrDefaultAsync<ContainerManifest?>(
                    @"select *
                      from registry_manifests
                      where repository_id = @RepositoryId and digest = @Digest",
                    new { RepositoryId = repository.Repository.Id, Digest = NormalizeDigest(reference) });
                return manifest == null ? null : new ContainerManifestReference() { Manifest = manifest };
            }

            var row = await conn.QuerySingleOrDefaultAsync<ManifestTagRow>(
                @"select m.*, t.name as tag_name
                  from registry_tags t
                  join registry_manifests m on m.id = t.manifest_id
                  where t.repository_id = @RepositoryId and t.name = @TagName",
                new { RepositoryId = repository.Repository.Id, TagName = reference.Trim() });

            return row == null ? null : new ContainerManifestReference()
            {
                Manifest = ToManifest(row),
                TagName = row.TagName,
            };
        }

        public async Task<IReadOnlyList<string>> GetTagsAsync(string repositoryName, CancellationToken token)
        {
            var repository = await RequireRepositoryAsync(repositoryName, token);
            using var conn = await this.ctx.GetConnection(token);
            var tags = await conn.QueryAsync<string>(
                @"select name
                  from registry_tags
                  where repository_id = @RepositoryId
                  order by name",
                new { RepositoryId = repository.Repository.Id });
            return tags.ToList();
        }

        public async Task<bool> BlobExistsAsync(string repositoryName, string digest, CancellationToken token)
        {
            var repository = await RequireRepositoryAsync(repositoryName, token);
            string normalizedDigest = NormalizeDigest(digest);
            using var conn = await this.ctx.GetConnection(token);
            int count = await conn.ExecuteScalarAsync<int>(
                @"select count(1)
                  from registry_blob_links
                  where repository_id = @RepositoryId and digest = @Digest",
                new { RepositoryId = repository.Repository.Id, Digest = normalizedDigest });
            if (count == 0)
            {
                return false;
            }

            return await this.storage.BlobExistsAsync(normalizedDigest, token);
        }

        public async Task<Stream?> OpenBlobReadAsync(string repositoryName, string digest, CancellationToken token)
        {
            if (!await BlobExistsAsync(repositoryName, digest, token))
            {
                return null;
            }

            return await this.storage.OpenBlobReadAsync(NormalizeDigest(digest), token);
        }

        public async Task<ContainerUpload> StartUploadAsync(string repositoryName, CancellationToken token)
        {
            var repository = await RequireRepositoryAsync(repositoryName, token);
            var upload = await this.storage.CreateUploadAsync(repository.Repository.FullName, token);
            using var conn = await this.ctx.GetConnection(token);
            await conn.ExecuteAsync(
                @"insert into registry_uploads (id, repository_id, storage_key, offset_bytes, started_date_utc, expires_date_utc, state, completed_digest)
                  values (@Id, @RepositoryId, @StorageKey, @OffsetBytes, @StartedDateUtc, @ExpiresDateUtc, @State, @CompletedDigest)",
                new
                {
                    upload.Id,
                    RepositoryId = repository.Repository.Id,
                    upload.StorageKey,
                    upload.OffsetBytes,
                    upload.StartedDateUtc,
                    upload.ExpiresDateUtc,
                    State = "open",
                    CompletedDigest = (string?)null,
                });
            upload.RepositoryId = repository.Repository.Id;
            return upload;
        }

        public async Task<ContainerUpload?> GetUploadAsync(string repositoryName, string uploadId, CancellationToken token)
        {
            var repository = await RequireRepositoryAsync(repositoryName, token);
            using var conn = await this.ctx.GetConnection(token);
            var upload = await conn.QuerySingleOrDefaultAsync<ContainerUpload?>(
                @"select *
                  from registry_uploads
                  where id = @Id and repository_id = @RepositoryId",
                new { Id = uploadId, RepositoryId = repository.Repository.Id });
            if (upload == null)
            {
                return null;
            }

            var live = await this.storage.GetUploadAsync(uploadId, token);
            if (live == null)
            {
                return null;
            }

            upload.OffsetBytes = live.OffsetBytes;
            upload.StorageKey = live.StorageKey;
            return upload;
        }

        public async Task<ContainerUpload> AppendUploadAsync(string repositoryName, string uploadId, Stream content, CancellationToken token)
        {
            var upload = await RequireUploadAsync(repositoryName, uploadId, token);
            var updated = await this.storage.AppendUploadAsync(uploadId, content, token);
            using var conn = await this.ctx.GetConnection(token);
            await conn.ExecuteAsync(
                @"update registry_uploads
                  set offset_bytes = @OffsetBytes,
                      expires_date_utc = @ExpiresDateUtc
                  where id = @Id",
                new { updated.OffsetBytes, updated.ExpiresDateUtc, Id = uploadId });
            updated.RepositoryId = upload.RepositoryId;
            return updated;
        }

        public async Task<ContainerBlob> CompleteUploadAsync(string repositoryName, string uploadId, string digest, string? mediaType, CancellationToken token)
        {
            var upload = await RequireUploadAsync(repositoryName, uploadId, token);
            var blob = await this.storage.CompleteUploadAsync(uploadId, digest, mediaType, token);
            using var conn = await this.ctx.GetConnection(token);
            await conn.ExecuteAsync(
                @"insert or ignore into registry_blobs (digest, algorithm, size_bytes, media_type, storage_key, created_date_utc)
                  values (@Digest, @Algorithm, @SizeBytes, @MediaType, @StorageKey, @CreatedDateUtc);
                  insert or ignore into registry_blob_links (repository_id, digest, created_date_utc)
                  values (@RepositoryId, @Digest, @CreatedDateUtc);
                  update registry_uploads
                  set offset_bytes = @OffsetBytes,
                      state = 'completed',
                      completed_digest = @Digest
                  where id = @UploadId;",
                new
                {
                    blob.Digest,
                    blob.Algorithm,
                    blob.SizeBytes,
                    blob.MediaType,
                    blob.StorageKey,
                    blob.CreatedDateUtc,
                    RepositoryId = upload.RepositoryId,
                    OffsetBytes = blob.SizeBytes,
                    UploadId = uploadId,
                });
            return blob;
        }

        public async Task<ContainerManifestReference> PutManifestAsync(ContainerManifestPutRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            var repository = await RequireRepositoryAsync(request.RepositoryName, token);

            string digest = ComputeDigest(request.ContentBytes);
            string reference = request.Reference.Trim();
            if (IsDigest(reference))
            {
                string normalizedReference = NormalizeDigest(reference);
                if (!string.Equals(normalizedReference, digest, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Manifest digest mismatch. Expected '{normalizedReference}' but computed '{digest}'.");
                }
            }

            using var conn = await this.ctx.GetConnection(token);
            conn.Open();
            using var tx = conn.BeginTransaction();

            int manifestId = await EnsureManifestAsync(conn, tx, repository.Repository.Id, digest, request, token);
            await EnsureManifestBlobLinksAsync(conn, tx, manifestId, repository.Repository.Id, request.ReferencedBlobDigests, request.ConfigDigest, token);

            string? tagName = null;
            if (!IsDigest(reference))
            {
                tagName = reference;
                int? existingManifestId = await conn.ExecuteScalarAsync<int?>(
                    @"select manifest_id
                      from registry_tags
                      where repository_id = @RepositoryId and name = @TagName",
                    new { RepositoryId = repository.Repository.Id, TagName = tagName }, tx);

                if (existingManifestId.HasValue && existingManifestId.Value != manifestId)
                {
                    bool canUpdate = await this.tagPolicy.CanUpdateTagAsync(repository, tagName, token);
                    if (!canUpdate)
                    {
                        throw new InvalidOperationException($"Tag '{tagName}' is immutable for repository '{repository.Repository.FullName}'.");
                    }
                }

                long nowUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await conn.ExecuteAsync(
                    @"insert into registry_tags (repository_id, name, manifest_id, created_date_utc, updated_date_utc)
                      values (@RepositoryId, @TagName, @ManifestId, @CreatedDateUtc, @UpdatedDateUtc)
                      on conflict(repository_id, name) do update set
                        manifest_id = excluded.manifest_id,
                        updated_date_utc = excluded.updated_date_utc;",
                    new
                    {
                        RepositoryId = repository.Repository.Id,
                        TagName = tagName,
                        ManifestId = manifestId,
                        CreatedDateUtc = nowUtc,
                        UpdatedDateUtc = nowUtc,
                    }, tx);
            }

            tx.Commit();

            return (await GetManifestAsync(repository.Repository.FullName, tagName ?? digest, token))!;
        }

        public async Task DeleteUploadAsync(string repositoryName, string uploadId, CancellationToken token)
        {
            var upload = await RequireUploadAsync(repositoryName, uploadId, token);
            await this.storage.DeleteUploadAsync(upload.Id, token);
            using var conn = await this.ctx.GetConnection(token);
            await conn.ExecuteAsync(
                @"delete from registry_uploads
                  where id = @Id and repository_id = @RepositoryId",
                new { Id = upload.Id, RepositoryId = upload.RepositoryId });
        }

        public async Task DeleteManifestAsync(string repositoryName, string digest, CancellationToken token)
        {
            var repository = await RequireRepositoryAsync(repositoryName, token);
            using var conn = await this.ctx.GetConnection(token);
            await conn.ExecuteAsync(
                @"delete from registry_manifests
                  where repository_id = @RepositoryId and digest = @Digest",
                new { RepositoryId = repository.Repository.Id, Digest = NormalizeDigest(digest) });
        }

        public async Task DeleteTagAsync(string repositoryName, string tagName, CancellationToken token)
        {
            var repository = await RequireRepositoryAsync(repositoryName, token);
            using var conn = await this.ctx.GetConnection(token);
            await conn.ExecuteAsync(
                @"delete from registry_tags
                  where repository_id = @RepositoryId and name = @TagName",
                new { RepositoryId = repository.Repository.Id, TagName = tagName.Trim() });
        }

        private async Task<ContainerRepositoryDetail> RequireRepositoryAsync(string repositoryName, CancellationToken token)
        {
            var repository = await this.repositories.GetRepositoryDetailAsync(repositoryName, token);
            if (repository == null)
            {
                throw new InvalidOperationException($"Repository '{repositoryName}' was not found.");
            }

            return repository;
        }

        private async Task<ContainerUpload> RequireUploadAsync(string repositoryName, string uploadId, CancellationToken token)
        {
            var upload = await GetUploadAsync(repositoryName, uploadId, token);
            if (upload == null)
            {
                throw new InvalidOperationException($"Upload '{uploadId}' was not found.");
            }

            return upload;
        }

        private static async Task<int> EnsureManifestAsync(IDbConnection conn, IDbTransaction tx, int repositoryId, string digest, ContainerManifestPutRequest request, CancellationToken token)
        {
            int? manifestId = await conn.ExecuteScalarAsync<int?>(
                @"select id
                  from registry_manifests
                  where repository_id = @RepositoryId and digest = @Digest",
                new { RepositoryId = repositoryId, Digest = digest }, tx);
            if (manifestId.HasValue)
            {
                return manifestId.Value;
            }

            return await conn.ExecuteScalarAsync<int>(
                @"insert into registry_manifests (repository_id, digest, media_type, schema_version, artifact_type, subject_digest, config_digest, content_bytes, created_date_utc)
                  values (@RepositoryId, @Digest, @MediaType, @SchemaVersion, @ArtifactType, @SubjectDigest, @ConfigDigest, @ContentBytes, @CreatedDateUtc);
                  select last_insert_rowid();",
                new
                {
                    RepositoryId = repositoryId,
                    Digest = digest,
                    request.MediaType,
                    request.SchemaVersion,
                    request.ArtifactType,
                    request.SubjectDigest,
                    request.ConfigDigest,
                    request.ContentBytes,
                    CreatedDateUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                }, tx);
        }

        private static async Task EnsureManifestBlobLinksAsync(IDbConnection conn, IDbTransaction tx, int manifestId, int repositoryId, IReadOnlyList<string> digests, string? configDigest, CancellationToken token)
        {
            await conn.ExecuteAsync(
                @"delete from registry_manifest_blobs
                  where manifest_id = @ManifestId",
                new { ManifestId = manifestId }, tx);

            int order = 0;
            foreach (string digest in digests.Select(NormalizeDigest).Distinct(StringComparer.Ordinal))
            {
                string kind = string.Equals(digest, NormalizeNullableDigest(configDigest), StringComparison.Ordinal)
                    ? "config"
                    : "layer";
                await conn.ExecuteAsync(
                    @"insert or ignore into registry_manifest_blobs (manifest_id, digest, kind, sort_order)
                      values (@ManifestId, @Digest, @Kind, @SortOrder);
                      insert or ignore into registry_blob_links (repository_id, digest, created_date_utc)
                      values (@RepositoryId, @Digest, @CreatedDateUtc);",
                    new
                    {
                        ManifestId = manifestId,
                        Digest = digest,
                        Kind = kind,
                        SortOrder = order++,
                        RepositoryId = repositoryId,
                        CreatedDateUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    }, tx);
            }
        }

        private static string ComputeDigest(byte[] contentBytes)
        {
            byte[] hash = SHA256.HashData(contentBytes);
            return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
        }

        private static bool IsDigest(string reference)
        {
            return reference.Contains(':', StringComparison.Ordinal);
        }

        private static string NormalizeDigest(string digest)
        {
            if (string.IsNullOrWhiteSpace(digest))
            {
                throw new ArgumentException("Digest is required.", nameof(digest));
            }

            string normalized = digest.Trim().ToLowerInvariant();
            if (!normalized.StartsWith("sha256:", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Digest '{digest}' is invalid.", nameof(digest));
            }

            return normalized;
        }

        private static string? NormalizeNullableDigest(string? digest)
        {
            return string.IsNullOrWhiteSpace(digest) ? null : NormalizeDigest(digest);
        }

        private static ContainerManifest ToManifest(ManifestTagRow row)
        {
            return new ContainerManifest()
            {
                Id = row.Id,
                RepositoryId = row.RepositoryId,
                Digest = row.Digest,
                MediaType = row.MediaType,
                SchemaVersion = row.SchemaVersion,
                ArtifactType = row.ArtifactType,
                SubjectDigest = row.SubjectDigest,
                ConfigDigest = row.ConfigDigest,
                ContentBytes = row.ContentBytes,
                CreatedDateUtc = row.CreatedDateUtc,
            };
        }

        private sealed class ManifestTagRow : ContainerManifest
        {
            public string? TagName { get; set; }
        }
    }
}
