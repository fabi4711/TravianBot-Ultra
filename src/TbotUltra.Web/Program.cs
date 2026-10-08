using TbotUltra.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<DesktopHost>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<DesktopHost>());

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.Run();
