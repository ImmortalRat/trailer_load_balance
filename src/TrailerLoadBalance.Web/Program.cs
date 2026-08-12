using TrailerLoadBalance.Web.Components;
using TrailerLoadBalance.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<TrailerProfileService>();
builder.Services.AddSingleton<CargoCatalogService>();
builder.Services.AddSingleton<LoadCalculationService>();
builder.Services.AddScoped<PersistenceService>();

var app = builder.Build();

// Configure the HTTP request pipeline. This app is deployed as plain HTTP behind a home
// router/firewall (LAN-only, no public exposure), so there's no HTTPS listener to redirect to
// and no HSTS to enforce - both would just break the app if enabled.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
