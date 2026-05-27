using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Omega.WebClient.Rendering;

public static class BlazorUiNodeRenderer
{
	public static void Render(RenderTreeBuilder builder, UiNode node)
	{
		var sequence = 0;
		RenderNode(builder, node, ref sequence);
	}

	private static void RenderNode(RenderTreeBuilder builder, UiNode node, ref int sequence)
	{
		switch (node)
		{
			case UiContent content:
				builder.AddContent(sequence++, content.Text);
				break;
			case UiElement element:
				builder.OpenElement(sequence++, element.Name);

				foreach (var attribute in element.Attributes)
				{
					builder.AddAttribute(sequence++, attribute.Key, attribute.Value);
				}

				foreach (var child in element.Children)
				{
					RenderNode(builder, child, ref sequence);
				}

				builder.CloseElement();
				break;
			case UiFragment fragment:
				foreach (var child in fragment.Children)
				{
					RenderNode(builder, child, ref sequence);
				}
				break;
			default:
				throw new InvalidOperationException($"Unsupported node type '{node.GetType().Name}'.");
		}
	}
}

public abstract class BlazorUiComponentBase : ComponentBase
{
	public abstract UiNode ViewFn();

	protected override void BuildRenderTree(RenderTreeBuilder builder) =>
		BlazorUiNodeRenderer.Render(builder, ViewFn());
}
