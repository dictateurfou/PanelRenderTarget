using PanelRenderTarget;
using Sandbox;
using Sandbox.UI;
using System;
using System.Linq;

public class TargetScreen : Component, Component.DontExecuteOnServer, ITargetScreen
{
	[Property] public string ScreenMaterialName { get; set; } = "screen-01";
	[Property] public Material ScreenMaterial { get; set; } = Material.Load( "materials/screen.vmat" );
	[Property] public Vector2Int ScreenTextureSize { get; set; } = new( 1280, 720 );
	[Property] public float TraceDistance { get; set; } = 200f;
	[Property] public bool ForceUpdate { get; set; } = false;
	[Property] public PanelTypeReference PanelType { get; set; } = new();

	[Property, Feature("interaction"), Description("interact with 2d mouse cursor")]
	public bool ScreenCursorInteraction { get; set; } = false;

	[Property,Feature("interaction"), Description("whether to show the virtual cursor") ]
	public bool ShowVirtualCursor { get; set; } = true;

	[Property, Feature( "Optimisation" ), Description("fps when the panel is focused") ]
	public int UpdateRateFocus { get; set; } = 60;

	[Property, Feature("Optimisation")]
	public bool UpdateWhenNotFocused { get; set; } = false;

	[Property, Feature( "Optimisation" ), Description( "fps update rate when the panel is visible in camera" ) ]
	public int UpdateRateNotFocused { get; set; } = 30;

	[Property, Feature( "Optimisation" ), Description("the distance of the update rate visible but not focus") ]
	public int UpdateDistanceMax { get; set; } = 500;


	private bool _firstUpdate = false;



	private readonly TargetPanelInput _input = new();

	public ModelRenderer Renderer { get; private set; }
	private Material _screenMaterialCopy;
	private Texture _screenTexture;
	private TargetRootPanel _rootPanel;
	private PanelSceneObject _panelObject;

	protected Panel Panel { get; private set; }
	public TargetRootPanel RootPanel => _rootPanel;
	public PanelSceneObject PanelObject => _panelObject;
	protected Texture ScreenTexture => _screenTexture;

	protected override void OnPreRender()
	{
		//ensure sceneobject have transform and correct bound for engine culling
		_panelObject.Transform = Renderer.Transform.World;
		_panelObject.Bounds = Renderer.Bounds;
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		Renderer = Components.Get<ModelRenderer>();

		if ( !Renderer.IsValid() )
		{
			Log.Warning( "No ModelRenderer found." );
			Enabled = false;
			return;
		}

		CreateTexture();
		CreatePanel();
		SetupMaterial();
		CreatePanelObject();

		OnPanelCreated( Panel );

		var panelComponent = this.Components.Get<PanelComponent>();
		TargetPanelSystem.Current.RegisterScreen( this );
	}

	public Panel GetPanel()
	{
		return Panel;
	}

	protected virtual void OnPanelCreated( Panel panel )
	{
	}

	private void CreateTexture()
	{
		_screenTexture = Texture.CreateRenderTarget()
			.WithSize( ScreenTextureSize.x, ScreenTextureSize.y )
			.WithInitialColor( Color.Black )
			.WithMips()
			.Create();
	}

	private void CreatePanel()
	{
		var bounds = new Rect( 0, 0, ScreenTextureSize.x, ScreenTextureSize.y );

		_rootPanel = new TargetRootPanel
		{
			RenderedManually = true,
			FixedBounds = bounds,
			FixedScale = 1f,
			PanelBounds = bounds,
			MouseVisibility = ScreenCursorInteraction ? MouseVisibility.Visible : MouseVisibility.Hidden
		};

		_rootPanel.Style.Width = Length.Pixels( ScreenTextureSize.x );
		_rootPanel.Style.Height = Length.Pixels( ScreenTextureSize.y );

		var type = PanelType?.Resolve();

		if ( type?.TargetType is null || !typeof( Panel ).IsAssignableFrom( type.TargetType ) )
		{
			Log.Warning( $"Invalid panel type: {PanelType?.TypeName}" );
			return;
		}

		Panel = type.Create<Panel>();
		_rootPanel.AddChild( Panel );

		Panel.Style.Width = Length.Percent( 100 );
		Panel.Style.Height = Length.Percent( 100 );
	}

	private void CreatePanelObject()
	{
		_panelObject = new PanelSceneObject(
			GameObject.GetBounds(),
			Scene.SceneWorld,
			_rootPanel,
			_screenTexture,
			this
		);
	}

	public void Tick()
	{
		if ( !Renderer.IsValid() || Renderer.Model is null || !Panel.IsValid() )
			return;

		var camera = Scene.Camera;

		if ( camera is null )
			return;

		if ( !TryGetPanelPosition( camera, out var panelPos ) )
		{
			ClearInput();
			return;
		}

		_panelObject.CursorVisible = true;
		_panelObject.CursorPosition = panelPos;
		
		_input.Tick(
			_rootPanel,
			panelPos,
			Input.Down( "attack1" ),
			Input.MouseWheel
		);

	}


	private bool TryGetPanelPosition( CameraComponent camera, out Vector2 panelPos )
	{
		panelPos = default;

		var screenCenter = new Vector2(
			camera.ScreenRect.Size.x * 0.5f,
			camera.ScreenRect.Size.y * 0.5f
		);

		var ray = camera.ScreenPixelToRay( screenCenter );

		if( ScreenCursorInteraction )
		{
			ray = camera.ScreenPixelToRay( Mouse.Position );
		}

		var localStart = Renderer.Transform.World.PointToLocal( ray.Position );
		var localEnd = Renderer.Transform.World.PointToLocal(
			ray.Position + ray.Forward * TraceDistance
		);

		var result = Renderer.Model.Trace
			.Ray( localStart, localEnd )
			.Run();

		if ( !result.Hit )
			return false;

		if ( result.Material is null || !result.Material.Name.Contains( ScreenMaterialName ) )
			return false;

		var influence = result.VertexInfluence;

		var uv =
			result.Vertex0.Uv0 * influence.x +
			result.Vertex1.Uv0 * influence.y +
			result.Vertex2.Uv0 * influence.z;

		uv = new Vector2(
			uv.x - MathF.Floor( uv.x ),
			uv.y - MathF.Floor( uv.y )
		);

		panelPos = new Vector2(
			uv.x * ScreenTextureSize.x,
			uv.y * ScreenTextureSize.y
		);

		return true;
	}

	private void SetupMaterial()
	{
		var oldMaterial = Renderer.Model.Materials
			.FirstOrDefault( x => x.Name.Contains( ScreenMaterialName ) );

		var index = Renderer.Model.Materials.IndexOf( oldMaterial );

		if ( index < 0 )
		{
			Log.Warning( $"Screen material not found: {ScreenMaterialName}" );
			return;
		}

		_screenMaterialCopy = ScreenMaterial.CreateCopy();
		_screenMaterialCopy.Set( "g_tColor", _screenTexture );

		Renderer.Materials.SetOverride( index, _screenMaterialCopy );
	}

	private void ClearInput()
	{
		_input.Clear();

		if ( _panelObject is not null )
			_panelObject.CursorVisible = false;
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		ClearInput();

		_panelObject?.Delete();
		_panelObject = null;

		_rootPanel?.Delete( true );
		_rootPanel = null;

		_screenTexture?.Dispose();
		_screenTexture = null;

		_screenMaterialCopy = null;
		Panel = null;
		Renderer = null;

		TargetPanelSystem.Current.UnregisterScreen( this );
	}
}
