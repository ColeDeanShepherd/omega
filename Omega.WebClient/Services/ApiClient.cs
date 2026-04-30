using System.Net.Http.Json;
using Omega.Core;

namespace Omega.WebClient.Services;

public interface IApiClient
{
    Task<TodoApiResult> GetTodosAsync(CancellationToken cancellationToken = default);
    Task<AddTodoApiResult> AddTodoAsync(string title, int? parentId = null, CancellationToken cancellationToken = default);
    Task<SetTodoCompletionApiResult> SetTodoCompletionAsync(int id, bool isComplete, CancellationToken cancellationToken = default);
    Task<UpdateTodoTitleApiResult> UpdateTodoTitleAsync(int id, string title, CancellationToken cancellationToken = default);
    Task<DeleteTodoApiResult> DeleteTodoAsync(int id, bool promoteChildren = false, CancellationToken cancellationToken = default);
}

public sealed class ApiClient(HttpClient httpClient, IConfiguration configuration) : IApiClient
{
    public async Task<TodoApiResult> GetTodosAsync(CancellationToken cancellationToken = default)
    {
        var apiBaseUrl = configuration["ApiBaseUrl"];

        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return TodoApiResult.Failure("Client configuration is missing ApiBaseUrl.");
        }

        var todosUri = $"{apiBaseUrl.TrimEnd('/')}/api/todos";

        try
        {
            var todos = await httpClient.GetFromJsonAsync<TodoItem[]>(todosUri, cancellationToken);
            return TodoApiResult.Success(todos ?? []);
        }
        catch (Exception ex)
        {
            return TodoApiResult.Failure($"Failed to load to-do items: {ex.Message}");
        }
    }

    public async Task<AddTodoApiResult> AddTodoAsync(string title, int? parentId = null, CancellationToken cancellationToken = default)
    {
        var apiBaseUrl = configuration["ApiBaseUrl"];

        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return AddTodoApiResult.Failure("Client configuration is missing ApiBaseUrl.");
        }

        var trimmedTitle = title?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedTitle))
        {
            return AddTodoApiResult.Failure("Enter a title before adding a to-do item.");
        }

        var todosUri = $"{apiBaseUrl.TrimEnd('/')}/api/todos";

        if (parentId is <= 0)
        {
            return AddTodoApiResult.Failure("The parent to-do id must be greater than zero when provided.");
        }

        try
        {
            var response = await httpClient.PostAsJsonAsync(todosUri, new CreateTodoRequest(trimmedTitle, parentId), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = string.IsNullOrWhiteSpace(errorContent)
                    ? $"Failed to add to-do item. HTTP {(int)response.StatusCode}."
                    : $"Failed to add to-do item. {errorContent}";

                return AddTodoApiResult.Failure(errorMessage);
            }

            var createdTodo = await response.Content.ReadFromJsonAsync<TodoItem>(cancellationToken: cancellationToken);

            return createdTodo is null
                ? AddTodoApiResult.Failure("The API did not return the created to-do item.")
                : AddTodoApiResult.Success(createdTodo);
        }
        catch (Exception ex)
        {
            return AddTodoApiResult.Failure($"Failed to add to-do item: {ex.Message}");
        }
    }

    public async Task<SetTodoCompletionApiResult> SetTodoCompletionAsync(int id, bool isComplete, CancellationToken cancellationToken = default)
    {
        var apiBaseUrl = configuration["ApiBaseUrl"];

        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return SetTodoCompletionApiResult.Failure("Client configuration is missing ApiBaseUrl.");
        }

        if (id <= 0)
        {
            return SetTodoCompletionApiResult.Failure("The to-do id must be greater than zero.");
        }

        var completionUri = $"{apiBaseUrl.TrimEnd('/')}/api/todos/{id}/completion";

        try
        {
            var response = await httpClient.PatchAsJsonAsync(
                completionUri,
                new SetTodoCompletionRequest(isComplete),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = string.IsNullOrWhiteSpace(errorContent)
                    ? $"Failed to update to-do status. HTTP {(int)response.StatusCode}."
                    : $"Failed to update to-do status. {errorContent}";

                return SetTodoCompletionApiResult.Failure(errorMessage);
            }

            var updatedTodo = await response.Content.ReadFromJsonAsync<TodoItem>(cancellationToken: cancellationToken);

            return updatedTodo is null
                ? SetTodoCompletionApiResult.Failure("The API did not return the updated to-do item.")
                : SetTodoCompletionApiResult.Success(updatedTodo);
        }
        catch (Exception ex)
        {
            return SetTodoCompletionApiResult.Failure($"Failed to update to-do status: {ex.Message}");
        }
    }

    public async Task<UpdateTodoTitleApiResult> UpdateTodoTitleAsync(int id, string title, CancellationToken cancellationToken = default)
    {
        var apiBaseUrl = configuration["ApiBaseUrl"];

        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return UpdateTodoTitleApiResult.Failure("Client configuration is missing ApiBaseUrl.");
        }

        if (id <= 0)
        {
            return UpdateTodoTitleApiResult.Failure("The to-do id must be greater than zero.");
        }

        var trimmedTitle = title?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedTitle))
        {
            return UpdateTodoTitleApiResult.Failure("Enter a title before updating a to-do item.");
        }

        var titleUri = $"{apiBaseUrl.TrimEnd('/')}/api/todos/{id}/title";

        try
        {
            var response = await httpClient.PatchAsJsonAsync(
                titleUri,
                new UpdateTodoTitleRequest(trimmedTitle),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = string.IsNullOrWhiteSpace(errorContent)
                    ? $"Failed to update to-do title. HTTP {(int)response.StatusCode}."
                    : $"Failed to update to-do title. {errorContent}";

                return UpdateTodoTitleApiResult.Failure(errorMessage);
            }

            var updatedTodo = await response.Content.ReadFromJsonAsync<TodoItem>(cancellationToken: cancellationToken);

            return updatedTodo is null
                ? UpdateTodoTitleApiResult.Failure("The API did not return the updated to-do item.")
                : UpdateTodoTitleApiResult.Success(updatedTodo);
        }
        catch (Exception ex)
        {
            return UpdateTodoTitleApiResult.Failure($"Failed to update to-do title: {ex.Message}");
        }
    }

    public async Task<DeleteTodoApiResult> DeleteTodoAsync(int id, bool promoteChildren = false, CancellationToken cancellationToken = default)
    {
        var apiBaseUrl = configuration["ApiBaseUrl"];

        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return DeleteTodoApiResult.Failure("Client configuration is missing ApiBaseUrl.");
        }

        if (id <= 0)
        {
            return DeleteTodoApiResult.Failure("The to-do id must be greater than zero.");
        }

        var todoUri = promoteChildren
            ? $"{apiBaseUrl.TrimEnd('/')}/api/todos/{id}?promoteChildren=true"
            : $"{apiBaseUrl.TrimEnd('/')}/api/todos/{id}";

        try
        {
            var response = await httpClient.DeleteAsync(todoUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = string.IsNullOrWhiteSpace(errorContent)
                    ? $"Failed to delete to-do item. HTTP {(int)response.StatusCode}."
                    : $"Failed to delete to-do item. {errorContent}";

                return DeleteTodoApiResult.Failure(errorMessage);
            }

            return DeleteTodoApiResult.Success();
        }
        catch (Exception ex)
        {
            return DeleteTodoApiResult.Failure($"Failed to delete to-do item: {ex.Message}");
        }
    }
}

public sealed record TodoApiResult(IReadOnlyList<TodoItem> Todos, string? ErrorMessage)
{
    public static TodoApiResult Success(IReadOnlyList<TodoItem> todos) => new(todos, null);

    public static TodoApiResult Failure(string errorMessage) =>
        new(Array.Empty<TodoItem>(), errorMessage);
}

public sealed record AddTodoApiResult(TodoItem? Todo, string? ErrorMessage)
{
    public static AddTodoApiResult Success(TodoItem todo) => new(todo, null);

    public static AddTodoApiResult Failure(string errorMessage) =>
        new(null, errorMessage);
}

public sealed record SetTodoCompletionApiResult(TodoItem? Todo, string? ErrorMessage)
{
    public static SetTodoCompletionApiResult Success(TodoItem todo) => new(todo, null);

    public static SetTodoCompletionApiResult Failure(string errorMessage) =>
        new(null, errorMessage);
}

public sealed record UpdateTodoTitleApiResult(TodoItem? Todo, string? ErrorMessage)
{
    public static UpdateTodoTitleApiResult Success(TodoItem todo) => new(todo, null);

    public static UpdateTodoTitleApiResult Failure(string errorMessage) =>
        new(null, errorMessage);
}

public sealed record DeleteTodoApiResult(string? ErrorMessage)
{
    public static DeleteTodoApiResult Success() => new(ErrorMessage: null);

    public static DeleteTodoApiResult Failure(string errorMessage) =>
        new(errorMessage);
}
