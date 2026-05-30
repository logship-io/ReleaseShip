using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using ReleaseShip.ConsoleHost.Models;
using Xunit;

namespace ReleaseShip.ConsoleHost.Tests
{
    public class RegistryCatalogControllerTests : IClassFixture<ReleaseShipApplicationFactory>
    {
        private readonly HttpClient client;

        public RegistryCatalogControllerTests(ReleaseShipApplicationFactory factory)
        {
            this.client = factory.CreateClient();
        }

        [Fact]
        public async Task PingReturnsRegistryHeader()
        {
            using var response = await this.client.GetAsync("/v2/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("Docker-Distribution-Api-Version", out var values));
            Assert.Contains("registry/2.0", values);
        }

        [Fact]
        public async Task PublicCatalogOnlyReturnsAnonymousRepositories()
        {
            var publicPayload = new RegistryRepositoryPutModel()
            {
                Description = "public repo",
                AllowAnonymousPull = true,
                AllowDelete = true,
                DefaultTagMutability = "mutable",
            };
            var privatePayload = new RegistryRepositoryPutModel()
            {
                Description = "private repo",
                AllowAnonymousPull = false,
                AllowDelete = false,
                DefaultTagMutability = "immutable",
            };

            using var publicCreate = await PutAsAdminJsonAsync("/api/admin/registry/repositories/demo/team/public-app", publicPayload);
            using var privateCreate = await PutAsAdminJsonAsync("/api/admin/registry/repositories/demo/team/private-app", privatePayload);

            Assert.Equal(HttpStatusCode.OK, publicCreate.StatusCode);
            Assert.Equal(HttpStatusCode.OK, privateCreate.StatusCode);

            var namespaces = await this.client.GetFromJsonAsync<RegistryNamespaceModel[]>("/api/public/registry/namespaces");
            var repositories = await this.client.GetFromJsonAsync<RegistryRepositoryModel[]>("/api/public/registry/repositories?namespace=demo/team");
            using var publicRepo = await this.client.GetAsync("/api/public/registry/repositories/demo/team/public-app");
            using var privateRepo = await this.client.GetAsync("/api/public/registry/repositories/demo/team/private-app");

            Assert.NotNull(namespaces);
            Assert.Contains(namespaces!, item => item.Name == "demo/team");

            Assert.NotNull(repositories);
            Assert.Contains(repositories!, item => item.FullName == "demo/team/public-app");
            Assert.DoesNotContain(repositories!, item => item.FullName == "demo/team/private-app");

            Assert.Equal(HttpStatusCode.OK, publicRepo.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, privateRepo.StatusCode);
        }

        [Fact]
        public async Task AdminCatalogRequiresAuthentication()
        {
            using var response = await this.client.GetAsync("/api/admin/registry/repositories");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Basic");
        }

        private Task<HttpResponseMessage> PutAsAdminJsonAsync<T>(string requestUri, T payload)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, requestUri)
            {
                Content = JsonContent.Create(payload),
            };
            request.Headers.Authorization = CreateBasicHeader(ReleaseShipApplicationFactory.BootstrapAdminUsername, ReleaseShipApplicationFactory.BootstrapAdminPassword);
            return this.client.SendAsync(request);
        }

        private static AuthenticationHeaderValue CreateBasicHeader(string username, string password)
        {
            string value = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            return new AuthenticationHeaderValue("Basic", value);
        }
    }
}
