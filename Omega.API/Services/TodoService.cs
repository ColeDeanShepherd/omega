using System.Text.Json;
using Microsoft.Extensions.Options;
using Omega.API.Settings;
using Omega.Core;

namespace Omega.API.Services;

public interface ITodoService
{
    Task<TodoServiceResult> GetTodosAsync(CancellationToken cancellationToken = default);
    Task<AddTodoServiceResult> AddTodoAsync(string title, int? parentId = null, CancellationToken cancellationToken = default);
    Task<SetTodoCompletionServiceResult> SetTodoCompletionAsync(int id, bool isComplete, CancellationToken cancellationToken = default);
    Task<UpdateTodoTitleServiceResult> UpdateTodoTitleAsync(int id, string title, CancellationToken cancellationToken = default);
    Task<MoveTodoServiceResult> MoveTodoAsync(int id, bool moveUp, CancellationToken cancellationToken = default);
    Task<DeleteTodoServiceResult> DeleteTodoAsync(int id, bool promoteChildren = false, CancellationToken cancellationToken = default);
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

    public async Task<AddTodoServiceResult> AddTodoAsync(string title, int? parentId = null, CancellationToken cancellationToken = default)
    {
        var trimmedTitle = title?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedTitle))
        {
            return AddTodoServiceResult.Failure(
                title: "Todo title is required",
                detail: "Provide a non-empty title when creating a to-do item.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (parentId is <= 0)
        {
            return AddTodoServiceResult.Failure(
                title: "Invalid parent to-do id",
                detail: "The parent to-do id must be greater than zero when provided.",
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

        var highestTodoId = GetHighestTodoId(readResult.Todos);
        var nextId = highestTodoId == 0 ? 1 : highestTodoId + 1;
        var newTodo = new TodoItem(nextId, trimmedTitle, false);
        IReadOnlyList<TodoItem> updatedTodos;

        if (parentId is null)
        {
            updatedTodos = readResult.Todos.Append(newTodo).ToArray();
        }
        else if (!TryAddChildTodo(readResult.Todos, parentId.Value, newTodo, out updatedTodos))
        {
            return AddTodoServiceResult.Failure(
                title: "Parent to-do item not found",
                detail: "No parent to-do item exists for the requested id.",
                statusCode: StatusCodes.Status404NotFound);
        }

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

    private static bool TryAddChildTodo(
        IReadOnlyList<TodoItem> todos,
        int parentId,
        TodoItem child,
        out IReadOnlyList<TodoItem> updatedTodos)
    {
        var rewrittenTodos = new List<TodoItem>(todos.Count);

        foreach (var todo in todos)
        {
            if (todo.Id == parentId)
            {
                var appendedChildren = todo.Children.Append(child).ToArray();
                rewrittenTodos.Add(todo with { Children = appendedChildren });
                rewrittenTodos.AddRange(todos.Skip(rewrittenTodos.Count));
                updatedTodos = rewrittenTodos;
                return true;
            }

            if (todo.Children.Count > 0 && TryAddChildTodo(todo.Children, parentId, child, out var updatedChildren))
            {
                rewrittenTodos.Add(todo with { Children = updatedChildren });
                rewrittenTodos.AddRange(todos.Skip(rewrittenTodos.Count));
                updatedTodos = rewrittenTodos;
                return true;
            }

            rewrittenTodos.Add(todo);
        }

        updatedTodos = todos;
        return false;
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

        if (!TrySetTodoCompletion(readResult.Todos, id, isComplete, out var updatedTodos, out var updatedTodo))
        {
            return SetTodoCompletionServiceResult.Failure(
                title: "To-do item not found",
                detail: "No to-do item exists for the requested id.",
                statusCode: StatusCodes.Status404NotFound);
        }

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

    public async Task<UpdateTodoTitleServiceResult> UpdateTodoTitleAsync(int id, string title, CancellationToken cancellationToken = default)
    {
        var trimmedTitle = title?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedTitle))
        {
            return UpdateTodoTitleServiceResult.Failure(
                title: "Todo title is required",
                detail: "Provide a non-empty title when updating a to-do item.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (id <= 0)
        {
            return UpdateTodoTitleServiceResult.Failure(
                title: "Invalid to-do id",
                detail: "The to-do id must be greater than zero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var todoFilePathResult = GetValidatedTodoFilePath();

        if (todoFilePathResult.Error is not null)
        {
            return UpdateTodoTitleServiceResult.Failure(
                todoFilePathResult.Error.Title,
                todoFilePathResult.Error.Detail,
                todoFilePathResult.Error.StatusCode);
        }

        var readResult = await ReadTodosAsync(todoFilePathResult.FilePath!, cancellationToken);

        if (readResult.Error is not null)
        {
            return UpdateTodoTitleServiceResult.Failure(
                readResult.Error.Title,
                readResult.Error.Detail,
                readResult.Error.StatusCode);
        }

        if (!TryUpdateTodoTitle(readResult.Todos, id, trimmedTitle, out var updatedTodos, out var updatedTodo))
        {
            return UpdateTodoTitleServiceResult.Failure(
                title: "To-do item not found",
                detail: "No to-do item exists for the requested id.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var writeResult = await WriteTodosAsync(todoFilePathResult.FilePath!, updatedTodos, cancellationToken);

        if (writeResult is not null)
        {
            return UpdateTodoTitleServiceResult.Failure(
                writeResult.Title,
                writeResult.Detail,
                writeResult.StatusCode);
        }

        return UpdateTodoTitleServiceResult.Success(updatedTodo);
    }

    public async Task<MoveTodoServiceResult> MoveTodoAsync(int id, bool moveUp, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return MoveTodoServiceResult.Failure(
                title: "Invalid to-do id",
                detail: "The to-do id must be greater than zero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var todoFilePathResult = GetValidatedTodoFilePath();

        if (todoFilePathResult.Error is not null)
        {
            return MoveTodoServiceResult.Failure(
                todoFilePathResult.Error.Title,
                todoFilePathResult.Error.Detail,
                todoFilePathResult.Error.StatusCode);
        }

        var readResult = await ReadTodosAsync(todoFilePathResult.FilePath!, cancellationToken);

        if (readResult.Error is not null)
        {
            return MoveTodoServiceResult.Failure(
                readResult.Error.Title,
                readResult.Error.Detail,
                readResult.Error.StatusCode);
        }

        if (!TryMoveTodo(readResult.Todos, id, moveUp, out var updatedTodos, out var movedTodo, out var moveError, out var foundTodo))
        {
            if (foundTodo)
            {
                return MoveTodoServiceResult.Failure(
                    title: "Cannot move to-do item",
                    detail: moveError ?? "The requested move is not possible.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return MoveTodoServiceResult.Failure(
                title: "To-do item not found",
                detail: "No to-do item exists for the requested id.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var writeResult = await WriteTodosAsync(todoFilePathResult.FilePath!, updatedTodos, cancellationToken);

        if (writeResult is not null)
        {
            return MoveTodoServiceResult.Failure(
                writeResult.Title,
                writeResult.Detail,
                writeResult.StatusCode);
        }

        return MoveTodoServiceResult.Success(movedTodo);
    }

    public async Task<DeleteTodoServiceResult> DeleteTodoAsync(int id, bool promoteChildren = false, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return DeleteTodoServiceResult.Failure(
                title: "Invalid to-do id",
                detail: "The to-do id must be greater than zero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var todoFilePathResult = GetValidatedTodoFilePath();

        if (todoFilePathResult.Error is not null)
        {
            return DeleteTodoServiceResult.Failure(
                todoFilePathResult.Error.Title,
                todoFilePathResult.Error.Detail,
                todoFilePathResult.Error.StatusCode);
        }

        var readResult = await ReadTodosAsync(todoFilePathResult.FilePath!, cancellationToken);

        if (readResult.Error is not null)
        {
            return DeleteTodoServiceResult.Failure(
                readResult.Error.Title,
                readResult.Error.Detail,
                readResult.Error.StatusCode);
        }

        bool found;
        IReadOnlyList<TodoItem> updatedTodos;

        if (promoteChildren)
            found = TryDeleteAndPromoteChildren(readResult.Todos, id, out updatedTodos);
        else
            found = TryDeleteTodo(readResult.Todos, id, out updatedTodos);

        if (!found)
        {
            return DeleteTodoServiceResult.Failure(
                title: "To-do item not found",
                detail: "No to-do item exists for the requested id.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var writeResult = await WriteTodosAsync(todoFilePathResult.FilePath!, updatedTodos, cancellationToken);

        if (writeResult is not null)
        {
            return DeleteTodoServiceResult.Failure(
                writeResult.Title,
                writeResult.Detail,
                writeResult.StatusCode);
        }

        return DeleteTodoServiceResult.Success();
    }

    private static int GetHighestTodoId(IReadOnlyList<TodoItem> todos)
    {
        var highestId = 0;
        var stack = new Stack<TodoItem>(todos);

        while (stack.Count > 0)
        {
            var todo = stack.Pop();

            if (todo.Id > highestId)
            {
                highestId = todo.Id;
            }

            foreach (var child in todo.Children)
            {
                stack.Push(child);
            }
        }

        return highestId;
    }

    private static bool TrySetTodoCompletion(
        IReadOnlyList<TodoItem> todos,
        int id,
        bool isComplete,
        out IReadOnlyList<TodoItem> updatedTodos,
        out TodoItem updatedTodo)
    {
        var rewrittenTodos = new List<TodoItem>(todos.Count);

        foreach (var todo in todos)
        {
            if (todo.Id == id)
            {
                updatedTodo = todo with { IsComplete = isComplete };
                rewrittenTodos.Add(updatedTodo);
                rewrittenTodos.AddRange(todos.Skip(rewrittenTodos.Count));
                updatedTodos = rewrittenTodos;
                return true;
            }

            if (todo.Children.Count > 0 &&
                TrySetTodoCompletion(todo.Children, id, isComplete, out var updatedChildren, out updatedTodo))
            {
                rewrittenTodos.Add(todo with { Children = updatedChildren });
                rewrittenTodos.AddRange(todos.Skip(rewrittenTodos.Count));
                updatedTodos = rewrittenTodos;
                return true;
            }

            rewrittenTodos.Add(todo);
        }

        updatedTodos = todos;
        updatedTodo = default!;
        return false;
    }

    private static bool TryUpdateTodoTitle(
        IReadOnlyList<TodoItem> todos,
        int id,
        string newTitle,
        out IReadOnlyList<TodoItem> updatedTodos,
        out TodoItem updatedTodo)
    {
        var rewrittenTodos = new List<TodoItem>(todos.Count);

        foreach (var todo in todos)
        {
            if (todo.Id == id)
            {
                updatedTodo = todo with { Title = newTitle };
                rewrittenTodos.Add(updatedTodo);
                rewrittenTodos.AddRange(todos.Skip(rewrittenTodos.Count));
                updatedTodos = rewrittenTodos;
                return true;
            }

            if (todo.Children.Count > 0 &&
                TryUpdateTodoTitle(todo.Children, id, newTitle, out var updatedChildren, out updatedTodo))
            {
                rewrittenTodos.Add(todo with { Children = updatedChildren });
                rewrittenTodos.AddRange(todos.Skip(rewrittenTodos.Count));
                updatedTodos = rewrittenTodos;
                return true;
            }

            rewrittenTodos.Add(todo);
        }

        updatedTodos = todos;
        updatedTodo = default!;
        return false;
    }

    private static bool TryDeleteTodo(
        IReadOnlyList<TodoItem> todos,
        int id,
        out IReadOnlyList<TodoItem> updatedTodos)
    {
        var rewrittenTodos = new List<TodoItem>(todos.Count);

        foreach (var todo in todos)
        {
            if (todo.Id == id)
            {
                rewrittenTodos.AddRange(todos.Skip(rewrittenTodos.Count + 1));
                updatedTodos = rewrittenTodos;
                return true;
            }

            if (todo.Children.Count > 0 && TryDeleteTodo(todo.Children, id, out var updatedChildren))
            {
                rewrittenTodos.Add(todo with { Children = updatedChildren });
                rewrittenTodos.AddRange(todos.Skip(rewrittenTodos.Count));
                updatedTodos = rewrittenTodos;
                return true;
            }

            rewrittenTodos.Add(todo);
        }

        updatedTodos = todos;
        return false;
    }

    private static bool TryDeleteAndPromoteChildren(
        IReadOnlyList<TodoItem> todos,
        int id,
        out IReadOnlyList<TodoItem> updatedTodos)
    {
        var rewrittenTodos = new List<TodoItem>(todos.Count);

        for (var i = 0; i < todos.Count; i++)
        {
            var todo = todos[i];

            if (todo.Id == id)
            {
                rewrittenTodos.AddRange(todo.Children);
                for (var j = i + 1; j < todos.Count; j++)
                    rewrittenTodos.Add(todos[j]);
                updatedTodos = rewrittenTodos;
                return true;
            }

            if (todo.Children.Count > 0 &&
                TryDeleteAndPromoteChildren(todo.Children, id, out var updatedChildren))
            {
                rewrittenTodos.Add(todo with { Children = updatedChildren });
                for (var j = i + 1; j < todos.Count; j++)
                    rewrittenTodos.Add(todos[j]);
                updatedTodos = rewrittenTodos;
                return true;
            }

            rewrittenTodos.Add(todo);
        }

        updatedTodos = todos;
        return false;
    }

    private static bool TryMoveTodo(
        IReadOnlyList<TodoItem> todos,
        int id,
        bool moveUp,
        out IReadOnlyList<TodoItem> updatedTodos,
        out TodoItem movedTodo,
        out string? moveError,
        out bool foundTodo)
    {
        if (!TryFindParentPathAndIndex(todos, id, [], out var parentPath, out var currentIndex))
        {
            updatedTodos = todos;
            movedTodo = default!;
            moveError = null;
            foundTodo = false;
            return false;
        }

        foundTodo = true;
        var siblingTodos = GetSiblingsAtParentPath(todos, parentPath);
        var targetIndex = moveUp ? currentIndex - 1 : currentIndex + 1;

        if (targetIndex < 0 || targetIndex >= siblingTodos.Count)
        {
            updatedTodos = todos;
            movedTodo = default!;
            moveError = moveUp
                ? "The to-do item is already the first item in its group."
                : "The to-do item is already the last item in its group.";

            return false;
        }

        var reorderedSiblings = siblingTodos.ToList();
        (reorderedSiblings[currentIndex], reorderedSiblings[targetIndex]) = (reorderedSiblings[targetIndex], reorderedSiblings[currentIndex]);

        movedTodo = reorderedSiblings[targetIndex];
        updatedTodos = ReplaceSiblingsAtParentPath(todos, parentPath, reorderedSiblings);
        moveError = null;
        return true;
    }

    private static bool TryFindParentPathAndIndex(
        IReadOnlyList<TodoItem> todos,
        int id,
        IReadOnlyList<int> currentParentPath,
        out IReadOnlyList<int> parentPath,
        out int index)
    {
        for (var i = 0; i < todos.Count; i++)
        {
            var todo = todos[i];

            if (todo.Id == id)
            {
                parentPath = currentParentPath;
                index = i;
                return true;
            }

            if (todo.Children.Count > 0)
            {
                var childParentPath = currentParentPath.Append(i).ToArray();

                if (TryFindParentPathAndIndex(todo.Children, id, childParentPath, out parentPath, out index))
                {
                    return true;
                }
            }
        }

        parentPath = [];
        index = -1;
        return false;
    }

    private static IReadOnlyList<TodoItem> GetSiblingsAtParentPath(
        IReadOnlyList<TodoItem> todos,
        IReadOnlyList<int> parentPath)
    {
        var siblings = todos;

        foreach (var parentIndex in parentPath)
        {
            siblings = siblings[parentIndex].Children;
        }

        return siblings;
    }

    private static IReadOnlyList<TodoItem> ReplaceSiblingsAtParentPath(
        IReadOnlyList<TodoItem> todos,
        IReadOnlyList<int> parentPath,
        IReadOnlyList<TodoItem> replacementSiblings,
        int depth = 0)
    {
        if (depth == parentPath.Count)
        {
            return replacementSiblings;
        }

        var rewrittenTodos = new List<TodoItem>(todos.Count);
        var parentIndex = parentPath[depth];

        for (var i = 0; i < todos.Count; i++)
        {
            var todo = todos[i];

            if (i == parentIndex)
            {
                rewrittenTodos.Add(todo with
                {
                    Children = ReplaceSiblingsAtParentPath(todo.Children, parentPath, replacementSiblings, depth + 1)
                });
            }
            else
            {
                rewrittenTodos.Add(todo);
            }
        }

        return rewrittenTodos;
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

public sealed record UpdateTodoTitleServiceResult(TodoItem? Todo, TodoServiceError? Error)
{
    public static UpdateTodoTitleServiceResult Success(TodoItem todo) => new(todo, null);

    public static UpdateTodoTitleServiceResult Failure(string title, string detail, int statusCode) =>
        new(null, new TodoServiceError(title, detail, statusCode));
}

public sealed record MoveTodoServiceResult(TodoItem? Todo, TodoServiceError? Error)
{
    public static MoveTodoServiceResult Success(TodoItem todo) => new(todo, null);

    public static MoveTodoServiceResult Failure(string title, string detail, int statusCode) =>
        new(null, new TodoServiceError(title, detail, statusCode));
}

    public sealed record DeleteTodoServiceResult(TodoServiceError? Error)
    {
        public static DeleteTodoServiceResult Success() => new(Error: null);

        public static DeleteTodoServiceResult Failure(string title, string detail, int statusCode) =>
        new(new TodoServiceError(title, detail, statusCode));
    }

public sealed record TodoServiceError(string Title, string Detail, int StatusCode);
