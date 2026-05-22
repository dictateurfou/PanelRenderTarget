using Sandbox;
using Sandbox.UI;

namespace PanelRenderTarget;

public class TargetRootPanel : RootPanel
{
	public Rect FixedBounds { get; set; } = new Rect( 0, 0, 1280, 720 );
	public float FixedScale { get; set; } = 1f;

	public new Vector2 MousePosition { get; set; }

	public MouseVisibility MouseVisibility { get; set; } = MouseVisibility.Hidden;

	public override void Tick()
	{
		base.Tick();
		//set visibility only if we want

		if( MouseVisibility == MouseVisibility.Visible && Mouse.Visibility != MouseVisibility.Visible )
		{
			Mouse.Visibility = MouseVisibility.Visible;
		}
	}

	protected override void UpdateBounds( Rect rect )
	{
		
		PanelBounds = FixedBounds;
		//this.Mouse
	}

	protected override void UpdateScale( Rect screenSize )
	{
		Scale = FixedScale;
	}


	public override void OnDeleted()
	{
		base.OnDeleted();
		if( MouseVisibility == MouseVisibility.Visible )
		{
			Mouse.Visibility = MouseVisibility.Hidden;
		}
	}
}
