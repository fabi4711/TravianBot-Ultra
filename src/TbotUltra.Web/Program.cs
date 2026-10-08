using System.Text;
using TbotUltra.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<DesktopHost>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<DesktopHost>());
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
        if (context.Request.Headers.Authorization.ToString() != expected)
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

app.Run();
