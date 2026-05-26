using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Omega.WebClient.Layout;

namespace Omega.WebClient.Pages;

[Route("/not-found")]
[Layout(typeof(MainLayout))]
public sealed class NotFound : ComponentBase
{
	private UiFragment _fragment(UiNode[]? children = null)
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

	private UiElement _element(string name, UiNode[]? children = null)
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

	private UiElement _h3(UiNode[]? children = null) =>
		_element("h3", children);

	private UiElement _p(UiNode[]? children = null) =>
		_element("p", children);
	
	private UiContent _text(string text) =>
		new(text);

	protected override void BuildRenderTree(RenderTreeBuilder builder)
	{
		var sequence = 0;
		var page = _fragment([
			_h3([ _text("Not Found") ]),
			_p([ _text("Sorry, the content you are looking for does not exist.") ])
		]);

		page.Render(builder, ref sequence);
	}
}

public abstract class UiNode
{
	internal abstract void Render(RenderTreeBuilder builder, ref int sequence);
}

public sealed class UiContent(string text) : UiNode
{
	public string Text { get; } = text;

	internal override void Render(RenderTreeBuilder builder, ref int sequence)
	{
		builder.AddContent(sequence++, Text);
	}
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

	internal override void Render(RenderTreeBuilder builder, ref int sequence)
	{
		builder.OpenElement(sequence++, Name);

		foreach (var attribute in _attributes)
		{
			builder.AddAttribute(sequence++, attribute.Key, attribute.Value);
		}

		foreach (var child in _children)
		{
			child.Render(builder, ref sequence);
		}

		builder.CloseElement();
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

	internal override void Render(RenderTreeBuilder builder, ref int sequence)
	{
		foreach (var child in _children)
		{
			child.Render(builder, ref sequence);
		}
	}
}
