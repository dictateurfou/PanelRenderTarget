using Sandbox;
using Sandbox.Internal;
using Sandbox.UI;
using System.Collections.Generic;
namespace PanelRenderTarget;

public class TargetPanelSystem : GameObjectSystem<TargetPanelSystem>
{

	public TargetPanelSystem(Scene scene) : base(scene)
	{
		Listen(Stage.FinishUpdate,1,UpdateAll, "TargetPanelSystemUpdateAll" );
	}

	protected HashSet<TargetScreen> Screens = new();

	public void RegisterScreen( TargetScreen screen )
	{
		Screens.Add(screen);
	}

	public void UnregisterScreen( TargetScreen screen )
	{
		Screens.Remove(screen);
	}

	void UpdateAll()
	{
		if ( Application.IsDedicatedServer ) return;

		var position = Scene.Camera.WorldPosition;
		var frustrum = Scene.Camera.GetFrustum();
		var camera = Scene.Camera;

		foreach ( var screen in Screens )
		{
			bool visible = frustrum.IsInside( screen.Renderer.Bounds, partially: true );
			var panel = screen.RootPanel;
			if (!visible || screen.WorldPosition.Distance( position ) > 5000f )
			{
				screen.PanelObject.RenderingEnabled = false;
				if( panel.Style.Display != DisplayMode.None )
					panel.Style.Display = DisplayMode.None;
				continue;
			}


			var worldBounds = screen.Renderer.Bounds;
			var screenCenter = new Vector2(
				camera.ScreenRect.Size.x * 0.5f,
				camera.ScreenRect.Size.y * 0.5f
			);
			var ray = camera.ScreenPixelToRay( screenCenter );
			var touch = worldBounds.Trace( ray, screen.TraceDistance, out _ );

			if ( screen.UpdateWhenNotFocused == false && !touch && screen.PanelObject.InitPassed )
			{
				screen.PanelObject.RenderingEnabled = false;
				if ( panel.Style.Display != DisplayMode.None )
					panel.Style.Display = DisplayMode.None;
				continue;
			}

			screen.PanelObject.RenderingEnabled = true;
			if( panel.Style.Display != DisplayMode.Contents )
				panel.Style.Display = DisplayMode.Contents;

			//only execute the tick when aim the model because tick is for input only
			if ( touch )
			{
				screen.Tick();
			}
		}
	}


	public void CreatePanelScreen( GameObject go, string screenMaterialName, Material material, Vector2Int size )
	{
		var comp = go.AddComponent<TargetScreen>( false );
		comp.ScreenMaterialName = screenMaterialName;
		comp.ScreenMaterial = material;
		comp.ScreenTextureSize = size;
		comp.Enabled = true;
	}
}
