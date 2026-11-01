using System.Diagnostics;
using System.Text.Json.Serialization;
using ClaimsModule.API.Authentication;
using ClaimsModule.API.Middleware;
using ClaimsModule.Application;
using ClaimsModule.Application.Common;
using ClaimsModule.Infrastructure;
using ClaimsModule.Persistence;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Lets a reviewer authenticate from Swagger itself: POST /api/dev/auth/token for a role's JWT
    // (Development/Testing only — see DevAuthController), then "Authorize" with it here.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste a JWT from POST /api/dev/auth/token, prefixed with \"Bearer \".",
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", document), [] },
    });
});

builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();

// Without a listener, ASP.NET Core's own "Microsoft.AspNetCore" hosting ActivitySource never
// actually records (no sampler says yes), so Activity.Current would stay null on every request.
// This is what makes CorrelationId propagation into ClaimAuditLog (wayfinder ticket 8) possible
// without pulling in the full OpenTelemetry SDK.
ActivitySource.AddActivityListener(new ActivityListener
{
    ShouldListenTo = _ => true,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
});

var app = builder.Build();

// Registers global job filters + recurring jobs (empty for now; later background-job slice work
// appends to it). Deliberately no app.UseHangfireDashboard() — dashboard auth/exposure is a
// deployment concern, out of scope here (see the background-jobs plan's Resolved Ambiguities).
app.Services.UseInfrastructure();

// Must wrap everything else in the pipeline (ADR-0006) so no unhandled exception from auth,
// routing, or a handler ever falls through to the framework's default error response.
// Registered via app.Use(...) rather than app.UseMiddleware<T>() deliberately: UseMiddleware<T>'s
// reflection binder requires a constructor taking RequestDelegate and an InvokeAsync(HttpContext)-only
// method, which conflicts with this middleware's simpler, directly-unit-testable
// InvokeAsync(HttpContext, RequestDelegate) shape (see ExceptionHandlingMiddlewareTests).
var exceptionHandlingMiddleware = new ExceptionHandlingMiddleware(
    app.Services.GetRequiredService<ILogger<ExceptionHandlingMiddleware>>());
app.Use((context, next) => exceptionHandlingMiddleware.InvokeAsync(context, next));

// Swagger must stay reachable on the deployed URL, not just in Development
// (per REQUIREMENTS.md §20.3 — the deployed Swagger/OpenAPI endpoint is a reviewed deliverable).
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposes the top-level-statement-generated Program class publicly so
// ClaimsModule.ApiIntegrationTests can target it via WebApplicationFactory<Program>.
public partial class Program
{
}
