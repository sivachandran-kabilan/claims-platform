using System.Text.Json.Serialization;
using Claims.Api.Auth;
using Claims.Api.Errors;
using Claims.Application;
using Claims.Infrastructure;
using Claims.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddClaimsAuth(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

// Request bodies are application/json only (keeps the OpenAPI spec and generated client unambiguous).
// Enums travel as names ("Motor", "Singapore"), matching how they are stored.
builder.Services.AddControllers(options =>
    {
        var json = options.InputFormatters.OfType<SystemTextJsonInputFormatter>().Single();
        json.SupportedMediaTypes.Clear();
        json.SupportedMediaTypes.Add("application/json");
    })
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("web", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("ETag", "Location")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var bearer = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", bearer);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = [] });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(); // bare 401/403/404/405/429 responses become ProblemDetails

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DevUserSeeder>().SeedAsync(CancellationToken.None);
}

await app.RunAsync();

// Exposed for WebApplicationFactory in the integration tests.
public partial class Program;
