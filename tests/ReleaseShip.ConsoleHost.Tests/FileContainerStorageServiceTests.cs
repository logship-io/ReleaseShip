using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using ReleaseShip.Data.Services;
using Xunit;

namespace ReleaseShip.ConsoleHost.Tests
{
    public class FileContainerStorageServiceTests
    {
        [Fact]
        public async Task CompleteUploadStoresBlobByDigest()
        {
            string root = CreateRootDirectory();

            try
            {
                var service = CreateService(root);
                byte[] payload = Encoding.UTF8.GetBytes("registry test payload");
                string digest = $"sha256:{Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant()}";

                var upload = await service.CreateUploadAsync("demo/team/public-app", CancellationToken.None);
                await service.AppendUploadAsync(upload.Id, new MemoryStream(payload), CancellationToken.None);
                var blob = await service.CompleteUploadAsync(upload.Id, digest, "application/octet-stream", CancellationToken.None);

                Assert.Equal(digest, blob.Digest);
                Assert.True(await service.BlobExistsAsync(digest, CancellationToken.None));

                await using var stream = await service.OpenBlobReadAsync(digest, CancellationToken.None);
                Assert.NotNull(stream);

                using var ms = new MemoryStream();
                await stream!.CopyToAsync(ms, CancellationToken.None);
                Assert.Equal(payload, ms.ToArray());
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public async Task CompleteUploadRejectsDigestMismatch()
        {
            string root = CreateRootDirectory();

            try
            {
                var service = CreateService(root);
                byte[] payload = Encoding.UTF8.GetBytes("registry test payload");

                var upload = await service.CreateUploadAsync("demo/team/private-app", CancellationToken.None);
                await service.AppendUploadAsync(upload.Id, new MemoryStream(payload), CancellationToken.None);

                var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    service.CompleteUploadAsync(upload.Id, "sha256:deadbeef", "application/octet-stream", CancellationToken.None));

                Assert.Contains("digest mismatch", error.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private static FileContainerStorageService CreateService(string root)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>()
                {
                    ["RootDirectory"] = root,
                })
                .Build();

            return new FileContainerStorageService(configuration);
        }

        private static string CreateRootDirectory()
        {
            string root = Path.Combine(Path.GetTempPath(), "release-ship-storage-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }
    }
}
