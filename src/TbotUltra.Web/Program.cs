using System.Text;
using TbotUltra.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(40));
builder.Services.AddSingleton<DesktopHost>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<DesktopHost>());
builder.Services.AddSingleton(_ => new FileBridge(TbotUltra.Worker.ProjectRootLocator.FindProjectRoot()));
builder.Services.AddSingleton<UiHub>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<UiHub>());

var app = builder.Build();

// The UI controls game accounts, so protect it whenever a password is configured.
var password = Environment.GetEnvironmentVariable("TBOT_WEB_PASSWORD");
var user = Environment.GetEnvironmentVariable("TBOT_WEB_USER") ?? "admin";
if (!string.IsNullOrEmpty(password))
{
    var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));
    app.Use(async (context, next) =>
    {
        var isLocalOpen = context.Request.Path == "/api/open" && context.Connection.RemoteIpAddress is { } remote && System.Net.IPAddress.IsLoopback(remote);
        if (!isLocalOpen && context.Request.Headers.Authorization.ToString() != expected)
        {
            context.Response.Headers.WWWAuthenticate = "Basic realm=\"Tbot Ultra\", charset=\"UTF-8\"";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next();
    });
}
else
{
    app.Logger.LogWarning("TBOT_WEB_PASSWORD is not set: the web UI is reachable without a password.");
}

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
app.UseDefaultFiles();
app.UseStaticFiles();
app.Map("/ws", async (HttpContext context, UiHub hub) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await hub.AcceptAsync(socket, context.RequestAborted);
});
app.MapGet("/health", () => Results.Ok("ok"));

// Images referenced by the desktop markup (/Assets/...), copied next to the app at build time.
app.MapGet("/assets/{name}", (string name) =>
{
    var path = Path.Combine(AppContext.BaseDirectory, "wwwroot", "assets", Path.GetFileName(name));
    var type = Path.GetExtension(path).ToLowerInvariant() switch { ".png" => "image/png", ".jpg" => "image/jpeg", _ => "application/octet-stream" };
    return File.Exists(path) ? Results.File(path, type) : Results.NotFound();
});

app.MapPost("/api/upload/{id:guid}", async (Guid id, HttpRequest request, FileBridge files) =>
{
    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    return file is not null && await files.CompleteUploadAsync(id, file) ? Results.Ok() : Results.BadRequest();
}).DisableAntiforgery();

app.MapGet("/api/files/{name}", (string name, FileBridge files) =>
{
    var path = Path.Combine(files.ExportDirectory, Path.GetFileName(name));
    return File.Exists(path) ? Results.File(path, "application/octet-stream", Path.GetFileName(path)) : Results.NotFound();
});
app.MapGet("/api/info", () => Results.Ok(new
{
    // {host} is replaced in the browser, so the link works however the container is reached.
    vncUrl = Environment.GetEnvironmentVariable("ENABLE_VNC") == "false"
        ? null
        : Environment.GetEnvironmentVariable("TBOT_VNC_URL") ?? (Environment.GetEnvironmentVariable("DISPLAY") is null ? null : "http://{host}:6080/vnc.html?autoconnect=1&resize=scale"),
}));

// Links the desktop code opens with the OS shell (xdg-open in the container) open in the browser tab.
app.MapPost("/api/open", async (HttpContext context, UiHub hub) =>
{
    if (context.Connection.RemoteIpAddress is not { } remote || !System.Net.IPAddress.IsLoopback(remote))
    {
        return Results.Forbid();
    }

    var form = await context.Request.ReadFormAsync();
    var url = form["url"].ToString();
    if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
    {
        hub.OpenInBrowser(uri.ToString());
    }

    return Results.Ok();
});

app.Run();
