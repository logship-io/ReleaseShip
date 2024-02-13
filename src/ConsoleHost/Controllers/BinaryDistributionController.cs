using Microsoft.AspNetCore.Mvc;
using ReleaseShip.ConsoleHost.Models;
using ReleaseShip.Data.Models;
using ReleaseShip.Data.Services;

namespace ReleaseShip.Controllers
{
    [ApiController]
    public partial class BinaryDistributionController : ControllerBase
    {

        const long MaxFileSize = 1000L * 1024L * 1024L; // 1000MB but that's insane

        [LoggerMessage(LogLevel.Warning, "Invalid binary path. Path: {Path}; BinaryId: {BinaryId}")]
        internal static partial void log_InvalidBinaryPath(ILogger logger, int binaryId, string? path, Exception ex);

        [LoggerMessage(LogLevel.Information, "Binary Download. Path: {Path}; BinaryId: {BinaryId}")]
        internal static partial void log_BinaryDownload(ILogger logger, int binaryId, string path);

        [LoggerMessage(LogLevel.Information, "Binary Upload for {Platform} with {Tags}. {FileName}")]
        internal static partial void log_BinaryUpload(ILogger logger, string platform, IEnumerable<string> tags, string filename);

        private readonly ILogger<BinaryDistributionController> logger;
        private readonly IConfiguration configuration;
        private readonly IProjectStorageService projects;
        private readonly IBinaryStorageService binaries;
        private readonly IReleaseTagsService releaseTags;
        private readonly IPlatformService platforms;

        public BinaryDistributionController(IBinaryStorageService binaries, IProjectStorageService projects, IPlatformService platforms, IConfiguration configuration, ILogger<BinaryDistributionController> logger, IReleaseTagsService releaseTags)
            : base()
        {
            this.logger = logger;
            this.projects = projects;
            this.binaries = binaries;
            this.configuration = configuration;
            this.releaseTags = releaseTags;
            this.platforms = platforms;
        }

        [HttpGet("/archive/{releaseId}")]
        [Produces("application/octet-stream")]
        public async Task<IActionResult> Get(int releaseId, CancellationToken token)
        {
            var release = await this.binaries.GetBinaryReleaseAsync(releaseId, token);
            if (release == null)
            {
                return NotFound($"No release exists with id {releaseId}.");
            }

            try
            {
                // Disposing of the stream causes System.ObjectDisposedException: Cannot access a closed file.
                // Hopefully not disposing this doesn't cause problems.
                var stream = await binaries.RetrieveAsync(release.Id, token);
                if (stream == null)
                {
                    return new ContentResult()
                    {
                        StatusCode = 500,
                        Content = $"Unable to read file."
                    };
                }

                var result = new FileStreamResult(stream, "application/octet-stream")
                {
                    FileDownloadName = Path.GetFileName(release.Path),
                };

                log_BinaryDownload(logger, release.Id, release.Path);
                return result;
            }
            catch (Exception ex)
            {
                log_InvalidBinaryPath(logger, 1, release.Path, ex);
                return new ContentResult()
                {
                    StatusCode = 500,
                    Content = $"Oops internal server error."
                };
            }
        }

        [HttpGet("/release/{projectId}/{platform}/{tag}")]
        [Produces("application/octet-stream")]
        public async Task<IActionResult> Get(string projectId, string platform, string tag, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return BadRequest("No release tag specified.");
            }

            if (string.IsNullOrWhiteSpace(platform))
            {
                return BadRequest("No release architecture specified.");
            }

            var project = await this.projects.GetProjectAsync(projectId, token);
            if (project == null)
            {
                return NotFound($"The project \"{projectId}\" does not exist.");
            }

            var release = await this.binaries.GetBinaryReleaseAsync(projectId, tag, platform, token);
            if (release == null)
            {
                return NotFound($"No release \"{tag}\" exists on {platform}.");
            }

            try
            {
                // Disposing of the stream causes System.ObjectDisposedException: Cannot access a closed file.
                // Hopefully not disposing this doesn't cause problems.
                var stream = await binaries.RetrieveAsync(release.Id, token);
                if (stream == null)
                {
                    return new ContentResult()
                    {
                        StatusCode = 500,
                        Content = $"Unable to read file."
                    };
                }

                var result = new FileStreamResult(stream, "application/octet-stream")
                {
                    FileDownloadName = Path.GetFileName(release.Path),
                };

                log_BinaryDownload(logger, release.Id, release.Path);
                return result;
            }
            catch (Exception ex)
            {
                log_InvalidBinaryPath(logger, 1, release.Path, ex);
                return new ContentResult()
                {
                    StatusCode = 500,
                    Content = $"Oops internal server error."
                };
            }
        }

        [HttpPost("/release/{projectId}/")]
        [RequestSizeLimit(MaxFileSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSize)]
        public async Task<IActionResult> Upload(string projectId, [FromForm] ReleaseUploadModel model, CancellationToken token)
        {
            if (model.Tags == null || model.Tags.Count == 0 || model.Tags.Any(string.IsNullOrWhiteSpace))
            {
                return BadRequest("Invalid tags.");
            }

            if (model.Archive == null)
            {
                return BadRequest("Invalid file upload.");
            }

            var project = await projects.GetProjectAsync(projectId, token);
            if (project == null)
            {
                return NotFound($"The project \"{projectId}\" does not exist.");
            }

            var tags = new HashSet<string>(model.Tags);
            await this.platforms.AddPlatform(model.PlatformId, token);
            
            using var stream = model.Archive.OpenReadStream();
            var releaseId = await this.binaries.StoreAsync(projectId, new BinaryUpload()
            {
                PlatformId = model.PlatformId,
                FileName = model.Archive.FileName,
            }, stream, token);

            await this.releaseTags.PutTagsAsync(releaseId, tags, token);
            return Ok();
        }
    }
}
