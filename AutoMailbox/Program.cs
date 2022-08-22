using AutoMailbox;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Starting web host");

    var builder = WebApplication.CreateBuilder();
    builder.Services.AddApplicationInsightsTelemetry();
    builder.Services.AddSingleton(new EmailQueue(1000));
    builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration));

    var app = builder.Build();

    app.MapGet("api/email/{username}", (ILogger<Program> logger, EmailQueue queue, string username) =>
    {
        logger.LogInformation("Retrieving last email for {Username}.", username);
        if (queue.TryGetLatest(username, out var email))
            return Results.Ok(email);
        return Results.NotFound();
    });

    app.MapPost("api/email", (EmailFormModel model, ILogger<Program> logger, EmailQueue queue) =>
    {
        logger.LogInformation("Incoming email: {@Email}", model);

        var email = Email.Parse(model);
        logger.LogInformation("Parsed email: {@Email}", email);

        queue.Enqueue(email);

        return Results.Ok();
    });

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
