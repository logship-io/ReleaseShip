using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ReleaseShip.ConsoleHost.Tests
{
    public sealed class ReleaseShipApplicationFactory : WebApplicationFactory<Program>, IDisposable
    {
        public const string BootstrapAdminUsername = "admin";
        public const string BootstrapAdminPassword = "release-ship-tests-admin";

        private readonly string rootDirectory;
        private readonly string databasePath;

        public ReleaseShipApplicationFactory()
        {
            this.rootDirectory = Path.Combine(Path.GetTempPath(), "release-ship-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.rootDirectory);
            this.databasePath = Path.Combine(this.rootDirectory, "release-ship.db");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>()
                {
                    ["ConnectionStrings:Default"] = $"Data Source={this.databasePath}",
                    ["RootDirectory"] = this.rootDirectory,
                    ["Authentication:BootstrapAdmin:Username"] = BootstrapAdminUsername,
                    ["Authentication:BootstrapAdmin:Password"] = BootstrapAdminPassword,
                });
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
            {
                return;
            }

            if (!Directory.Exists(this.rootDirectory))
            {
                return;
            }

            Directory.Delete(this.rootDirectory, recursive: true);
        }
    }
}
