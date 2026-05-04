namespace Omega.Core;

public sealed record TodoItem(int Id, string Title, bool IsComplete, DateTimeOffset? CompletedAt)
{
	public IReadOnlyList<TodoItem> Children { get; init; } = [];
}
