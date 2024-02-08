using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using ReleaseShip.Data.Services;
using ReleaseShip.Models.RSS;

namespace ReleaseShip.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public partial class RssController : ControllerBase
    {
        private readonly ILogger<RssController> logger;
        private readonly IProjectStorageService projects;
        private readonly IReleaseTagsService releaseTags;
        private readonly IBinaryStorageService binaries;

        [LoggerMessage(LogLevel.Debug, "RSS Download")]
        internal static partial void log_RssDownload(ILogger logger);

        public RssController(IProjectStorageService projects, IBinaryStorageService binaries, IReleaseTagsService releaseTags, ILogger<RssController> logger, IWebHostEnvironment hostingEnvironment)
        {
            this.logger = logger;
            this.projects = projects;
            this.releaseTags = releaseTags;
            this.binaries = binaries;
        }


        [HttpGet("/rss/")]
        [OutputCache(Duration = 60)]
        [Produces("application/rss+xml", "application/xml", "application/json", Type = typeof(RssFeed))]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var projects = await this.projects.GetProjectsAsync(token);
            var items = new List<RssItem>();
            foreach (var p in projects)
            {
                var tags = await this.releaseTags.GetTagsAsync(p.Id, token);
                foreach (var tag in tags)
                {
                    items.Add(new RssItem()
                    {
                        Title = $"{p.Name} #{tag.Id}",
                        Description = $"Update release tag \"{tag.Id}\".",
                        Link = $"{this.Request.Scheme}://{this.Request.Host}{this.Request.PathBase}",
                        PubDate = DateTimeOffset.FromUnixTimeSeconds(tag.PublishDateUtc).UtcDateTime,
                        Author = "logship LLC",
                        Enclosure = new RssEnclosure()
                        {
                            Length = 0,
                            Type = "application/octet-stream",
                            Url = $"{this.Request.Scheme}://{this.Request.Host}{this.Request.PathBase}/archive/{tag.BinReleaseId}",
                        }
                    });
                }
            }

            items = items.OrderBy(i => i.PubDate).ToList();
            var rss = new RssFeed()
            {
                Channel = new RssChannel()
                {
                    Description = "The latest downloadable artifacts for releases of logship products and tools.",
                    Link = "https://logship.io",
                    Title = "Logship Artifact Releases",
                    Items = items,
                },
                Version = "2.0",
            };

            log_RssDownload(logger);
            return Ok(rss);
        }
    }
}
