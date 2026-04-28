namespace Omega.API.Settings;

public sealed class DataStoreSettings
{
    public const string SectionName = "DataStore";

    public string? TodoFilePath { get; init; }
}
