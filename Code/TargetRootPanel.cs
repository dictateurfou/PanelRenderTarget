using Sandbox;
using Sandbox.UI;

namespace PanelRenderTarget;

public class TargetRootPanel : RootPanel
{
	public Rect FixedBounds { get; set; } = new Rect( 0, 0, 1280, 720 );
	public float FixedScale { get; set; } = 1f;

	public new Vector2 MousePosition { get; set; }

	protected override void UpdateBounds( Rect rect )
	{
		Mouse.Visibility = MouseVisibility.Visible;
		PanelBounds = FixedBounds;
		//this.Mouse
	}

	protected override void UpdateScale( Rect screenSize )
	{
		Scale = FixedScale;
	}

	public void Test()
	{
		
	}
}
