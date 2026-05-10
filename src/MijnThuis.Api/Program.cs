using MijnThuis.Application;
using MijnThuis.Infrastructure.Sql;
using MijnThuis.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddApplication();
builder.Services.AddInfrastructureSql(builder.Configuration);

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
   .WithName("Health")
   .WithOpenApi();

app.MapApiEndpoints();

app.Run();
