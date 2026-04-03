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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapKeyLifecycleEndpoints();

app.Run();

// Expose Program for WebApplicationFactory in test projects
public partial class Program;
