using System.Security.Claims;
using System.Net.Http.Headers;
using System.Text.Json;
using ForgeIdle.Data;
using ForgeIdle.Game;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var dataProtectionKeysPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "App_Data",
    "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("ForgeIdle");

var connectionString = Environment.GetEnvironmentVariable("FORGEIDLE_DB_CONNECTION")
    ?? builder.Configuration.GetConnectionString("ForgeIdle")
    ?? throw new InvalidOperationException(
        "FORGEIDLE_DB_CONNECTION 환경 변수를 설정하세요. database/README.md를 참고하세요.");

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseSqlServer(connectionString));
var authentication = builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ForgeIdle.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
    });

AddSocialLogin(authentication, builder.Configuration, "Kakao", "카카오",
    "https://kauth.kakao.com/oauth/authorize",
    "https://kauth.kakao.com/oauth/token",
    "https://kapi.kakao.com/v2/user/me",
    json => json.RootElement.GetProperty("id").ToString());
builder.Services.AddAuthorization();
builder.Services.AddScoped<PlayerRepository>();
builder.Services.AddScoped<GameService>();
builder.Services.AddSingleton<GameCatalog>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    db.Database.EnsureCreated();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/auth/providers", () => Results.Ok(new
{
    Kakao = IsConfigured(builder.Configuration, "Kakao"),
    TestLogin = IsTestLoginEnabled()
}));

app.MapGet("/api/auth/social/{provider}", (string provider) =>
{
    var scheme = provider.ToLowerInvariant() switch
    {
        "kakao" => "Kakao",
        _ => null
    };
    return scheme is null
        ? Results.NotFound()
        : Results.Challenge(new AuthenticationProperties { RedirectUri = "/" }, [scheme]);
});

app.MapGet("/api/auth/test-login", async (HttpContext context, PlayerRepository players) =>
{
    if (!IsTestLoginEnabled())
        return Results.NotFound();

    var accountName = players.GetOrCreateSocialAccount("test", "operator");
    var player = players.GetRequired(accountName);
    if (string.IsNullOrWhiteSpace(player.Nickname))
        players.SetNickname(accountName, "운영자");

    var identity = new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, accountName)],
        CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity));
    return Results.Redirect("/");
});

app.MapPost("/api/auth/logout", async (HttpContext context) =>
{
    await context.SignOutAsync();
    return Results.Ok(new { ok = true });
}).RequireAuthorization();

app.MapGet("/api/auth/me", (ClaimsPrincipal user, GameService game) =>
    Results.Ok(game.GetPlayer(user.Identity!.Name!))).RequireAuthorization();

app.MapPost("/api/auth/nickname", (NicknameRequest request, ClaimsPrincipal user, PlayerRepository players, GameService game) =>
{
    try
    {
        players.SetNickname(user.Identity!.Name!, request.Nickname);
        return Results.Ok(game.GetPlayer(user.Identity.Name!));
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { message = exception.Message });
    }
}).RequireAuthorization();

app.MapGet("/api/rankings", (PlayerRepository players) =>
    Results.Ok(players.GetRankings()));

app.MapGet("/api/catalog", (GameCatalog catalog) =>
    Results.Ok(new { catalog.Areas, catalog.Enhancements }));

app.MapPost("/api/game/hunt/start", (StartHuntRequest request, ClaimsPrincipal user, GameService game) =>
    Results.Ok(game.StartHunt(user.Identity!.Name!, request.AreaId))).RequireAuthorization();

app.MapPost("/api/game/hunt/claim", (ClaimsPrincipal user, GameService game) =>
    Results.Ok(game.ClaimHunt(user.Identity!.Name!))).RequireAuthorization();

app.MapPost("/api/game/hunt/manual", (ClaimsPrincipal user, GameService game) =>
    Results.Ok(game.ManualHunt(user.Identity!.Name!))).RequireAuthorization();

app.MapPost("/api/game/enhance", (EnhanceRequest request, ClaimsPrincipal user, GameService game) =>
    Results.Ok(game.Enhance(user.Identity!.Name!, request.UseProtection))).RequireAuthorization();

app.MapPost("/api/game/boss", (ClaimsPrincipal user, GameService game) =>
    Results.Ok(game.ChallengeBoss(user.Identity!.Name!))).RequireAuthorization();

app.MapPost("/api/game/stats/invest", (InvestStatRequest request, ClaimsPrincipal user, GameService game) =>
    Results.Ok(game.InvestStat(user.Identity!.Name!, request.Stat))).RequireAuthorization();

app.MapPost("/api/game/stats/reset", (ClaimsPrincipal user, GameService game) =>
    Results.Ok(game.ResetStats(user.Identity!.Name!))).RequireAuthorization();

app.MapFallbackToFile("index.html");

app.Run();

static void AddSocialLogin(
    AuthenticationBuilder authentication,
    IConfiguration configuration,
    string scheme,
    string displayName,
    string authorizationEndpoint,
    string tokenEndpoint,
    string userInformationEndpoint,
    Func<JsonDocument, string> externalId)
{
    authentication.AddOAuth(scheme, displayName, options =>
    {
        options.ClientId = configuration[$"Authentication:{scheme}:ClientId"] ?? "not-configured";
        options.ClientSecret = configuration[$"Authentication:{scheme}:ClientSecret"] ?? "not-configured";
        options.CallbackPath = $"/signin-{scheme.ToLowerInvariant()}";
        options.AuthorizationEndpoint = authorizationEndpoint;
        options.TokenEndpoint = tokenEndpoint;
        options.UserInformationEndpoint = userInformationEndpoint;
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.BackchannelTimeout = TimeSpan.FromSeconds(15);
        options.Events = new OAuthEvents
        {
            OnRemoteFailure = context =>
            {
                Console.Error.WriteLine($"Social login failed for {scheme}: {context.Failure?.Message}");
                context.HandleResponse();
                context.Response.Redirect("/?loginError=social");
                return Task.CompletedTask;
            },
            OnCreatingTicket = async context =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                using var response = await context.Backchannel.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    context.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var accountName = context.HttpContext.RequestServices
                    .GetRequiredService<PlayerRepository>()
                    .GetOrCreateSocialAccount(scheme.ToLowerInvariant(), externalId(json));
                context.Identity!.AddClaim(new Claim(ClaimTypes.Name, accountName));
            }
        };
    });
}

static bool IsConfigured(IConfiguration configuration, string scheme) =>
    !string.IsNullOrWhiteSpace(configuration[$"Authentication:{scheme}:ClientId"]) &&
    !string.IsNullOrWhiteSpace(configuration[$"Authentication:{scheme}:ClientSecret"]);

static bool IsTestLoginEnabled() =>
    !string.Equals(
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
        "Production",
        StringComparison.OrdinalIgnoreCase) &&
    string.Equals(
        Environment.GetEnvironmentVariable("FORGEIDLE_TEST_LOGIN_ENABLED"),
        "true",
        StringComparison.OrdinalIgnoreCase);

public sealed record NicknameRequest(string Nickname);
