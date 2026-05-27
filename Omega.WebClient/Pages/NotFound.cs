using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Omega.WebClient.Layout;
using Omega.WebClient.Rendering;

using _ = Omega.WebClient.Rendering.UiBuilder;

namespace Omega.WebClient.Pages;

[Route("/not-found")]
[Layout(typeof(MainLayout))]
public sealed class NotFound : ComponentBase
{
	protected override void BuildRenderTree(RenderTreeBuilder builder)
	{
		var page = _.Fragment([
			_.H3([ _.Text("Not Found") ]),
			_.P([ _.Text("Sorry, the content you are looking for does not exist.") ])
		]);

		UiNodeRenderer.Render(builder, page);
	}
}
