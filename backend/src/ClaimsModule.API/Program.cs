using ClaimsModule.API.Middleware;
using ClaimsModule.Application;
using ClaimsModule.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

// Must wrap everything else in the pipeline (ADR-0006) so no unhandled exception from auth,
// routing, or a handler ever falls through to the framework's default error response.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger must stay reachable on the deployed URL, not just in Development
// (per REQUIREMENTS.md §20.3 — the deployed Swagger/OpenAPI endpoint is a reviewed deliverable).
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposes the top-level-statement-generated Program class publicly so
// ClaimsModule.ApiIntegrationTests can target it via WebApplicationFactory<Program>.
public partial class Program
{
}
