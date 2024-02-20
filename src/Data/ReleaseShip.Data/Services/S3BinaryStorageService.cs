using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReleaseShip.Data.Models;
using ReleaseShip.Data.Relational;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ReleaseShip.Data.Services
{
    internal sealed partial class S3BinaryStorageService : BaseBinaryStorageService, IDisposable
    {
        [LoggerMessage(LogLevel.Warning, "Invalid binary path. Path: {Path}; BinaryId: {BinaryId}")]
        internal static partial void log_InvalidBinaryPath(ILogger logger, int binaryId, string? path);

        [LoggerMessage(LogLevel.Warning, "Failed to read binary with path: {Path}.")]
        internal static partial void log_FailedBinaryRead(ILogger logger, string path, Exception exception);

        [LoggerMessage(LogLevel.Warning, "Failed to write binary.")]
        internal static partial void log_FailedBinaryWrite(ILogger logger, Exception exception);

        [LoggerMessage(LogLevel.Warning, "Invalid binary release. BinaryId: {BinaryId}")]
        internal static partial void log_InvalidBinaryRelease(ILogger logger, int binaryId);

        private readonly ILogger<S3BinaryStorageService> logger;
        private readonly IDatabaseContext databaseContext;
        private readonly IConfiguration configuration;
        private readonly AmazonS3Config s3config;
        private readonly AmazonS3Client s3client;
        
        private string ServiceEndpoint => this.configuration.GetSection("Storage:S3").GetValue<string>(nameof(ServiceEndpoint))!;
        private string Bucket => this.configuration.GetSection("Storage:S3").GetValue<string>(nameof(Bucket))!;
        private string AccessKey => this.configuration.GetSection("Storage:S3").GetValue<string>(nameof(AccessKey))!;
        private string SecretKey => this.configuration.GetSection("Storage:S3").GetValue<string>(nameof(SecretKey))!;

        public S3BinaryStorageService(ILogger<S3BinaryStorageService> logger, IDatabaseContext databaseContext, IConfiguration configuration)
            : base(databaseContext, logger)
        {
            this.logger = logger;
            this.databaseContext = databaseContext;
            this.configuration = configuration;
            this.s3config = new AmazonS3Config();
            this.s3config.ServiceURL = ServiceEndpoint;
            
            this.s3client = new AmazonS3Client(AccessKey, SecretKey, this.s3config);
        }

        public override async Task<Stream?> RetrieveAsync(int binaryReleaseId, CancellationToken token)
        {
            var release = await this.GetBinaryReleaseAsync(binaryReleaseId, token);
            if (release == null)
            {
                log_InvalidBinaryRelease(logger, binaryReleaseId);
                return null;
            }

            using var response = await this.s3client.GetObjectAsync(this.Bucket, $"release/{release.Path}", token);
            var ms = new MemoryStream();
            using (var stream = response.ResponseStream)
            {
                await stream.CopyToAsync(ms, token);
                ms.Position = 0;
            }

            return ms;
        }

        public override async Task<int> StoreAsync(string projectId, BinaryUpload model, Stream fileData, CancellationToken token)
        {
            var guid = Guid.NewGuid();
            string extension = Path.GetExtension(model.FileName);
            string filename = $"{projectId}_{model.PlatformId}_{guid}{extension}";

            try
            {
                TransferUtility utility = new TransferUtility(this.s3client);
                TransferUtilityUploadRequest request = new TransferUtilityUploadRequest();
                request.BucketName = this.Bucket;
                request.ContentType = "application/octet-stream";
                request.Key = $"release/{filename}";
                request.InputStream = fileData;
                await utility.UploadAsync(request, token);
                return await this.StoreBinaryReleaseAsync(projectId, model, filename, token);
            }
            catch (Exception ex)
            {
                log_FailedBinaryWrite(logger, ex);
                throw;
            }
        }

        public void Dispose()
        {
            ((IDisposable)s3client).Dispose();
        }
    }
}
