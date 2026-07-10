using System.Text.Json.Serialization;
using ApiKeyManagement.Api.Middleware;
using ApiKeyManagement.AccessPolicy;
using ApiKeyManagement.Infrastructure;
using ApiKeyManagement.KeyLifecycle;
using ApiKeyManagement.TenantManagement;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddTenantManagementModule();
builder.Services.AddKeyLifecycleModule();
builder.Services.AddAccessPolicyModule();

// ADR-024 §1: control-plane JWT auth. Signing key has one source — configuration
// "Jwt:SigningKey" (Base64, HMAC-SHA256, >= 32 bytes) — fail-loud at startup if missing;
// no hardcoded fallback. issuer/audience intentionally unvalidated (single symmetric signer;
// see ADR-024 Rationale). NameClaimType/RoleClaimType align with api-spec.md §2.1 claims.
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException(
        "Configuration \"Jwt:SigningKey\" is required (ADR-024 §1) but was not set.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // MapInboundClaims defaults to true and would rename "sub"/"role" to XML-schema URIs,
        // breaking Actor.FromClaims and the RoleClaimType below — keep raw JWT claim names.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtSigningKey)),
            ValidateLifetime = true,
            ValidateIssuer = false,
            ValidateAudience = false,
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

// ADR-006: enum string wire format aligned with PascalCase enum members.
// allowIntegerValues=false rejects numeric enum values in request bodies.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(allowIntegerValues: false));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseMiddleware<UnhandledExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapKeyLifecycleEndpoints();

app.Run();

// Expose Program for WebApplicationFactory in test projects
public partial class Program;
