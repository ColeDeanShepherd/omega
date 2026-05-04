using Microsoft.Extensions.Options;
using Omega.API.Settings;

namespace Omega.API.Services;

public interface IAiContextService
{
    Task<AiContextServiceResult> GetAiContextAsync(CancellationToken cancellationToken = default);
    Task<AiContextServiceResult> SaveAiContextAsync(string content, CancellationToken cancellationToken = default);
}

public sealed class AiContextService(
    ILogger<AiContextService> logger,
    IOptions<DataStoreSettings> dataStoreSettingsOptions) : IAiContextService
{
    public async Task<AiContextServiceResult> GetAiContextAsync(CancellationToken cancellationToken = default)
    {
        var pathResult = GetValidatedAiContextFilePath();

        if (pathResult.Error is not null)
        {
            return AiContextServiceResult.Failure(
                pathResult.Error.Title,
                pathResult.Error.Detail,
                pathResult.Error.StatusCode);
        }

        var filePath = pathResult.FilePath!;

        try
        {
            EnsureParentDirectoryExists(filePath);

            if (!File.Exists(filePath))
            {
                await File.WriteAllTextAsync(filePath, string.Empty, cancellationToken);
            }

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            return AiContextServiceResult.Success(content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read AI context file from {Path}.", filePath);

            return AiContextServiceResult.Failure(
                title: "Failed to read AI context file",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public async Task<AiContextServiceResult> SaveAiContextAsync(string content, CancellationToken cancellationToken = default)
    {
        var pathResult = GetValidatedAiContextFilePath();

        if (pathResult.Error is not null)
        {
            return AiContextServiceResult.Failure(
                pathResult.Error.Title,
                pathResult.Error.Detail,
                pathResult.Error.StatusCode);
        }

        var filePath = pathResult.FilePath!;

        try
        {
            EnsureParentDirectoryExists(filePath);
            await File.WriteAllTextAsync(filePath, content ?? string.Empty, cancellationToken);
            return AiContextServiceResult.Success(content ?? string.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write AI context file to {Path}.", filePath);

            return AiContextServiceResult.Failure(
                title: "Failed to save AI context file",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private AiContextFilePathResult GetValidatedAiContextFilePath()
    {
        var configuredPath = dataStoreSettingsOptions.Value.AiContextFilePath;

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return AiContextFilePathResult.Failure(
                title: "AI context file path is not configured",
                detail: $"Set '{DataStoreSettings.SectionName}:AiContextFilePath' in API configuration.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return AiContextFilePathResult.Success(configuredPath);
    }

    private static void EnsureParentDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}

public sealed record AiContextServiceResult(string? Content, TodoServiceError? Error)
{
    public static AiContextServiceResult Success(string content) => new(content, null);

    public static AiContextServiceResult Failure(string title, string detail, int statusCode) =>
        new(null, new TodoServiceError(title, detail, statusCode));
}

public sealed record AiContextFilePathResult(string? FilePath, TodoServiceError? Error)
{
    public static AiContextFilePathResult Success(string filePath) => new(filePath, null);

    public static AiContextFilePathResult Failure(string title, string detail, int statusCode) =>
        new(null, new TodoServiceError(title, detail, statusCode));
}