using Microsoft.AspNetCore.Authorization;
using MuseumSystem.Application;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Infrastructure;
using MuseumSystem.Infrastructure.Identity;
using MuseumSystem.Web.Components;
using MuseumSystem.Web.Components.Pages.Photography;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddMuseumApplication();
builder.Services.AddAuthorization(options => options.AddMuseumPolicies());
builder.Services.AddMuseumInfrastructure(builder.Configuration);

var app = builder.Build();

await app.Services.EnsureDevelopmentDatabaseMigratedAsync(app.Environment);
await app.Services.SeedDevelopmentAdminAsync(app.Environment, app.Configuration);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorPages();
app.MapPhotographyImageStreamEndpoint();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;