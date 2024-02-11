using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Formatters;
using ReleaseShip.Data.Relational;
using ReleaseShip.Data.Services;
using ReleaseShip.Data.SQLite;

ILoggerFactory factory = null!;
ILogger logger = null!;
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += Console_CancelKeyPress;
TaskScheduler.UnobservedTaskException += UnobservedTaskException;

var builder = WebApplication.CreateBuilder(args);
string connectionString = builder.Configuration.GetConnectionString("Default")!;

builder.Services.AddReleaseShipSqlite(connectionString);
builder.Services.AddControllers(options =>
{
    var xml = new XmlSerializerOutputFormatter(factory);
    xml.SupportedMediaTypes.Add("application/rss+xml");
    xml.SupportedMediaTypes.Add("text/rss+xml");
    options.OutputFormatters.Add(xml);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
factory = app.Services.GetRequiredService<ILoggerFactory>();
logger = app.Logger;

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });
}

app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => "ReleaseShip Package Archive");

var ctx = app.Services.GetRequiredService<IDatabaseContext>();
await ReleaseShip.Data.Metadata.IDatabaseContextExtensions.InitializeDatabaseAsync(ctx, logger, cts.Token);

app.Run();

TaskScheduler.UnobservedTaskException -= UnobservedTaskException;
Console.CancelKeyPress -= Console_CancelKeyPress;
return 0;

void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    cts.Cancel();
    e.Cancel = true;
}

void UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
{
#pragma warning disable CA1848 // Use the LoggerMessage delegates
    foreach (var ex in e.Exception.InnerExceptions)
    {
        logger?.LogCritical(ex, nameof(UnobservedTaskException));
    }

    logger?.LogCritical(e.Exception, nameof(UnobservedTaskException));
    Environment.Exit(-2);
#pragma warning restore CA1848 // Use the LoggerMessage delegates
};
