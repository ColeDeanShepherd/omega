namespace Omega.Core;

public sealed record TodoItem(int Id, string Title, bool IsComplete)
{
	public IReadOnlyList<TodoItem> Children { get; init; } = [];
}
