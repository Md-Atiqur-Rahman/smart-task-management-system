using Contoso.TemplateApp.Api.Extensions;
using Contoso.TemplateApp.Application.Configuration;
using Contoso.TemplateApp.Infrastructure.Configuration;

// Composition root. Calls entry points only: no inline service registration.
var builder = WebApplication.CreateBuilder(args);

builder.ConfigureApplication();
builder.ConfigureInfrastructure();

builder.Services.AddSerilogLogging(builder.Configuration);
builder.Services.AddExceptionHandling();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddCorsPolicy(builder.Configuration);
builder.Services.AddRateLimiting(builder.Configuration);
builder.Services.AddApiControllers();
builder.Services.AddOpenApiDocument();

var app = builder.Build();

app.UseExceptionHandling();
app.UseHttpsAndSecurityHeaders();
app.UseRequestLogging();
app.UseCorsPolicy();
app.UseRateLimiting();
app.UseJwtAuthentication();
app.UseHealthEndpoint();
app.UseOpenApiInDevelopment();
app.UseApiControllers();

await app.RunAsync();
