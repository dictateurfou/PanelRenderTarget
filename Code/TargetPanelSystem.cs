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
			if (!visible || screen.WorldPosition.Distance( position ) > screen.UpdateDistanceMax )
			{
				screen.PanelObject.RenderingEnabled = false;
				if( panel.Style.Display != DisplayMode.None )
					panel.Style.Display = DisplayMode.None;
				continue;
			}

			screen.PanelObject.RenderingEnabled = true;
			if( panel.Style.Display != DisplayMode.Contents )
				panel.Style.Display = DisplayMode.Contents;


			var worldBounds = screen.Renderer.Bounds;
			var screenCenter = new Vector2(
				camera.ScreenRect.Size.x * 0.5f,
				camera.ScreenRect.Size.y * 0.5f
			);

			//only execute the tick when aim the model because tick is for input only
			var ray = camera.ScreenPixelToRay( screenCenter );
			if ( worldBounds.Trace( ray, screen.TraceDistance, out _ ) )
				screen.Tick();
			
			
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
