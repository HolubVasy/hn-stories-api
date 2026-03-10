using HackerNewsApi.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCustomMiddleware();
app.MapOpenApi();
app.UseSwaggerUI(o =>
    o.SwaggerEndpoint("/openapi/v1.json", "HackerNews API"));
app.MapStoryEndpoints();

app.Run();

// Required for WebApplicationFactory in integration tests.
public partial class Program { }
