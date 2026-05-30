using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ReleaseShip.Data.Models;

namespace ReleaseShip.Data.Services
{
    internal sealed class FileContainerStorageService : IContainerStorageService
    {
        private readonly IConfiguration configuration;

        private string RegistryRootDirectory => this.configuration.GetValue<string>("Storage:Containers:RootDirectory")
            ?? Path.Combine(this.configuration.GetValue<string>("RootDirectory") ?? Path.GetTempPath(), "registry");

        public FileContainerStorageService(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public Task<bool> BlobExistsAsync(string digest, CancellationToken token)
        {
            string path = GetBlobPath(digest);
            return Task.FromResult(File.Exists(path));
        }

        public Task<ContainerUpload> CreateUploadAsync(string repositoryName, CancellationToken token)
        {
            _ = NormalizeRepositoryName(repositoryName);

            string uploadId = Guid.NewGuid().ToString("N");
            string uploadPath = GetUploadPath(uploadId);
            Directory.CreateDirectory(Path.GetDirectoryName(uploadPath)!);

            using (File.Create(uploadPath))
            {
            }

            return Task.FromResult(CreateUploadDescriptor(uploadId, uploadPath));
        }

        public Task DeleteBlobAsync(string digest, CancellationToken token)
        {
            string path = GetBlobPath(digest);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.CompletedTask;
        }

        public Task DeleteUploadAsync(string uploadId, CancellationToken token)
        {
            string uploadPath = GetUploadPath(uploadId);
            if (File.Exists(uploadPath))
            {
                File.Delete(uploadPath);
            }

            return Task.CompletedTask;
        }

        public Task<ContainerUpload?> GetUploadAsync(string uploadId, CancellationToken token)
        {
            string uploadPath = GetUploadPath(uploadId);
            if (!File.Exists(uploadPath))
            {
                return Task.FromResult<ContainerUpload?>(null);
            }

            return Task.FromResult<ContainerUpload?>(CreateUploadDescriptor(uploadId, uploadPath));
        }

        public Task<Stream?> OpenBlobReadAsync(string digest, CancellationToken token)
        {
            string path = GetBlobPath(digest);
            if (!File.Exists(path))
            {
                return Task.FromResult<Stream?>(null);
            }

            Stream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult<Stream?>(stream);
        }

        public async Task<ContainerUpload> AppendUploadAsync(string uploadId, Stream content, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(content);

            string uploadPath = GetUploadPath(uploadId);
            if (!File.Exists(uploadPath))
            {
                throw new FileNotFoundException("Upload was not found.", uploadId);
            }

            await using (var stream = File.Open(uploadPath, FileMode.Append, FileAccess.Write, FileShare.None))
            {
                await content.CopyToAsync(stream, token);
            }

            return CreateUploadDescriptor(uploadId, uploadPath);
        }

        public async Task<ContainerBlob> CompleteUploadAsync(string uploadId, string digest, string? mediaType, CancellationToken token)
        {
            string uploadPath = GetUploadPath(uploadId);
            if (!File.Exists(uploadPath))
            {
                throw new FileNotFoundException("Upload was not found.", uploadId);
            }

            var normalizedDigest = NormalizeDigest(digest);
            string blobPath = GetBlobPath(normalizedDigest);
            Directory.CreateDirectory(Path.GetDirectoryName(blobPath)!);

            string computedDigest = await ComputeDigestAsync(uploadPath, token);
            if (!string.Equals(computedDigest, normalizedDigest, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Uploaded content digest mismatch. Expected '{normalizedDigest}' but found '{computedDigest}'.");
            }

            if (!File.Exists(blobPath))
            {
                File.Move(uploadPath, blobPath);
            }
            else
            {
                File.Delete(uploadPath);
            }

            var fileInfo = new FileInfo(blobPath);
            return new ContainerBlob()
            {
                Digest = normalizedDigest,
                Algorithm = GetAlgorithm(normalizedDigest),
                SizeBytes = fileInfo.Length,
                MediaType = mediaType,
                StorageKey = blobPath,
                CreatedDateUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
        }

        private static ContainerUpload CreateUploadDescriptor(string uploadId, string uploadPath)
        {
            var info = new FileInfo(uploadPath);
            return new ContainerUpload()
            {
                Id = uploadId,
                StorageKey = uploadPath,
                OffsetBytes = info.Exists ? info.Length : 0,
                StartedDateUtc = new DateTimeOffset(info.Exists ? info.CreationTimeUtc : DateTime.UtcNow).ToUnixTimeSeconds(),
                ExpiresDateUtc = new DateTimeOffset((info.Exists ? info.CreationTimeUtc : DateTime.UtcNow).AddDays(1)).ToUnixTimeSeconds(),
                State = "open",
            };
        }

        private static async Task<string> ComputeDigestAsync(string uploadPath, CancellationToken token)
        {
            await using var stream = File.Open(uploadPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(stream, token);
            return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
        }

        private string GetBlobPath(string digest)
        {
            var normalizedDigest = NormalizeDigest(digest);
            string algorithm = GetAlgorithm(normalizedDigest);
            string hash = GetHash(normalizedDigest);
            string prefix = hash.Substring(0, 2);
            return Path.Combine(this.RegistryRootDirectory, "blobs", algorithm, prefix, hash);
        }

        private string GetUploadPath(string uploadId)
        {
            if (string.IsNullOrWhiteSpace(uploadId))
            {
                throw new ArgumentException("Upload id is required.", nameof(uploadId));
            }

            string normalized = uploadId.Trim().ToLowerInvariant();
            return Path.Combine(this.RegistryRootDirectory, "uploads", $"{normalized}.bin");
        }

        private static string NormalizeDigest(string digest)
        {
            if (string.IsNullOrWhiteSpace(digest))
            {
                throw new ArgumentException("Digest is required.", nameof(digest));
            }

            string normalized = digest.Trim().ToLowerInvariant();
            int separator = normalized.IndexOf(':');
            if (separator <= 0 || separator == normalized.Length - 1)
            {
                throw new ArgumentException($"Digest '{digest}' is not in the expected algorithm:hash format.", nameof(digest));
            }

            string algorithm = normalized[..separator];
            if (!string.Equals(algorithm, "sha256", StringComparison.Ordinal))
            {
                throw new NotSupportedException($"Digest algorithm '{algorithm}' is not supported.");
            }

            _ = GetHash(normalized);
            return normalized;
        }

        private static string NormalizeRepositoryName(string repositoryName)
        {
            if (string.IsNullOrWhiteSpace(repositoryName))
            {
                throw new ArgumentException("Repository name is required.", nameof(repositoryName));
            }

            return repositoryName.Trim().Trim('/').ToLowerInvariant();
        }

        private static string GetAlgorithm(string digest)
        {
            int separator = digest.IndexOf(':');
            return digest[..separator];
        }

        private static string GetHash(string digest)
        {
            int separator = digest.IndexOf(':');
            string hash = digest[(separator + 1)..];
            if (hash.Length < 2)
            {
                throw new ArgumentException($"Digest '{digest}' is invalid.", nameof(digest));
            }

            return hash;
        }
    }
}
