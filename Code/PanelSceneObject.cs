using Sandbox;

namespace PanelRenderTarget;

//try to make panel is not visible when doesnt need update maybe that optimise the Ui rendering
public class PanelSceneObject : SceneCustomObject
{
	private readonly TargetRootPanel _panel;
	private readonly Texture _texture;
	private RenderTarget _renderTarget;

	private const float MaxFps = 60;
	private const float MinInterval = 1f / MaxFps;
	private float _timeSinceLastRender = 0f;

	public Vector2 CursorPosition { get; set; }
	public bool CursorVisible { get; set; } = true;
	public string CursorIcon { get; set; } = "arrow_selector_tool";

	public readonly TargetScreen Screen;

	public PanelSceneObject(
		BBox bound,
		SceneWorld sceneWorld,
		TargetRootPanel panel,
		Texture texture,
		TargetScreen screen
	) : base( sceneWorld )
	{
		_panel = panel;
		_texture = texture;
		_renderTarget = RenderTarget.From( _texture );
		Bounds = bound;
		Screen = screen;
		Flags.IsOpaque = true;
		Flags.IsTranslucent = false;
		Flags.WantsPrePass = false;
		//this.GetGameObject
	}

	public override void RenderSceneObject()
	{
		base.RenderSceneObject();
		//Log.Info( "RenderSceneObject" );
		if ( !_texture.IsLoaded || !_panel.IsValid() )
			return;

		_timeSinceLastRender += Time.Delta;


		if( !CursorVisible )
			if (_timeSinceLastRender < MinInterval )
				return;

		_timeSinceLastRender = 0f;

		var bounds = new Rect( 0, 0, _texture.Width, _texture.Height );

		_panel.FixedBounds = bounds;
		_panel.PanelBounds = bounds;
		_panel.FixedScale = 1f;

		var oldTarget = Graphics.RenderTarget;
		var oldViewport = Graphics.Viewport;

		Graphics.RenderTarget = _renderTarget;
		Graphics.Viewport = bounds;

		Graphics.Attributes.SetCombo( "D_WORLDPANEL", 0 );
		//_panel.MarkRenderDirty();
		
		_panel.RenderManual();

		if ( CursorVisible && Screen.ShowVirtualCursor )
		{
			var cursorRect = new Rect(
				CursorPosition.x - 8,
				CursorPosition.y - 8,
				0,
				0
			);
			Graphics.DrawText( cursorRect, ".", Color.Red, "Roboto", 32f );
		}

		Graphics.GenerateMipMaps( _texture );

		Graphics.Viewport = oldViewport;
		Graphics.RenderTarget = oldTarget;
	}
}
