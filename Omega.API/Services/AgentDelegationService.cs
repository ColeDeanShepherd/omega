using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Omega.API.Settings;
using Omega.Core;

namespace Omega.API.Services;

public interface IAgentDelegationService
{
    Task<DelegateTodoServiceResult> DelegateTodoAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class AgentDelegationService(
    ITodoService todoService,
    IAiContextService aiContextService,
    IOptions<AgentDelegationSettings> options,
    ILogger<AgentDelegationService> logger) : IAgentDelegationService
{
    public async Task<DelegateTodoServiceResult> DelegateTodoAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return DelegateTodoServiceResult.Failure(
                title: "Invalid to-do id",
                detail: "The to-do id must be greater than zero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var settings = options.Value;

        if (!settings.Enabled)
        {
            return DelegateTodoServiceResult.Failure(
                title: "Agent delegation is disabled",
                detail: $"Set '{AgentDelegationSettings.SectionName}:Enabled' to true in API configuration.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(settings.LauncherFileName))
        {
            return DelegateTodoServiceResult.Failure(
                title: "Agent launcher is not configured",
                detail: $"Set '{AgentDelegationSettings.SectionName}:LauncherFileName' to the AI CLI executable name (for example, 'copilot').",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var getTodosResult = await todoService.GetTodosAsync(cancellationToken);

        if (getTodosResult.Error is not null)
        {
            return DelegateTodoServiceResult.Failure(
                getTodosResult.Error.Title,
                getTodosResult.Error.Detail,
                getTodosResult.Error.StatusCode);
        }

        if (!TryFindContainingTree(getTodosResult.Todos, id, out var containingTree, out var targetTodo, out var pathToTarget))
        {
            return DelegateTodoServiceResult.Failure(
                title: "To-do item not found",
                detail: "No to-do item exists for the requested id.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var executionOrder = BuildExecutionOrder(targetTodo);
        var aiContextResult = await aiContextService.GetAiContextAsync(cancellationToken);

        if (aiContextResult.Error is not null)
        {
            return DelegateTodoServiceResult.Failure(
                aiContextResult.Error.Title,
                aiContextResult.Error.Detail,
                aiContextResult.Error.StatusCode);
        }

        var prompt = BuildDelegationPrompt(
            containingTree,
            targetTodo,
            pathToTarget,
            executionOrder,
            aiContextResult.Content ?? string.Empty);

        try
        {
            var process = StartAgentProcess(settings, prompt, cancellationToken);
            logger.LogInformation("Delegated todo {TodoId} to AI agent using process {ProcessId}.", id, process.Id);

            return DelegateTodoServiceResult.Success(
                process.Id,
                $"Delegated '{targetTodo.Title}' to the AI agent process.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start AI agent process for todo {TodoId}.", id);

            return DelegateTodoServiceResult.Failure(
                title: "Failed to start AI agent process",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static Process StartAgentProcess(AgentDelegationSettings settings, string prompt, CancellationToken cancellationToken)
    {
        var useShellExecute = settings.UseShellExecute;
        var promptFilePath = CreatePromptFileIfNeeded(settings, prompt);

        var psi = new ProcessStartInfo
        {
            FileName = settings.LauncherFileName!,
            UseShellExecute = useShellExecute,
            CreateNoWindow = settings.CreateNoWindow
        };

        if (!string.IsNullOrWhiteSpace(settings.WorkingDirectory))
        {
            psi.WorkingDirectory = settings.WorkingDirectory;
        }

        var args = settings.LauncherArguments ?? [];

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(ExpandPlaceholders(arg, prompt, promptFilePath));
        }

        var passPromptViaStdin = settings.PassPromptViaStandardInput;

        if (passPromptViaStdin)
        {
            if (useShellExecute)
            {
                throw new InvalidOperationException(
                    $"'{AgentDelegationSettings.SectionName}:PassPromptViaStandardInput' requires '{AgentDelegationSettings.SectionName}:UseShellExecute' to be false.");
            }

            psi.RedirectStandardInput = true;
        }

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Process start did not return a running process instance.");

        if (!string.IsNullOrWhiteSpace(promptFilePath))
        {
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                try
                {
                    File.Delete(promptFilePath);
                }
                catch
                {
                    // Ignore cleanup failures for temp prompt files.
                }
            };
        }

        if (passPromptViaStdin)
        {
            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Ignore cancellation cleanup failures.
                }
            });

            process.StandardInput.Write(prompt);
            process.StandardInput.WriteLine();
            process.StandardInput.Close();
        }

        return process;
    }

    private static string? CreatePromptFileIfNeeded(AgentDelegationSettings settings, string prompt)
    {
        var args = settings.LauncherArguments ?? [];
        var needsPromptFile = args.Any(arg => arg.Contains("{PROMPT_FILE}", StringComparison.Ordinal));

        if (!needsPromptFile)
        {
            return null;
        }

        var promptFilePath = Path.Combine(Path.GetTempPath(), $"omega-agent-prompt-{Guid.NewGuid():N}.txt");
        File.WriteAllText(promptFilePath, prompt);
        return promptFilePath;
    }

    private static string ExpandPlaceholders(string value, string prompt, string? promptFilePath) =>
        value
            .Replace("{PROMPT}", prompt, StringComparison.Ordinal)
            .Replace("{PROMPT_FILE}", promptFilePath ?? string.Empty, StringComparison.Ordinal);

    private static bool TryFindContainingTree(
        IReadOnlyList<TodoItem> rootTodos,
        int id,
        out TodoItem containingTree,
        out TodoItem targetTodo,
        out IReadOnlyList<TodoItem> pathToTarget)
    {
        foreach (var rootTodo in rootTodos)
        {
            if (TryFindPath(rootTodo, id, [], out var path))
            {
                containingTree = rootTodo;
                targetTodo = path[^1];
                pathToTarget = path;
                return true;
            }
        }

        containingTree = default!;
        targetTodo = default!;
        pathToTarget = [];
        return false;
    }

    private static bool TryFindPath(
        TodoItem current,
        int id,
        IReadOnlyList<TodoItem> path,
        out IReadOnlyList<TodoItem> fullPath)
    {
        var nextPath = path.Append(current).ToArray();

        if (current.Id == id)
        {
            fullPath = nextPath;
            return true;
        }

        foreach (var child in current.Children)
        {
            if (TryFindPath(child, id, nextPath, out fullPath))
            {
                return true;
            }
        }

        fullPath = [];
        return false;
    }

    private static IReadOnlyList<TodoItem> BuildExecutionOrder(TodoItem target)
    {
        var ordered = new List<TodoItem>();
        AppendDescendantsPostOrder(target, ordered);
        ordered.Add(target);
        return ordered;
    }

    private static void AppendDescendantsPostOrder(TodoItem todo, ICollection<TodoItem> destination)
    {
        foreach (var child in todo.Children)
        {
            AppendDescendantsPostOrder(child, destination);
            destination.Add(child);
        }
    }

    private static string BuildDelegationPrompt(
        TodoItem containingTree,
        TodoItem targetTodo,
        IReadOnlyList<TodoItem> pathToTarget,
        IReadOnlyList<TodoItem> executionOrder,
        string aiContext)
    {
        var containingTreeJson = JsonSerializer.Serialize(containingTree, new JsonSerializerOptions { WriteIndented = true });
        var targetSubtreeJson = JsonSerializer.Serialize(targetTodo, new JsonSerializerOptions { WriteIndented = true });
        var targetPathText = string.Join(" -> ", pathToTarget.Select(t => $"{t.Title} (#{t.Id})"));
        var executionSteps = string.Join(Environment.NewLine, executionOrder.Select((todo, index) =>
            $"{index + 1}. {todo.Title} (id: {todo.Id}, complete: {todo.IsComplete.ToString().ToLowerInvariant()})"));

        var builder = new StringBuilder();
        builder.AppendLine("You are an autonomous engineering agent with access to the host computer.");
        builder.AppendLine("Execute the delegated to-do task using the context below.");
        builder.AppendLine();
        builder.AppendLine("Required behavior:");
        builder.AppendLine("- If the target to-do is a leaf, perform only that task.");
        builder.AppendLine("- If the target to-do has descendants, perform descendant tasks first in execution order, then decide whether the parent target requires additional action.");
        builder.AppendLine("- Do not modify todo completion state in this app.");
        builder.AppendLine("- Summarize what you changed, what you ran, and any blockers.");
        builder.AppendLine();
        builder.AppendLine("AI context (user-maintained markdown):");
        builder.AppendLine(string.IsNullOrWhiteSpace(aiContext) ? "(empty)" : aiContext);
        builder.AppendLine();
        builder.AppendLine($"Target to-do: {targetTodo.Title} (id: {targetTodo.Id})");
        builder.AppendLine($"Path to target within containing tree: {targetPathText}");
        builder.AppendLine();
        builder.AppendLine("Execution order (descendants first, then target):");
        builder.AppendLine(executionSteps);
        builder.AppendLine();
        builder.AppendLine("Containing task tree for context:");
        builder.AppendLine(containingTreeJson);
        builder.AppendLine();
        builder.AppendLine("Target subtree:");
        builder.AppendLine(targetSubtreeJson);

        return builder.ToString();
    }
}

public sealed record DelegateTodoServiceResult(int? ProcessId, string? Message, TodoServiceError? Error)
{
    public static DelegateTodoServiceResult Success(int processId, string message) => new(processId, message, null);

    public static DelegateTodoServiceResult Failure(string title, string detail, int statusCode) =>
        new(null, null, new TodoServiceError(title, detail, statusCode));
}