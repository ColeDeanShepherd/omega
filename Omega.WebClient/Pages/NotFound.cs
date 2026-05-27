using Microsoft.AspNetCore.Components;
using Omega.WebClient.Layout;
using Omega.WebClient.Rendering;

using _ = Omega.WebClient.Rendering.UiBuilder;

namespace Omega.WebClient.Pages;

[Route("/not-found")]
[Layout(typeof(MainLayout))]
public sealed class NotFound : BlazorUiComponentBase
{
	public NotFound() : base(ViewFn) { }

	public static UiNode ViewFn() =>
		_.Fragment([
			_.H3([ _.Text("Not Found") ]),
			_.P([ _.Text("Sorry, the content you are looking for does not exist.") ])
		]);
}
