using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ReleaseShip.Data.Models;
using ReleaseShip.Data.Services;

namespace ReleaseShip.Controllers
{
    [ApiController]
    public class ContainerDistributionController : ControllerBase
    {
        private const string BlobsSegment = "/blobs/";
        private const string UploadsSegment = "/blobs/uploads/";
        private const string UploadsStartSuffix = "/blobs/uploads";
        private const string ManifestsSegment = "/manifests/";
        private const string TagsListSuffix = "/tags/list";

        private readonly IContainerRegistryService registry;

        public ContainerDistributionController(IContainerRegistryService registry)
        {
            this.registry = registry;
        }

        [HttpGet("/v2/{**rest}")]
        public async Task<IActionResult> Get(string? rest, CancellationToken token)
        {
            SetRegistryHeaders();

            if (TryParseBlobRoute(rest, out var repositoryName, out var digest))
            {
                if (await RequireRepositoryAccessAsync(repositoryName, "registry:pull", allowAnonymous: true, token) is IActionResult denied)
                {
                    return denied;
                }

                var stream = await this.registry.OpenBlobReadAsync(repositoryName, digest, token);
                if (stream == null)
                {
                    return NotFound();
                }

                Response.Headers["Docker-Content-Digest"] = digest;
                return File(stream, "application/octet-stream");
            }

            if (TryParseUploadRoute(rest, out repositoryName, out var uploadId))
            {
                if (await RequireRepositoryAccessAsync(repositoryName, "registry:push", allowAnonymous: false, token) is IActionResult deniedUpload)
                {
                    return deniedUpload;
                }

                var upload = await this.registry.GetUploadAsync(repositoryName, uploadId, token);
                if (upload == null)
                {
                    return NotFound();
                }

                Response.Headers.Location = BuildUploadLocation(repositoryName, upload.Id);
                Response.Headers["Docker-Upload-UUID"] = upload.Id;
                Response.Headers["Range"] = BuildRangeHeader(upload.OffsetBytes);
                return Accepted();
            }

            if (TryParseManifestRoute(rest, out repositoryName, out var reference))
            {
                if (await RequireRepositoryAccessAsync(repositoryName, "registry:pull", allowAnonymous: true, token) is IActionResult denied)
                {
                    return denied;
                }

                var manifest = await this.registry.GetManifestAsync(repositoryName, reference, token);
                if (manifest == null)
                {
                    return NotFound();
                }

                Response.Headers["Docker-Content-Digest"] = manifest.Manifest.Digest;
                return File(manifest.Manifest.ContentBytes, manifest.Manifest.MediaType);
            }

            if (TryParseTagsListRoute(rest, out repositoryName))
            {
                if (await RequireRepositoryAccessAsync(repositoryName, "registry:pull", allowAnonymous: true, token) is IActionResult denied)
                {
                    return denied;
                }

                var tags = await this.registry.GetTagsAsync(repositoryName, token);
                return Ok(new { name = repositoryName, tags });
            }

            return NotFound();
        }

        [HttpHead("/v2/{**rest}")]
        public async Task<IActionResult> Head(string? rest, CancellationToken token)
        {
            SetRegistryHeaders();

            if (TryParseBlobRoute(rest, out var repositoryName, out var digest))
            {
                if (await RequireRepositoryAccessAsync(repositoryName, "registry:pull", allowAnonymous: true, token) is IActionResult denied)
                {
                    return denied;
                }

                bool exists = await this.registry.BlobExistsAsync(repositoryName, digest, token);
                if (!exists)
                {
                    return NotFound();
                }

                Response.Headers["Docker-Content-Digest"] = digest;
                return Ok();
            }

            if (TryParseManifestRoute(rest, out repositoryName, out var reference))
            {
                if (await RequireRepositoryAccessAsync(repositoryName, "registry:pull", allowAnonymous: true, token) is IActionResult denied)
                {
                    return denied;
                }

                var manifest = await this.registry.GetManifestAsync(repositoryName, reference, token);
                if (manifest == null)
                {
                    return NotFound();
                }

                Response.Headers["Docker-Content-Digest"] = manifest.Manifest.Digest;
                Response.Headers["Content-Type"] = manifest.Manifest.MediaType;
                return Ok();
            }

            return NotFound();
        }

        [HttpPost("/v2/{**rest}")]
        public async Task<IActionResult> Post(string? rest, CancellationToken token)
        {
            SetRegistryHeaders();

            if (!TryParseStartUploadRoute(rest, out var repositoryName))
            {
                return NotFound();
            }

            if (await RequireRepositoryAccessAsync(repositoryName, "registry:push", allowAnonymous: false, token) is IActionResult deniedManifest)
            {
                return deniedManifest;
            }

            var upload = await this.registry.StartUploadAsync(repositoryName, token);
            Response.Headers.Location = BuildUploadLocation(repositoryName, upload.Id);
            Response.Headers["Docker-Upload-UUID"] = upload.Id;
            Response.Headers["Range"] = BuildRangeHeader(upload.OffsetBytes);
            return Accepted();
        }

        [HttpPatch("/v2/{**rest}")]
        public async Task<IActionResult> Patch(string? rest, CancellationToken token)
        {
            SetRegistryHeaders();

            if (!TryParseUploadRoute(rest, out var repositoryName, out var uploadId))
            {
                return NotFound();
            }

            if (await RequireRepositoryAccessAsync(repositoryName, "registry:push", allowAnonymous: false, token) is IActionResult deniedPatch)
            {
                return deniedPatch;
            }

            var upload = await this.registry.AppendUploadAsync(repositoryName, uploadId, Request.Body, token);
            Response.Headers.Location = BuildUploadLocation(repositoryName, upload.Id);
            Response.Headers["Docker-Upload-UUID"] = upload.Id;
            Response.Headers["Range"] = BuildRangeHeader(upload.OffsetBytes);
            return Accepted();
        }

        [HttpPut("/v2/{**rest}")]
        public async Task<IActionResult> Put(string? rest, [FromQuery] string? digest, CancellationToken token)
        {
            SetRegistryHeaders();
            try
            {
                if (TryParseUploadRoute(rest, out var repositoryName, out var uploadId))
                {
                    if (await RequireRepositoryAccessAsync(repositoryName, "registry:push", allowAnonymous: false, token) is IActionResult deniedUpload)
                    {
                        return deniedUpload;
                    }

                    if (string.IsNullOrWhiteSpace(digest))
                    {
                        return BadRequest("Digest is required.");
                    }

                    if (Request.ContentLength.GetValueOrDefault() > 0)
                    {
                        await this.registry.AppendUploadAsync(repositoryName, uploadId, Request.Body, token);
                    }

                    var blob = await this.registry.CompleteUploadAsync(repositoryName, uploadId, digest, Request.ContentType, token);
                    Response.Headers.Location = BuildBlobLocation(repositoryName, blob.Digest);
                    Response.Headers["Docker-Content-Digest"] = blob.Digest;
                    return StatusCode(StatusCodes.Status201Created);
                }

                if (!TryParseManifestRoute(rest, out repositoryName, out var reference))
                {
                    return NotFound();
                }

                if (await RequireRepositoryAccessAsync(repositoryName, "registry:push", allowAnonymous: false, token) is IActionResult deniedManifest)
                {
                    return deniedManifest;
                }

                using var ms = new MemoryStream();
                await Request.Body.CopyToAsync(ms, token);
                var manifestRequest = ParseManifestRequest(repositoryName, reference, Request.ContentType, ms.ToArray());
                var manifest = await this.registry.PutManifestAsync(manifestRequest, token);
                Response.Headers.Location = BuildManifestLocation(repositoryName, manifest.TagName ?? manifest.Manifest.Digest);
                Response.Headers["Docker-Content-Digest"] = manifest.Manifest.Digest;
                return StatusCode(StatusCodes.Status201Created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(CreateErrorResponse("BAD_REQUEST", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(CreateErrorResponse("DENIED", ex.Message));
            }
        }

        [HttpDelete("/v2/{**rest}")]
        public async Task<IActionResult> Delete(string? rest, CancellationToken token)
        {
            SetRegistryHeaders();

            if (TryParseUploadRoute(rest, out var repositoryName, out var uploadId))
            {
                if (await RequireRepositoryAccessAsync(repositoryName, "registry:push", allowAnonymous: false, token) is IActionResult denied)
                {
                    return denied;
                }

                await this.registry.DeleteUploadAsync(repositoryName, uploadId, token);
                return NoContent();
            }

            if (TryParseManifestRoute(rest, out repositoryName, out var digest))
            {
                if (await RequireRepositoryAccessAsync(repositoryName, "registry:delete", allowAnonymous: false, token) is IActionResult denied)
                {
                    return denied;
                }

                await this.registry.DeleteManifestAsync(repositoryName, digest, token);
                return Accepted();
            }

            return NotFound();
        }

        [HttpDelete("/api/admin/registry/tags")]
        public async Task<IActionResult> DeleteAdminTag([FromQuery] string? repositoryName, [FromQuery] string? tagName, CancellationToken token)
        {
            if (RequireAdmin() is IActionResult denied)
            {
                return denied;
            }

            if (string.IsNullOrWhiteSpace(repositoryName) || string.IsNullOrWhiteSpace(tagName))
            {
                return BadRequest("Repository name and tag name are required.");
            }

            await this.registry.DeleteTagAsync(repositoryName, tagName, token);
            return NoContent();
        }

        private static ContainerManifestPutRequest ParseManifestRequest(string repositoryName, string reference, string? contentType, byte[] contentBytes)
        {
            if (contentBytes.Length == 0)
            {
                throw new InvalidOperationException("Manifest payload is empty.");
            }

            using var doc = JsonDocument.Parse(contentBytes);
            var root = doc.RootElement;
            if (!root.TryGetProperty("schemaVersion", out var schemaVersionNode))
            {
                throw new InvalidOperationException("Manifest is missing schemaVersion.");
            }

            string mediaType = !string.IsNullOrWhiteSpace(contentType)
                ? contentType
                : root.TryGetProperty("mediaType", out var mediaTypeNode)
                    ? mediaTypeNode.GetString() ?? "application/vnd.oci.image.manifest.v1+json"
                    : "application/vnd.oci.image.manifest.v1+json";

            string? configDigest = null;
            var blobDigests = new List<string>();
            if (root.TryGetProperty("config", out var configNode) && TryGetDigest(configNode, out var parsedConfigDigest))
            {
                configDigest = parsedConfigDigest;
                blobDigests.Add(parsedConfigDigest);
            }

            if (root.TryGetProperty("layers", out var layersNode) && layersNode.ValueKind == JsonValueKind.Array)
            {
                foreach (var layer in layersNode.EnumerateArray())
                {
                    if (TryGetDigest(layer, out var layerDigest))
                    {
                        blobDigests.Add(layerDigest);
                    }
                }
            }

            return new ContainerManifestPutRequest()
            {
                RepositoryName = repositoryName,
                Reference = reference,
                MediaType = mediaType,
                ContentBytes = contentBytes,
                ReferencedBlobDigests = blobDigests.Distinct(StringComparer.Ordinal).ToArray(),
                ConfigDigest = configDigest,
                SchemaVersion = schemaVersionNode.GetInt32(),
                ArtifactType = root.TryGetProperty("artifactType", out var artifactTypeNode) ? artifactTypeNode.GetString() : null,
                SubjectDigest = root.TryGetProperty("subject", out var subjectNode) && TryGetDigest(subjectNode, out var subjectDigest) ? subjectDigest : null,
            };
        }

        private static bool TryGetDigest(JsonElement node, out string digest)
        {
            digest = string.Empty;
            if (!node.TryGetProperty("digest", out var digestNode))
            {
                return false;
            }

            string? value = digestNode.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            digest = value.Trim().ToLowerInvariant();
            return true;
        }

        private static bool TryParseBlobRoute(string? rest, out string repositoryName, out string digest)
        {
            return TryParsePrefixAndValue(rest, BlobsSegment, out repositoryName, out digest);
        }

        private static bool TryParseUploadRoute(string? rest, out string repositoryName, out string uploadId)
        {
            return TryParsePrefixAndValue(rest, UploadsSegment, out repositoryName, out uploadId);
        }

        private static bool TryParseManifestRoute(string? rest, out string repositoryName, out string reference)
        {
            return TryParsePrefixAndValue(rest, ManifestsSegment, out repositoryName, out reference);
        }

        private static bool TryParseStartUploadRoute(string? rest, out string repositoryName)
        {
            repositoryName = string.Empty;
            string normalized = NormalizeRest(rest);
            if (!normalized.EndsWith(UploadsStartSuffix, StringComparison.Ordinal))
            {
                return false;
            }

            repositoryName = normalized[..^UploadsStartSuffix.Length];
            repositoryName = repositoryName.Trim('/');
            return !string.IsNullOrWhiteSpace(repositoryName);
        }

        private static bool TryParseTagsListRoute(string? rest, out string repositoryName)
        {
            repositoryName = string.Empty;
            string normalized = NormalizeRest(rest);
            if (!normalized.EndsWith(TagsListSuffix, StringComparison.Ordinal))
            {
                return false;
            }

            repositoryName = normalized[..^TagsListSuffix.Length];
            repositoryName = repositoryName.Trim('/');
            return !string.IsNullOrWhiteSpace(repositoryName);
        }

        private static bool TryParsePrefixAndValue(string? rest, string segment, out string repositoryName, out string value)
        {
            repositoryName = string.Empty;
            value = string.Empty;
            string normalized = NormalizeRest(rest);
            int index = normalized.IndexOf(segment, StringComparison.Ordinal);
            if (index <= 0)
            {
                return false;
            }

            repositoryName = normalized[..index];
            repositoryName = repositoryName.Trim('/');
            value = normalized[(index + segment.Length)..];
            return !string.IsNullOrWhiteSpace(repositoryName) && !string.IsNullOrWhiteSpace(value);
        }

        private static string NormalizeRest(string? rest)
        {
            if (string.IsNullOrWhiteSpace(rest))
            {
                return string.Empty;
            }

            return "/" + rest.Trim().Trim('/');
        }

        private string BuildUploadLocation(string repositoryName, string uploadId)
        {
            return BuildAbsoluteUri($"/v2/{repositoryName}/blobs/uploads/{uploadId}");
        }

        private string BuildBlobLocation(string repositoryName, string digest)
        {
            return BuildAbsoluteUri($"/v2/{repositoryName}/blobs/{digest}");
        }

        private string BuildManifestLocation(string repositoryName, string reference)
        {
            return BuildAbsoluteUri($"/v2/{repositoryName}/manifests/{reference}");
        }

        private string BuildAbsoluteUri(string path)
        {
            return $"{Request.Scheme}://{Request.Host}{path}";
        }

        private static string BuildRangeHeader(long offsetBytes)
        {
            long upper = Math.Max(offsetBytes - 1, 0);
            return $"0-{upper}";
        }

        private static object CreateErrorResponse(string code, string message)
        {
            return new
            {
                errors = new[]
                {
                    new
                    {
                        code,
                        message,
                    }
                }
            };
        }

        private void SetRegistryHeaders()
        {
            Response.Headers["Docker-Distribution-Api-Version"] = "registry/2.0";
        }

        private IActionResult? RequireAdmin()
        {
            if (!this.User.Identity?.IsAuthenticated ?? true)
            {
                return Challenge();
            }

            return this.User.IsInRole("Admin") ? null : Forbid();
        }

        private async Task<IActionResult?> RequireRepositoryAccessAsync(string repositoryName, string permissionClaimType, bool allowAnonymous, CancellationToken token)
        {
            if (allowAnonymous && (!this.User.Identity?.IsAuthenticated ?? true))
            {
                var detail = await this.registry.GetRepositoryDetailAsync(repositoryName, token);
                if (detail?.Repository.AllowAnonymousPull == true)
                {
                    return null;
                }
            }

            if (!this.User.Identity?.IsAuthenticated ?? true)
            {
                return Challenge();
            }

            if (this.User.IsInRole("Admin"))
            {
                return null;
            }

            bool allowed = this.User.Claims
                .Where(claim => string.Equals(claim.Type, permissionClaimType, StringComparison.Ordinal))
                .Select(claim => claim.Value)
                .Any(scope => scope == "*" || string.Equals(scope, repositoryName, StringComparison.OrdinalIgnoreCase) || repositoryName.StartsWith(scope + "/", StringComparison.OrdinalIgnoreCase));

            return allowed ? null : Forbid();
        }
    }
}
