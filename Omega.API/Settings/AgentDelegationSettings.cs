namespace Omega.API.Settings;

public sealed class AgentDelegationSettings
{
    public const string SectionName = "AgentDelegation";

    public bool Enabled { get; init; }

    public string? LauncherFileName { get; init; }

    public string[] LauncherArguments { get; init; } = [];

    public string? WorkingDirectory { get; init; }

    public bool PassPromptViaStandardInput { get; init; }

    public bool UseShellExecute { get; init; }

    public bool CreateNoWindow { get; init; } = true;
}