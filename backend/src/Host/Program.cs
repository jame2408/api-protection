using System.Text.Json.Serialization;
using ApiKeyManagement.Api.Middleware;
using ApiKeyManagement.AccessPolicy;
using ApiKeyManagement.Infrastructure;
using ApiKeyManagement.KeyLifecycle;
using ApiKeyManagement.TenantManagement;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddTenantManagementModule();
builder.Services.AddKeyLifecycleModule();
builder.Services.AddAccessPolicyModule();

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

app.MapKeyLifecycleEndpoints();

app.Run();

// Expose Program for WebApplicationFactory in test projects
public partial class Program;
