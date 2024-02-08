using Microsoft.Extensions.DependencyInjection;
using ReleaseShip.Data.Services;

namespace ReleaseShip.Data
{
    public static class Extensions
    {
        public static IServiceCollection AddReleaseShipData(this IServiceCollection services)
        {
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
            return services
                .AddTransient<IBinaryStorageService, FileBinaryStorageService>()
                .AddTransient<IProjectStorageService, ProjectService>()
                .AddTransient<IReleaseTagsService, ReleaseTagsService>()
                .AddTransient<IPlatformService, PlatformService>()
            ;
        }
    }
}
