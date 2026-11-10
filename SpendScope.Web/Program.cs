using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using SpendScope.Web;
using SpendScope.Web.Services;
using SpendScope.Web.Services.Api;
using SpendScope.Web.Services.Auth;
using SpendScope.Web.Services.Browser;
using SpendScope.Web.Services.Charts;
using SpendScope.Web.Services.Http;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var baseAddress = new Uri(builder.HostEnvironment.BaseAddress);

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.VisibleStateDuration = 4000;
    config.SnackbarConfiguration.PreventDuplicates = false;
});

builder.Services.AddSingleton<BrowserStorage>();
builder.Services.AddSingleton<ThemeState>();
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddSingleton<AuthSession>();
builder.Services.AddSingleton<JwtAuthStateProvider>();
builder.Services.AddSingleton<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddTransient<AuthorizationHeaderHandler>();
builder.Services.AddTransient<RefreshOnUnauthorizedHandler>();

// "auth": login / refresh / logout. No auth handlers, so refreshing can never recurse.
builder.Services.AddHttpClient(AuthSession.AuthClient, c => c.BaseAddress = baseAddress);
builder.Services.AddHttpClient<AccountApiClient>(c => c.BaseAddress = baseAddress);

// Authenticated clients: AuthorizationHeaderHandler (outer), then RefreshOnUnauthorizedHandler (spec 19.5).
void AddAuthenticated<TClient>() where TClient : class =>
    builder.Services.AddHttpClient<TClient>(c => c.BaseAddress = baseAddress)
        .AddHttpMessageHandler<AuthorizationHeaderHandler>()
        .AddHttpMessageHandler<RefreshOnUnauthorizedHandler>();

AddAuthenticated<ProfileApiClient>();
AddAuthenticated<ExpenseApiClient>();
AddAuthenticated<CategoryApiClient>();
AddAuthenticated<ReportApiClient>();
AddAuthenticated<AdminApiClient>();

builder.Services.AddScoped<ChartInterop>();

await builder.Build().RunAsync();
