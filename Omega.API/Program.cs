using Omega.API.Services;
using Omega.API.Settings;
using Omega.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddScoped<IAgentDelegationService, AgentDelegationService>();
builder.Services.Configure<DataStoreSettings>(builder.Configuration.GetSection(DataStoreSettings.SectionName));
builder.Services.Configure<AgentDelegationSettings>(builder.Configuration.GetSection(AgentDelegationSettings.SectionName));

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

app.MapPost("/api/todos", async (CreateTodoRequest request, ITodoService todoService, CancellationToken cancellationToken) =>
{
    var result = await todoService.AddTodoAsync(request.Title, request.ParentId, cancellationToken);

    if (result.Error is not null)
    {
        return Results.Problem(
            title: result.Error.Title,
            detail: result.Error.Detail,
            statusCode: result.Error.StatusCode);
    }

    return Results.Created($"/api/todos/{result.Todo!.Id}", result.Todo);
})
.WithName("AddTodo");

app.MapPatch("/api/todos/{id:int}/completion", async (
    int id,
    SetTodoCompletionRequest request,
    ITodoService todoService,
    CancellationToken cancellationToken) =>
{
    var result = await todoService.SetTodoCompletionAsync(id, request.IsComplete, cancellationToken);

    if (result.Error is not null)
    {
        return Results.Problem(
            title: result.Error.Title,
            detail: result.Error.Detail,
            statusCode: result.Error.StatusCode);
    }

    return Results.Ok(result.Todo);
})
.WithName("SetTodoCompletion");

app.MapPatch("/api/todos/{id:int}/title", async (
    int id,
    UpdateTodoTitleRequest request,
    ITodoService todoService,
    CancellationToken cancellationToken) =>
{
    var result = await todoService.UpdateTodoTitleAsync(id, request.Title, cancellationToken);

    if (result.Error is not null)
    {
        return Results.Problem(
            title: result.Error.Title,
            detail: result.Error.Detail,
            statusCode: result.Error.StatusCode);
    }

    return Results.Ok(result.Todo);
})
.WithName("UpdateTodoTitle");

app.MapPatch("/api/todos/{id:int}/position", async (
    int id,
    MoveTodoRequest request,
    ITodoService todoService,
    CancellationToken cancellationToken) =>
{
    var result = await todoService.MoveTodoAsync(id, request.MoveUp, cancellationToken);

    if (result.Error is not null)
    {
        return Results.Problem(
            title: result.Error.Title,
            detail: result.Error.Detail,
            statusCode: result.Error.StatusCode);
    }

    return Results.Ok(result.Todo);
})
.WithName("MoveTodo");

app.MapDelete("/api/todos/{id:int}", async (
    int id,
    bool? promoteChildren,
    ITodoService todoService,
    CancellationToken cancellationToken) =>
{
    var result = await todoService.DeleteTodoAsync(id, promoteChildren ?? false, cancellationToken);

    if (result.Error is not null)
    {
        return Results.Problem(
            title: result.Error.Title,
            detail: result.Error.Detail,
            statusCode: result.Error.StatusCode);
    }

    return Results.NoContent();
})
.WithName("DeleteTodo");

app.MapPost("/api/todos/{id:int}/delegate", async (
    int id,
    IAgentDelegationService agentDelegationService,
    CancellationToken cancellationToken) =>
{
    var result = await agentDelegationService.DelegateTodoAsync(id, cancellationToken);

    if (result.Error is not null)
    {
        return Results.Problem(
            title: result.Error.Title,
            detail: result.Error.Detail,
            statusCode: result.Error.StatusCode);
    }

    return Results.Ok(new DelegateTodoResponse(result.ProcessId!.Value, result.Message!));
})
.WithName("DelegateTodo");

app.Run();
