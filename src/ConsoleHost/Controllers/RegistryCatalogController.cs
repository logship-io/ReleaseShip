using Microsoft.AspNetCore.Mvc;
using ReleaseShip.ConsoleHost.Models;
using ReleaseShip.Data.Models;
using ReleaseShip.Data.Services;

namespace ReleaseShip.Controllers
{
    [ApiController]
    public class RegistryCatalogController : ControllerBase
    {
        private readonly IContainerRepositoryService repositories;
        private readonly IContainerRegistryService registry;

        public RegistryCatalogController(IContainerRepositoryService repositories, IContainerRegistryService registry)
        {
            this.repositories = repositories;
            this.registry = registry;
        }

        [HttpGet("/v2/")]
        public IActionResult Ping()
        {
            SetRegistryHeaders();
            return Ok();
        }

        [HttpHead("/v2/")]
        public IActionResult PingHead()
        {
            SetRegistryHeaders();
            return Ok();
        }

        [HttpGet("/api/public/registry/namespaces")]
        [Produces("application/json", "application/xml", Type = typeof(RegistryNamespaceModel[]))]
        public async Task<IActionResult> GetPublicNamespaces(CancellationToken token)
        {
            var namespaces = await this.repositories.GetNamespacesAsync(publicOnly: true, token);
            return Ok(namespaces.Select(name => new RegistryNamespaceModel()
            {
                Name = name,
            }));
        }

        [HttpGet("/api/public/registry/repositories")]
        [Produces("application/json", "application/xml", Type = typeof(RegistryRepositoryModel[]))]
        public async Task<IActionResult> GetPublicRepositories([FromQuery(Name = "namespace")] string? namespaceName, CancellationToken token)
        {
            var repos = await this.repositories.GetRepositoriesAsync(namespaceName, publicOnly: true, token);
            return Ok(repos.Select(ToModel));
        }

        [HttpGet("/api/admin/registry/repositories")]
        [Produces("application/json", "application/xml", Type = typeof(RegistryRepositoryModel[]))]
        public async Task<IActionResult> GetRepositories([FromQuery(Name = "namespace")] string? namespaceName, CancellationToken token)
        {
            if (RequireAdmin() is IActionResult denied)
            {
                return denied;
            }

            var repos = await this.repositories.GetRepositoriesAsync(namespaceName, publicOnly: false, token);
            return Ok(repos.Select(ToModel));
        }

        [HttpGet("/api/public/registry/repositories/{*fullName}")]
        [Produces("application/json", "application/xml", Type = typeof(RegistryRepositoryModel))]
        public async Task<IActionResult> GetPublicRepository(string? fullName, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return BadRequest("Invalid repository name.");
            }

            var detail = await this.repositories.GetRepositoryDetailAsync(fullName, token);
            if (detail == null || !detail.Repository.AllowAnonymousPull)
            {
                return NotFound();
            }

            return Ok(ToModel(detail));
        }

        [HttpGet("/api/admin/registry/repositories/{*fullName}")]
        [Produces("application/json", "application/xml", Type = typeof(RegistryRepositoryModel))]
        public async Task<IActionResult> GetRepository(string? fullName, CancellationToken token)
        {
            if (RequireAdmin() is IActionResult denied)
            {
                return denied;
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                return BadRequest("Invalid repository name.");
            }

            var detail = await this.repositories.GetRepositoryDetailAsync(fullName, token);
            if (detail == null)
            {
                return NotFound();
            }

            return Ok(ToModel(detail));
        }

        [HttpGet("/api/admin/registry/tags")]
        [Produces("application/json", "application/xml", Type = typeof(RegistryTagModel[]))]
        public async Task<IActionResult> GetRepositoryTags([FromQuery] string? repositoryName, CancellationToken token)
        {
            if (RequireAdmin() is IActionResult denied)
            {
                return denied;
            }

            if (string.IsNullOrWhiteSpace(repositoryName))
            {
                return BadRequest("Invalid repository name.");
            }

            var tags = await this.registry.GetTagsAsync(repositoryName, token);
            var models = new List<RegistryTagModel>();
            foreach (var tag in tags)
            {
                var manifest = await this.registry.GetManifestAsync(repositoryName, tag, token);
                if (manifest == null)
                {
                    continue;
                }

                models.Add(new RegistryTagModel()
                {
                    Name = tag,
                    ManifestDigest = manifest.Manifest.Digest,
                });
            }

            return Ok(models);
        }

        [HttpPut("/api/admin/registry/repositories/{*fullName}")]
        [Produces("application/json", "application/xml", Type = typeof(RegistryRepositoryModel))]
        public async Task<IActionResult> PutRepository(string? fullName, RegistryRepositoryPutModel model, CancellationToken token)
        {
            if (RequireAdmin() is IActionResult denied)
            {
                return denied;
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                return BadRequest("Invalid repository name.");
            }

            try
            {
                await this.repositories.PutRepositoryAsync(new ContainerRepository()
                {
                    FullName = fullName,
                    Description = model.Description,
                    AllowAnonymousPull = model.AllowAnonymousPull,
                    AllowDelete = model.AllowDelete,
                    DefaultTagMutability = model.DefaultTagMutability,
                }, model.ProtectedTagPatterns, token);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }

            var detail = await this.repositories.GetRepositoryDetailAsync(fullName, token);
            if (detail == null)
            {
                return NotFound();
            }

            return Ok(ToModel(detail));
        }

        private IActionResult? RequireAdmin()
        {
            if (!this.User.Identity?.IsAuthenticated ?? true)
            {
                return Challenge();
            }

            return this.User.IsInRole("Admin") ? null : Forbid();
        }

        private void SetRegistryHeaders()
        {
            Response.Headers["Docker-Distribution-Api-Version"] = "registry/2.0";
        }

        private static RegistryRepositoryModel ToModel(ContainerRepository repository)
        {
            return ToModel(new ContainerRepositoryDetail()
            {
                Repository = repository,
                TagRules = [],
            });
        }

        private static RegistryRepositoryModel ToModel(ContainerRepositoryDetail detail)
        {
            return new RegistryRepositoryModel()
            {
                Id = detail.Repository.Id,
                Namespace = detail.Repository.Namespace,
                Name = detail.Repository.Name,
                FullName = detail.Repository.FullName,
                Description = detail.Repository.Description,
                AllowAnonymousPull = detail.Repository.AllowAnonymousPull,
                AllowDelete = detail.Repository.AllowDelete,
                DefaultTagMutability = detail.Repository.DefaultTagMutability,
                ProtectedTagPatterns = detail.TagRules.Where(rule => rule.IsImmutable).Select(rule => rule.Pattern).ToArray(),
            };
        }
    }
}
