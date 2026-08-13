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

// Static assets under _framework/, js/, etc. are content-fingerprinted (their filename encodes a
// hash of their content) and MapStaticAssets already marks them cacheable forever - that's fine
// since a new build gets a new filename. The HTML document itself is what *references* those
// fingerprinted filenames, though, and it is NOT fingerprinted - if a browser (or an intermediate
// proxy/cache) holds onto a stale copy of the page after a redeploy, it will reference a
// fingerprinted asset filename that no longer exists in the new build, 404 trying to load it, and
// the whole app becomes non-interactive since the Blazor circuit can never start. Force the HTML
// document to always be revalidated so a stale page can never outlive a redeploy.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        if (context.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true)
        {
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
        }
        return Task.CompletedTask;
    });
    await next();
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
