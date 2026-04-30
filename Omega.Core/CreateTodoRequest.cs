namespace Omega.Core;

public sealed record CreateTodoRequest(string Title, int? ParentId = null);