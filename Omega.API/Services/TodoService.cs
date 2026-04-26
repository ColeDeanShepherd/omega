using System.Text.Json;
using Microsoft.Extensions.Options;
using Omega.API.Settings;
using Omega.Core;

namespace Omega.API.Services;

public interface ITodoService
{
    Task<TodoServiceResult> GetTodosAsync(CancellationToken cancellationToken = default);
}

public sealed class TodoService(
    ILogger<TodoService> logger,
    IOptions<TodoFileSettings> todoFileSettingsOptions) : ITodoService
{
    public async Task<TodoServiceResult> GetTodosAsync(CancellationToken cancellationToken = default)
    {
        var todoFilePath = todoFileSettingsOptions.Value.Path;

        if (string.IsNullOrWhiteSpace(todoFilePath))
        {
            return TodoServiceResult.Failure(
                title: "Todo file path is not configured",
                detail: $"Set '{TodoFileSettings.SectionName}:Path' in configuration to an absolute path for a JSON file.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (!Path.IsPathRooted(todoFilePath))
        {
            return TodoServiceResult.Failure(
                title: "Todo file path must be absolute",
                detail: $"Configuration key '{TodoFileSettings.SectionName}:Path' must contain an absolute file path.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (!File.Exists(todoFilePath))
        {
            logger.LogWarning("Configured todo file path does not exist: {TodoFilePath}", todoFilePath);

            return TodoServiceResult.Failure(
                title: "Todo file was not found",
                detail: "The configured server file path does not exist.",
                statusCode: StatusCodes.Status404NotFound);
        }

        try
        {
            await using var stream = File.OpenRead(todoFilePath);
            var todos = await JsonSerializer.DeserializeAsync<TodoItem[]>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                },
                cancellationToken);

            return TodoServiceResult.Success(todos ?? []);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Todo JSON parse failed for configured file path.");

            return TodoServiceResult.Failure(
                title: "Todo file contains invalid JSON",
                detail: "The configured todo file could not be parsed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Todo file read failed for configured file path.");

            return TodoServiceResult.Failure(
                title: "Todo file could not be read",
                detail: "An I/O error occurred while reading the configured todo file.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}

public sealed record TodoServiceResult(IReadOnlyList<TodoItem> Todos, TodoServiceError? Error)
{
    public static TodoServiceResult Success(IReadOnlyList<TodoItem> todos) => new(todos, null);

    public static TodoServiceResult Failure(string title, string detail, int statusCode) =>
        new(Array.Empty<TodoItem>(), new TodoServiceError(title, detail, statusCode));
}

public sealed record TodoServiceError(string Title, string Detail, int StatusCode);
