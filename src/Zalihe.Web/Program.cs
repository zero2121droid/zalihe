using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Infrastructure;
using Zalihe.Infrastructure.Jobs;
using Zalihe.Infrastructure.Persistence;
using Zalihe.Web.Auth;
using Zalihe.Web.Errors;
using Zalihe.Web.Hosting;
using Zalihe.Web.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInfrastructure();
builder.Services.AddAppIdentity();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ApiProblems.AddDefaultCode);
builder.Services
    .AddControllers(options => options.Filters.Add<CookieAntiforgeryFilter>())
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = ApiProblems.FromInvalidModelState)
    .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions));
// The OpenAPI document reads these options, so enums are described as strings there too.
builder.Services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

// In production the app sits behind Caddy, which ends HTTPS and forwards plain HTTP. These headers tell
// the app the original scheme and client address, so cookies are marked Secure. Only the proxy can
// reach the app's port (it isn't published), so every sender is trusted; off by default, as locally
// anyone could send them.
var behindProxy = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled");
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

if (behindProxy)
{
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // HTTP to HTTPS redirection is Caddy's job; the browser is told to stay on HTTPS.
    app.UseHsts();
}

// The React build (wwwroot in the container; locally Vite serves it instead).
app.UseStaticFiles(SpaFiles.Assets);

app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
// An unknown API address is a 404 problem, not the React page.
app.Map("/api/{**path}", () => Results.NotFound()).ExcludeFromDescription();
// Every other address without a file extension is a React route (/items/…, /invite/…).
app.MapFallbackToFile("index.html", SpaFiles.IndexPage);

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    // Production applies new migrations when a new version starts; EF Core locks the database
    // while doing it. Locally and in tests they are applied with "dotnet ef" or by the test factory.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.Services.ScheduleRecurringJobs();

app.Run();

// Enums travel as lowercase codes ("kom", "kg") that clients can translate. Numbers must be JSON
// numbers (not "12" strings), so the OpenAPI document and generated types say just "number".
static void ConfigureJson(JsonSerializerOptions options)
{
    options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    options.NumberHandling = JsonNumberHandling.Strict;
}
