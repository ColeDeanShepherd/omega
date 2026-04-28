using System.Text.Json;
using Microsoft.Extensions.Options;
using Omega.API.Settings;
using Omega.Core;

namespace Omega.API.Services;

public interface ITodoService
{
    Task<TodoServiceResult> GetTodosAsync(CancellationToken cancellationToken = default);
    Task<AddTodoServiceResult> AddTodoAsync(string title, CancellationToken cancellationToken = default);
    Task<SetTodoCompletionServiceResult> SetTodoCompletionAsync(int id, bool isComplete, CancellationToken cancellationToken = default);
}

public sealed class TodoService(
    ILogger<TodoService> logger,
    IOptions<DataStoreSettings> todoFileSettingsOptions) : ITodoService
{
    public async Task<TodoServiceResult> GetTodosAsync(CancellationToken cancellationToken = default)
    {
        var todoFilePathResult = GetValidatedTodoFilePath();

        if (todoFilePathResult.Error is not null)
        {
            return TodoServiceResult.Failure(
                todoFilePathResult.Error.Title,
                todoFilePathResult.Error.Detail,
                todoFilePathResult.Error.StatusCode);
        }

        var readResult = await ReadTodosAsync(todoFilePathResult.FilePath!, cancellationToken);

        if (readResult.Error is not null)
        {
            return TodoServiceResult.Failure(
                readResult.Error.Title,
                readResult.Error.Detail,
                readResult.Error.StatusCode);
        }

        return TodoServiceResult.Success(readResult.Todos);
    }

    public async Task<AddTodoServiceResult> AddTodoAsync(string title, CancellationToken cancellationToken = default)
    {
        var trimmedTitle = title?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedTitle))
        {
            return AddTodoServiceResult.Failure(
                title: "Todo title is required",
                detail: "Provide a non-empty title when creating a to-do item.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var todoFilePathResult = GetValidatedTodoFilePath();

        if (todoFilePathResult.Error is not null)
        {
            return AddTodoServiceResult.Failure(
                todoFilePathResult.Error.Title,
                todoFilePathResult.Error.Detail,
                todoFilePathResult.Error.StatusCode);
        }

        var readResult = await ReadTodosAsync(todoFilePathResult.FilePath!, cancellationToken);

        if (readResult.Error is not null)
        {
            return AddTodoServiceResult.Failure(
                readResult.Error.Title,
                readResult.Error.Detail,
                readResult.Error.StatusCode);
        }

        var nextId = readResult.Todos.Count == 0 ? 1 : readResult.Todos.Max(todo => todo.Id) + 1;
        var newTodo = new TodoItem(nextId, trimmedTitle, false);
        var updatedTodos = readResult.Todos.Append(newTodo).ToArray();

        var writeResult = await WriteTodosAsync(todoFilePathResult.FilePath!, updatedTodos, cancellationToken);

        if (writeResult is not null)
        {
            return AddTodoServiceResult.Failure(
                writeResult.Title,
                writeResult.Detail,
                writeResult.StatusCode);
        }

        return AddTodoServiceResult.Success(newTodo);
    }

    public async Task<SetTodoCompletionServiceResult> SetTodoCompletionAsync(int id, bool isComplete, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return SetTodoCompletionServiceResult.Failure(
                title: "Invalid to-do id",
                detail: "The to-do id must be greater than zero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var todoFilePathResult = GetValidatedTodoFilePath();

        if (todoFilePathResult.Error is not null)
        {
            return SetTodoCompletionServiceResult.Failure(
                todoFilePathResult.Error.Title,
                todoFilePathResult.Error.Detail,
                todoFilePathResult.Error.StatusCode);
        }

        var readResult = await ReadTodosAsync(todoFilePathResult.FilePath!, cancellationToken);

        if (readResult.Error is not null)
        {
            return SetTodoCompletionServiceResult.Failure(
                readResult.Error.Title,
                readResult.Error.Detail,
                readResult.Error.StatusCode);
        }

        var todoToUpdate = readResult.Todos.FirstOrDefault(todo => todo.Id == id);

        if (todoToUpdate is null)
        {
            return SetTodoCompletionServiceResult.Failure(
                title: "To-do item not found",
                detail: "No to-do item exists for the requested id.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var updatedTodo = todoToUpdate with { IsComplete = isComplete };
        var updatedTodos = readResult.Todos
            .Select(todo => todo.Id == id ? updatedTodo : todo)
            .ToArray();

        var writeResult = await WriteTodosAsync(todoFilePathResult.FilePath!, updatedTodos, cancellationToken);

        if (writeResult is not null)
        {
            return SetTodoCompletionServiceResult.Failure(
                writeResult.Title,
                writeResult.Detail,
                writeResult.StatusCode);
        }

        return SetTodoCompletionServiceResult.Success(updatedTodo);
    }

    private (string? FilePath, TodoServiceError? Error) GetValidatedTodoFilePath()
    {
        var todoFilePath = todoFileSettingsOptions.Value.TodoFilePath;

        if (string.IsNullOrWhiteSpace(todoFilePath))
        {
            return (
                null,
                new TodoServiceError(
                    "Todo file path is not configured",
                    $"Set '{DataStoreSettings.SectionName}:TodoFilePath' in configuration to an absolute path for a JSON file.",
                    StatusCodes.Status500InternalServerError));
        }

        if (!Path.IsPathRooted(todoFilePath))
        {
            return (
                null,
                new TodoServiceError(
                    "Todo file path must be absolute",
                    $"Configuration key '{DataStoreSettings.SectionName}:TodoFilePath' must contain an absolute file path.",
                    StatusCodes.Status500InternalServerError));
        }

        if (!File.Exists(todoFilePath))
        {
            logger.LogWarning("Configured todo file path does not exist: {TodoFilePath}", todoFilePath);

            return (
                null,
                new TodoServiceError(
                    "Todo file was not found",
                    "The configured server file path does not exist.",
                    StatusCodes.Status404NotFound));
        }

        return (todoFilePath, null);
    }

    private async Task<(IReadOnlyList<TodoItem> Todos, TodoServiceError? Error)> ReadTodosAsync(
        string todoFilePath,
        CancellationToken cancellationToken)
    {
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

            return (todos ?? [], null);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Todo JSON parse failed for configured file path.");

            return (
                Array.Empty<TodoItem>(),
                new TodoServiceError(
                    "Todo file contains invalid JSON",
                    "The configured todo file could not be parsed.",
                    StatusCodes.Status500InternalServerError));
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Todo file read failed for configured file path.");

            return (
                Array.Empty<TodoItem>(),
                new TodoServiceError(
                    "Todo file could not be read",
                    "An I/O error occurred while reading the configured todo file.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    private async Task<TodoServiceError?> WriteTodosAsync(
        string todoFilePath,
        IReadOnlyList<TodoItem> todos,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.Open(todoFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(
                stream,
                todos,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                },
                cancellationToken);

            return null;
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Todo file write failed for configured file path.");

            return new TodoServiceError(
                "Todo file could not be updated",
                "An I/O error occurred while writing the configured todo file.",
                StatusCodes.Status500InternalServerError);
        }
    }
}

public sealed record TodoServiceResult(IReadOnlyList<TodoItem> Todos, TodoServiceError? Error)
{
    public static TodoServiceResult Success(IReadOnlyList<TodoItem> todos) => new(todos, null);

    public static TodoServiceResult Failure(string title, string detail, int statusCode) =>
        new(Array.Empty<TodoItem>(), new TodoServiceError(title, detail, statusCode));
}

public sealed record AddTodoServiceResult(TodoItem? Todo, TodoServiceError? Error)
{
    public static AddTodoServiceResult Success(TodoItem todo) => new(todo, null);

    public static AddTodoServiceResult Failure(string title, string detail, int statusCode) =>
        new(null, new TodoServiceError(title, detail, statusCode));
}

public sealed record SetTodoCompletionServiceResult(TodoItem? Todo, TodoServiceError? Error)
{
    public static SetTodoCompletionServiceResult Success(TodoItem todo) => new(todo, null);

    public static SetTodoCompletionServiceResult Failure(string title, string detail, int statusCode) =>
        new(null, new TodoServiceError(title, detail, statusCode));
}

public sealed record TodoServiceError(string Title, string Detail, int StatusCode);
