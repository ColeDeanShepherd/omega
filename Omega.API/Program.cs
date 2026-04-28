using Omega.API.Services;
using Omega.API.Settings;
using Omega.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.Configure<DataStoreSettings>(builder.Configuration.GetSection(DataStoreSettings.SectionName));

var webClientOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? throw new InvalidOperationException("CORS allowed origins are not configured. Set 'Cors:AllowedOrigins' in configuration to an array of allowed origin URLs.");

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClient", policy =>
    {
        policy
            .WithOrigins(webClientOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("WebClient");

app.MapGet("/api/todos", async (ITodoService todoService, CancellationToken cancellationToken) =>
{
    var result = await todoService.GetTodosAsync(cancellationToken);

    if (result.Error is not null)
    {
        return Results.Problem(
            title: result.Error.Title,
            detail: result.Error.Detail,
            statusCode: result.Error.StatusCode);
    }

    return Results.Ok(result.Todos);
})
.WithName("GetTodos");

app.Run();
