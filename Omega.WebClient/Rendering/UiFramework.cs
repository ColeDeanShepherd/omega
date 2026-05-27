namespace Omega.WebClient.Rendering;

public abstract class UiNode;

public sealed class UiContent(string text) : UiNode
{
	public string Text { get; } = text;
}

public sealed class UiElement(string name) : UiNode
{
	private readonly List<UiNode> _children = [];
	private readonly Dictionary<string, object?> _attributes = [];

	public string Name { get; } = name;
	public IReadOnlyList<UiNode> Children => _children;
	public IReadOnlyDictionary<string, object?> Attributes => _attributes;

	public UiElement Add(UiNode child)
	{
		_children.Add(child);
		return this;
	}

	public UiElement AddText(string text)
	{
		_children.Add(new UiContent(text));
		return this;
	}

	public UiElement Attr(string name, object? value)
	{
		_attributes[name] = value;
		return this;
	}
}

public sealed class UiFragment : UiNode
{
	private readonly List<UiNode> _children = [];

	public IReadOnlyList<UiNode> Children => _children;

	public UiFragment Add(UiNode child)
	{
		_children.Add(child);
		return this;
	}
}

public static class UiBuilder
{
    public static UiFragment Fragment(UiNode[]? children = null)
	{
		UiFragment fragment = new();

		if (children != null)
		{
			foreach (var child in children)
			{
				fragment.Add(child);
			}
		}

		return fragment;
	}

	public static UiElement Element(string name, UiNode[]? children = null)
	{
		UiElement element = new(name);

		if (children != null)
		{
			foreach (var child in children)
			{
				element.Add(child);
			}
		}

		return element;
	}

	public static UiElement H3(UiNode[]? children = null) =>
		Element("h3", children);

	public static UiElement P(UiNode[]? children = null) =>
		Element("p", children);
	
	public static UiContent Text(string text) =>
		new(text);
}
