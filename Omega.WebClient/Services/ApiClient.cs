using System.Net.Http.Json;
using Omega.Core;

namespace Omega.WebClient.Services;

public interface IApiClient
{
    Task<TodoApiResult> GetTodosAsync(CancellationToken cancellationToken = default);
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
}

public sealed record TodoApiResult(IReadOnlyList<TodoItem> Todos, string? ErrorMessage)
{
    public static TodoApiResult Success(IReadOnlyList<TodoItem> todos) => new(todos, null);

    public static TodoApiResult Failure(string errorMessage) =>
        new(Array.Empty<TodoItem>(), errorMessage);
}
