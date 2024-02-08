using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReleaseShip.Data.Models;
using ReleaseShip.Data.Relational;
using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace ReleaseShip.Data.Services
{
    internal sealed partial class FileBinaryStorageService : BaseBinaryStorageService
    {
        [LoggerMessage(LogLevel.Warning, "Invalid binary path. Path: {Path}; BinaryId: {BinaryId}")]
        internal static partial void log_InvalidBinaryPath(ILogger logger, int binaryId, string? path);
        
        [LoggerMessage(LogLevel.Warning, "Failed to read binary with path: {Path}.")]
        internal static partial void log_FailedBinaryRead(ILogger logger, string path, Exception exception);

        [LoggerMessage(LogLevel.Warning, "Invalid binary release. BinaryId: {BinaryId}")]
        internal static partial void log_InvalidBinaryRelease(ILogger logger, int binaryId);

        private readonly ILogger<FileBinaryStorageService> logger;
        private readonly IDatabaseContext databaseContext;
        private readonly IConfiguration configuration;

        private string RootDirectory => this.configuration.GetValue<string>(nameof(RootDirectory))!;


        public FileBinaryStorageService(IDatabaseContext databaseContext, IConfiguration configuration, ILogger<FileBinaryStorageService> logger)
            : base(databaseContext, logger)
        {
            this.databaseContext = databaseContext;
            this.logger = logger;
            this.configuration = configuration;
        }

        public override async Task<Stream?> RetrieveAsync(int binaryReleaseId, CancellationToken token)
        {
            var release = await this.GetBinaryReleaseAsync(binaryReleaseId, token);
            if (release == null)
            {
                log_InvalidBinaryRelease(logger, binaryReleaseId);
                return null;
            }
            
            try
            {
                return File.OpenRead(release.Path);
            }
            catch (Exception ex)
            {
                log_FailedBinaryRead(logger, release.Path, ex);
            }

            return null;
        }

        public override async Task<int> StoreAsync(string projectId, BinaryUpload model, Stream fileData, CancellationToken token)
        {
            var guid = Guid.NewGuid();
            string path = Path.Combine(RootDirectory, projectId, guid.ToString(), "bin");
            Directory.CreateDirectory(path);
            string extension = Path.GetExtension(model.FileName);
            string filename = $"{projectId}_{model.PlatformId}{extension}";
            string filepath = Path.Combine(path, filename);

            using var fs = new FileStream(filepath, FileMode.Create);
            await fileData.CopyToAsync(fs, token);
            return await this.StoreBinaryReleaseAsync(projectId, model, filepath, token);
        }
    }
}
