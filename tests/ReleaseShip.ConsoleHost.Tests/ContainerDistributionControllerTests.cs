using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReleaseShip.ConsoleHost.Models;
using Xunit;

namespace ReleaseShip.ConsoleHost.Tests
{
    public class ContainerDistributionControllerTests : IClassFixture<ReleaseShipApplicationFactory>
    {
        private readonly ReleaseShipApplicationFactory factory;
        private readonly HttpClient client;

        public ContainerDistributionControllerTests(ReleaseShipApplicationFactory factory)
        {
            this.factory = factory;
            this.client = factory.CreateClient();
        }

        [Fact]
        public async Task RegistryUploadAndManifestFlowWorks()
        {
            const string repositoryName = "demo/team/app";
            await CreateRepositoryAsync(repositoryName, new RegistryRepositoryPutModel()
            {
                Description = "container app",
                AllowAnonymousPull = true,
                AllowDelete = true,
                DefaultTagMutability = "mutable",
            });
            var token = await IssueRepositoryTokenAsync(repositoryName, canDelete: true);
            using var writeClient = CreateClientWithPassword(ReleaseShipApplicationFactory.BootstrapAdminUsername, token.Secret);

            byte[] configBytes = Encoding.UTF8.GetBytes("{\"architecture\":\"amd64\",\"os\":\"linux\"}");
            string configDigest = ComputeDigest(configBytes);

            using var startResponse = await writeClient.PostAsync($"/v2/{repositoryName}/blobs/uploads/", null);
            Assert.Equal(HttpStatusCode.Accepted, startResponse.StatusCode);
            string uploadLocation = startResponse.Headers.Location!.ToString();

            using var chunkResponse = await writeClient.PatchAsync(uploadLocation, new ByteArrayContent(configBytes));
            Assert.Equal(HttpStatusCode.Accepted, chunkResponse.StatusCode);

            using var completeResponse = await writeClient.PutAsync($"{uploadLocation}?digest={configDigest}", null);
            Assert.Equal(HttpStatusCode.Created, completeResponse.StatusCode);

            byte[] manifestBytes = CreateManifestJson(configDigest, configBytes.Length);
            using var manifestResponse = await PutManifestAsync(writeClient, repositoryName, "1.0.0", manifestBytes);
            Assert.Equal(HttpStatusCode.Created, manifestResponse.StatusCode);

            string manifestDigest = manifestResponse.Headers.GetValues("Docker-Content-Digest").Single();

            var tags = await this.client.GetFromJsonAsync<JsonElement>($"/v2/{repositoryName}/tags/list");
            Assert.Equal(repositoryName, tags.GetProperty("name").GetString());
            Assert.Equal("1.0.0", tags.GetProperty("tags")[0].GetString());

            using var headManifest = await this.client.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"/v2/{repositoryName}/manifests/1.0.0"));
            Assert.Equal(HttpStatusCode.OK, headManifest.StatusCode);
            Assert.Equal(manifestDigest, headManifest.Headers.GetValues("Docker-Content-Digest").Single());

            byte[] pulledManifest = await this.client.GetByteArrayAsync($"/v2/{repositoryName}/manifests/{manifestDigest}");
            Assert.Equal(manifestBytes, pulledManifest);

            byte[] pulledConfig = await this.client.GetByteArrayAsync($"/v2/{repositoryName}/blobs/{configDigest}");
            Assert.Equal(configBytes, pulledConfig);
        }

        [Fact]
        public async Task ProtectedTagRejectsOverwrite()
        {
            const string repositoryName = "demo/team/protected-app";
            await CreateRepositoryAsync(repositoryName, new RegistryRepositoryPutModel()
            {
                Description = "protected app",
                AllowAnonymousPull = true,
                AllowDelete = true,
                DefaultTagMutability = "mutable",
                ProtectedTagPatterns = ["latest"],
            });
            var token = await IssueRepositoryTokenAsync(repositoryName, canDelete: true);
            using var writeClient = CreateClientWithPassword(ReleaseShipApplicationFactory.BootstrapAdminUsername, token.Secret);

            string firstDigest = await UploadBlobAsync(writeClient, repositoryName, Encoding.UTF8.GetBytes("{\"version\":1}"));
            byte[] firstManifest = CreateManifestJson(firstDigest, "{\"version\":1}".Length);
            using var firstPush = await PutManifestAsync(writeClient, repositoryName, "latest", firstManifest);
            Assert.Equal(HttpStatusCode.Created, firstPush.StatusCode);

            string secondDigest = await UploadBlobAsync(writeClient, repositoryName, Encoding.UTF8.GetBytes("{\"version\":2}"));
            byte[] secondManifest = CreateManifestJson(secondDigest, "{\"version\":2}".Length);
            using var secondPush = await PutManifestAsync(writeClient, repositoryName, "latest", secondManifest);

            Assert.Equal(HttpStatusCode.Conflict, secondPush.StatusCode);
        }

        private async Task CreateRepositoryAsync(string fullName, RegistryRepositoryPutModel model)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/registry/repositories/{fullName}")
            {
                Content = JsonContent.Create(model),
            };
            request.Headers.Authorization = CreateBasicHeader(ReleaseShipApplicationFactory.BootstrapAdminUsername, ReleaseShipApplicationFactory.BootstrapAdminPassword);
            using var response = await this.client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private async Task<RegistryIssuedTokenModel> IssueRepositoryTokenAsync(string repositoryName, bool canDelete)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/tokens")
            {
                Content = JsonContent.Create(new RegistryTokenIssueModel()
                {
                    PrincipalUsername = ReleaseShipApplicationFactory.BootstrapAdminUsername,
                    Name = $"repo-token-{Guid.NewGuid():N}",
                    ScopeType = "repository",
                    ScopeValue = repositoryName,
                    CanPull = true,
                    CanPush = true,
                    CanDelete = canDelete,
                }),
            };
            request.Headers.Authorization = CreateBasicHeader(ReleaseShipApplicationFactory.BootstrapAdminUsername, ReleaseShipApplicationFactory.BootstrapAdminPassword);
            using var response = await this.client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var token = await response.Content.ReadFromJsonAsync<RegistryIssuedTokenModel>();
            Assert.NotNull(token);
            return token!;
        }

        private HttpClient CreateClientWithPassword(string username, string password)
        {
            var client = this.factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = CreateBasicHeader(username, password);
            return client;
        }

        private static async Task<string> UploadBlobAsync(HttpClient writeClient, string repositoryName, byte[] blobBytes)
        {
            string digest = ComputeDigest(blobBytes);
            using var startResponse = await writeClient.PostAsync($"/v2/{repositoryName}/blobs/uploads/", null);
            string uploadLocation = startResponse.Headers.Location!.ToString();

            using var chunkResponse = await writeClient.PatchAsync(uploadLocation, new ByteArrayContent(blobBytes));
            Assert.Equal(HttpStatusCode.Accepted, chunkResponse.StatusCode);

            using var completeResponse = await writeClient.PutAsync($"{uploadLocation}?digest={digest}", null);
            Assert.Equal(HttpStatusCode.Created, completeResponse.StatusCode);
            return digest;
        }

        private static AuthenticationHeaderValue CreateBasicHeader(string username, string password)
        {
            string value = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            return new AuthenticationHeaderValue("Basic", value);
        }

        private static Task<HttpResponseMessage> PutManifestAsync(HttpClient writeClient, string repositoryName, string reference, byte[] manifestBytes)
        {
            var content = new ByteArrayContent(manifestBytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.oci.image.manifest.v1+json");
            return writeClient.PutAsync($"/v2/{repositoryName}/manifests/{reference}", content);
        }

        private static byte[] CreateManifestJson(string configDigest, int configSize)
        {
            var manifest = new
            {
                schemaVersion = 2,
                mediaType = "application/vnd.oci.image.manifest.v1+json",
                config = new
                {
                    mediaType = "application/vnd.oci.image.config.v1+json",
                    digest = configDigest,
                    size = configSize,
                },
                layers = Array.Empty<object>(),
            };

            return JsonSerializer.SerializeToUtf8Bytes(manifest);
        }

        private static string ComputeDigest(byte[] contentBytes)
        {
            return $"sha256:{Convert.ToHexString(SHA256.HashData(contentBytes)).ToLowerInvariant()}";
        }
    }
}
