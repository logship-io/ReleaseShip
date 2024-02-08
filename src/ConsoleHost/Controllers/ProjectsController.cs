using Microsoft.AspNetCore.Mvc;
using ReleaseShip.Data.Services;
using ReleaseShip.Models;

namespace ReleaseShip.Controllers
{
    [ApiController]
    public class ProjectsController : ControllerBase
    {
        private readonly ILogger<RssController> logger;
        private readonly IProjectStorageService projects;
        private readonly IReleaseTagsService releaseTags;

        public ProjectsController(IProjectStorageService projects, IReleaseTagsService releaseTags, ILogger<RssController> logger)
        {
            this.logger = logger;
            this.projects = projects;
            this.releaseTags = releaseTags;
        }

        [HttpGet("/projects")]
        [Produces("application/json", "application/xml", Type = typeof(ProjectModel))]
        public async Task<IActionResult> GetProjects(CancellationToken token)
        {
            var projects = await this.projects.GetProjectsAsync(token);
            if (projects == null)
            {
                return NotFound();
            }

            return Ok(projects.Select(p => new ProjectModel()
            {
                Id = p.Id,
                Name = p.Name,
            }));
        }

        [HttpGet("/projects/{projectId}")]
        [Produces("application/json", "application/xml", Type = typeof(ProjectModel))]
        public async Task<IActionResult> GetProject(string? projectId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(projectId))
            {
                return BadRequest("Invalid project id.");
            }

            var project = await this.projects.GetProjectAsync(projectId, token);
            if (project == null)
            {
                return NotFound();
            }

            return Ok(new ProjectModel()
            {
                Id = project.Id,
                Name = project.Name,
            });
        }

        [HttpPut("/projects/{projectId}")]
        [Produces("application/json", "application/xml", Type = typeof(ProjectModel))]
        public async Task<IActionResult> PutProject(string projectId, ProjectUploadModel model, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(projectId)
                || projectId.Any(c => Path.GetInvalidPathChars().Contains(c) 
                    || char.IsWhiteSpace(c)))
            {
                return BadRequest("Invalid project id.");
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                model.Name = projectId;
            }

            await this.projects.PutProjectAsync(new Data.Models.Project()
            {
                Id = projectId,
                Name = model.Name,
            }, token);

            var project = await this.projects.GetProjectAsync(projectId, token);
            if (project == null)
            {
                return NotFound();
            }

            return Ok(project);
        }

        [HttpGet("/projects/{projectId}/tags")]
        [Produces("application/json", "application/xml", Type = typeof(BinaryReleaseTagModel[]))]
        public async Task<IActionResult> GetProjectTags(string? projectId, [FromQuery]DateTime? sinceUtc, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(projectId))
            {
                return BadRequest("Invalid project id.");
            }

            var project = await this.projects.GetProjectAsync(projectId, token);
            if (project == null)
            {
                return NotFound();
            }

            var tags = await this.releaseTags.GetTagsAsync(projectId, token, sinceUtc);
            return Ok(tags.Select(t => new BinaryReleaseTagModel()
            {
                BinReleaseId = t.BinReleaseId,
                PlatformId = t.PlatformId,
                CreateDateUtc = DateTimeOffset.FromUnixTimeSeconds(t.PublishDateUtc).UtcDateTime,
                Id = t.Id,
            }).ToArray());
        }
    }
}
