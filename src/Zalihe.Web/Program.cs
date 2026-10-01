using System.Text.Json;
using System.Text.Json.Serialization;
using Zalihe.Infrastructure;
using Zalihe.Web.Auth;
using Zalihe.Web.Errors;
using Zalihe.Web.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInfrastructure();
builder.Services.AddAppIdentity();

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ApiProblems.AddDefaultCode);
builder.Services
    .AddControllers(options => options.Filters.Add<CookieAntiforgeryFilter>())
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = ApiProblems.FromInvalidModelState)
    .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions));
// The OpenAPI document reads these options, so enums are described as strings there too.
builder.Services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Enums travel as lowercase codes ("kom", "kg") that clients can translate. Numbers must be JSON
// numbers (not "12" strings), so the OpenAPI document and generated types say just "number".
static void ConfigureJson(JsonSerializerOptions options)
{
    options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    options.NumberHandling = JsonNumberHandling.Strict;
}
