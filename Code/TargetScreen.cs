using PanelRenderTarget;
using Sandbox;
using Sandbox.UI;
using System;
using System.Linq;

public class TargetScreen : Component, Component.DontExecuteOnServer, ITargetScreen
{
	[Property, Feature( "interaction" ), Description( "Enable verbose logs for screen mouse detection" )]
	public bool DebugMouseTrace { get; set; } = false;

	[Property] public string ScreenMaterialName { get; set; } = "screen-01";
	[Property] public Material ScreenMaterial { get; set; } = Material.Load( "materials/screen.vmat" );
	[Property] public Vector2Int ScreenTextureSize { get; set; } = new( 1280, 720 );
	[Property] public float TraceDistance { get; set; } = 200f;
	[Property] public bool ForceUpdate { get; set; } = false;
	[Property] public PanelTypeReference PanelType { get; set; } = new();
	[Property, Feature( "interaction" ), Description( "Small UV offset applied after triangle interpolation to correct slight drift" )]
	public Vector2 ScreenUvOffset { get; set; } = Vector2.Zero;

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
	private Vertex[] _cachedVertices;
	private uint[] _cachedIndices;
	private Model _cachedMeshModel;
	private TriangleMaterialRange[] _cachedTriangleMaterialRanges = Array.Empty<TriangleMaterialRange>();
	private double _tracePerfAccumMs;
	private double _tracePerfMaxMs;
	private double _tracePerfCacheAccumMs;
	private double _tracePerfTrianglesAccumMs;
	private int _tracePerfSamples;
	private TimeSince _tracePerfLogTimer;

	private readonly struct TriangleMaterialRange
	{
		public int StartTriangle { get; init; }
		public int EndTriangleExclusive { get; init; }
		public Material Material { get; init; }
	}

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

		RefreshMeshCache();
		CreateTexture();
		CreatePanel();
		SetupMaterial();
		CreatePanelObject();

		OnPanelCreated( Panel );

		var panelComponent = Components.Get<PanelComponent>();
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

		return TryGetPanelPositionFromMeshRaycast( ray, out panelPos );
	}

	private bool TryGetPanelPositionFromMeshRaycast( Ray ray, out Vector2 panelPos )
	{
		using var scope = Sandbox.Diagnostics.Performance.Scope( "TargetScreen.MouseTrace" );
		var totalTimer = Sandbox.Diagnostics.FastTimer.StartNew();
		panelPos = default;

		var cacheTimer = Sandbox.Diagnostics.FastTimer.StartNew();
		RefreshMeshCache();
		var cacheMs = cacheTimer.ElapsedMilliSeconds;

		if ( _cachedVertices is null || _cachedIndices is null || _cachedIndices.Length < 3 )
		{
			if ( DebugMouseTrace )
				Log.Info( $"[TargetScreen] Reject mesh cache: vertices={_cachedVertices?.Length ?? 0} indices={_cachedIndices?.Length ?? 0}" );
			UpdateDebugTracePerf( totalTimer.ElapsedMilliSeconds, cacheMs, 0f );
			return false;
		}

		var localOrigin = Renderer.Transform.World.PointToLocal( ray.Position );
		var localEnd = Renderer.Transform.World.PointToLocal( ray.Position + ray.Forward * TraceDistance );
		var localDirection = (localEnd - localOrigin).Normal;

		var bestDistance = float.MaxValue;
		var bestUv = Vector2.Zero;
		var bestTriangle = -1;
		var triangleTimer = Sandbox.Diagnostics.FastTimer.StartNew();

		for ( int triStart = 0; triStart + 2 < _cachedIndices.Length; triStart += 3 )
		{
			var triangleIndex = triStart / 3;
			if ( !IsTriangleMaterialMatch( triangleIndex ) )
				continue;

			var i0 = _cachedIndices[triStart + 0];
			var i1 = _cachedIndices[triStart + 1];
			var i2 = _cachedIndices[triStart + 2];
			if ( i0 >= _cachedVertices.Length || i1 >= _cachedVertices.Length || i2 >= _cachedVertices.Length )
				continue;

			var v0 = _cachedVertices[i0];
			var v1 = _cachedVertices[i1];
			var v2 = _cachedVertices[i2];

			if ( !TryRayTriangleIntersection( localOrigin, localDirection, v0.Position, v1.Position, v2.Position, out var distance, out var barycentric ) )
				continue;

			if ( distance > TraceDistance || distance >= bestDistance )
				continue;

			var triangleUv =
				v0.TexCoord0 * barycentric.x +
				v1.TexCoord0 * barycentric.y +
				v2.TexCoord0 * barycentric.z;

			bestDistance = distance;
			bestUv = triangleUv;
			bestTriangle = triangleIndex;
		}

		var triangleMs = triangleTimer.ElapsedMilliSeconds;

		if ( bestTriangle < 0 )
		{
			if ( DebugMouseTrace )
				Log.Info( $"[TargetScreen] No triangle hit. vertices={_cachedVertices.Length} indices={_cachedIndices.Length}" );
			UpdateDebugTracePerf( totalTimer.ElapsedMilliSeconds, cacheMs, triangleMs );
			return false;
		}

		if ( DebugMouseTrace )
			Log.Info( $"[TargetScreen] Mesh hit triangle={bestTriangle} distance={bestDistance} rawUv={bestUv}" );

		var uv = new Vector2(
			bestUv.x - MathF.Floor( bestUv.x ),
			bestUv.y - MathF.Floor( bestUv.y )
		);

		uv += ScreenUvOffset;

		panelPos = new Vector2(
			Math.Clamp( uv.x, 0f, 1f ) * ScreenTextureSize.x,
			Math.Clamp( uv.y, 0f, 1f ) * ScreenTextureSize.y
		);

		if ( DebugMouseTrace )
			Log.Info( $"[TargetScreen] Final UV={uv} panelPos={panelPos}" );

		UpdateDebugTracePerf( totalTimer.ElapsedMilliSeconds, cacheMs, triangleMs );
		return true;
	}

	private void UpdateDebugTracePerf( double totalMs, double cacheMs, double triangleMs )
	{
		_tracePerfAccumMs += totalMs;
		_tracePerfCacheAccumMs += cacheMs;
		_tracePerfTrianglesAccumMs += triangleMs;
		_tracePerfMaxMs = Math.Max( _tracePerfMaxMs, totalMs );
		_tracePerfSamples++;

		if ( _tracePerfLogTimer < 5f )
			return;

		var avgTotalMs = _tracePerfSamples > 0 ? _tracePerfAccumMs / _tracePerfSamples : 0d;
		var avgCacheMs = _tracePerfSamples > 0 ? _tracePerfCacheAccumMs / _tracePerfSamples : 0d;
		var avgTrianglesMs = _tracePerfSamples > 0 ? _tracePerfTrianglesAccumMs / _tracePerfSamples : 0d;
		var callsPerSecond = _tracePerfLogTimer > 0f ? _tracePerfSamples / _tracePerfLogTimer : 0f;
		var triangleCount = _cachedIndices?.Length / 3 ?? 0;

		Log.Info( $"[TargetScreen] Trace perf avg={avgTotalMs:F3}ms max={_tracePerfMaxMs:F3}ms cache={avgCacheMs:F3}ms triangles={avgTrianglesMs:F3}ms calls={callsPerSecond:F1}/s tris={triangleCount}" );

		_tracePerfAccumMs = 0d;
		_tracePerfCacheAccumMs = 0d;
		_tracePerfTrianglesAccumMs = 0d;
		_tracePerfMaxMs = 0d;
		_tracePerfSamples = 0;
		_tracePerfLogTimer = 0f;
	}

	private void RefreshMeshCache()
	{
		var model = Renderer?.Model;
		if ( model is null || model == _cachedMeshModel )
			return;

		_cachedMeshModel = model;
		_cachedVertices = model.GetVertices();
		_cachedIndices = model.GetIndices();
		_cachedTriangleMaterialRanges = BuildTriangleMaterialRanges( model );

		if ( DebugMouseTrace )
			Log.Info( $"[TargetScreen] Refreshed mesh cache for {model.Name}: vertices={_cachedVertices?.Length ?? 0} indices={_cachedIndices?.Length ?? 0} materialRanges={_cachedTriangleMaterialRanges.Length}" );
	}

	private TriangleMaterialRange[] BuildTriangleMaterialRanges( Model model )
	{
		var meshInfo = model.MeshInfo;
		if ( meshInfo?.Meshes is null )
			return Array.Empty<TriangleMaterialRange>();

		var ranges = new System.Collections.Generic.List<TriangleMaterialRange>();
		var triangleCursor = 0;

		foreach ( var mesh in meshInfo.Meshes )
		{
			if ( mesh?.DrawCalls is null )
				continue;

			foreach ( var drawCall in mesh.DrawCalls )
			{
				var triangleCount = Math.Max( 0, drawCall.Indices / 3 );
				if ( triangleCount == 0 )
					continue;

				ranges.Add( new TriangleMaterialRange
				{
					StartTriangle = triangleCursor,
					EndTriangleExclusive = triangleCursor + triangleCount,
					Material = drawCall.Material
				} );

				if ( DebugMouseTrace )
					Log.Info( $"[TargetScreen] Material range {triangleCursor}->{triangleCursor + triangleCount} material={drawCall.Material?.Name}" );

				triangleCursor += triangleCount;
			}
		}

		return ranges.ToArray();
	}

	private bool IsTriangleMaterialMatch( int triangleIndex )
	{
		if ( string.IsNullOrWhiteSpace( ScreenMaterialName ) || _cachedTriangleMaterialRanges.Length == 0 )
			return true;

		foreach ( var range in _cachedTriangleMaterialRanges )
		{
			if ( triangleIndex < range.StartTriangle || triangleIndex >= range.EndTriangleExclusive )
				continue;

			var match = range.Material?.Name?.Contains( ScreenMaterialName, StringComparison.OrdinalIgnoreCase ) == true;
			if ( DebugMouseTrace && triangleIndex == range.StartTriangle )
				Log.Info( $"[TargetScreen] Triangle {triangleIndex} material={range.Material?.Name} match={match}" );
			return match;
		}

		return true;
	}

	private static bool TryRayTriangleIntersection( Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float distance, out Vector3 barycentric )
	{
		distance = 0f;
		barycentric = default;

		const float epsilon = 0.0001f;
		var edge1 = b - a;
		var edge2 = c - a;
		var pvec = Vector3.Cross( direction, edge2 );
		var det = Vector3.Dot( edge1, pvec );
		if ( MathF.Abs( det ) < epsilon )
			return false;

		var invDet = 1f / det;
		var tvec = origin - a;
		var v = Vector3.Dot( tvec, pvec ) * invDet;
		if ( v < 0f || v > 1f )
			return false;

		var qvec = Vector3.Cross( tvec, edge1 );
		var w = Vector3.Dot( direction, qvec ) * invDet;
		if ( w < 0f || v + w > 1f )
			return false;

		distance = Vector3.Dot( edge2, qvec ) * invDet;
		if ( distance < 0f )
			return false;

		barycentric = new Vector3( 1f - v - w, v, w );
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
