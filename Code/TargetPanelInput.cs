using Sandbox;
using Sandbox.UI;

namespace PanelRenderTarget;


public class TargetPanelInput
{
	private Panel _hovered;
	private Panel _active;
	private bool _lastMouseDown;

	private Panel _potentialDrag;
	
	public void Tick( RootPanel root, Vector2 mousePos, bool mouseDown, Vector2 mouseWheel )
	{
		if ( !root.IsValid() )
			return;

		var target = FindPanelAt( root, mousePos );

		SetHovered( target );
		_hovered?.CreateEvent(
			new MousePanelEvent( "onmousemove", _hovered, "none" )
		);
		
		if ( _hovered != null && mouseWheel.Length > 0 )
		{
			//var wheelEvent = new MousePanelEvent( "onmousewheel", _hovered, "none" );
			//wheelEvent.Value = mouseWheel;

			//_hovered.CreateEvent( wheelEvent );
			_hovered.OnMouseWheel( mouseWheel.y * Vector2.Up );
			//_hovered.TryScroll
		}

		if ( mouseDown && !_lastMouseDown )
		{
			_active = _hovered;
			
			if ( _active != null )
			{
				_potentialDrag = FindDragTarget( _active );
				SwitchPseudoClass( PseudoClass.Active, true, _active );

				_active.CreateEvent(
					new MousePanelEvent( "onmousedown", _active, "mouseleft" )
				);
			}
		}

		if ( !mouseDown && _lastMouseDown )
		{
			if ( _active != null )
			{
				_active.CreateEvent(
					new MousePanelEvent( "onmouseup", _active, "mouseleft" )
				);

				if ( _active == _hovered )
				{
					_active.CreateEvent(
						new MousePanelEvent( "onclick", _active, "mouseleft" )
					);
				}
				SwitchPseudoClass( PseudoClass.Active, false, _active );
			}

			_active = null;
		}

		_lastMouseDown = mouseDown;
	}

	private Panel FindDragTarget( Panel panel )
	{
		while ( panel != null )
		{
			if ( panel.WantsDrag )
				return panel;

			panel = panel.Parent;
		}

		return null;
	}

	public void Clear()
	{
		SetHovered( null );

		if ( _active != null )
		{
			SwitchPseudoClass( PseudoClass.Active, false, _active );
			_active = null;
		}

		_lastMouseDown = false;
	}

	private void SetHovered( Panel current )
	{
		if ( current == _hovered )
			return;

		if ( _hovered != null )
		{
			SwitchPseudoClass( PseudoClass.Hover, false, _hovered, current );

			_hovered.CreateEvent(
				new MousePanelEvent( "onmouseout", _hovered, "none" )
			);
		}

		_hovered = current;

		if ( _hovered != null )
		{
			if ( _active == null || _active == _hovered )
			{
				SwitchPseudoClass( PseudoClass.Hover, true, _hovered );
			}

			_hovered.CreateEvent(
				new MousePanelEvent( "onmouseover", _hovered, "none" )
			);
		}
	}

	private static void SwitchPseudoClass(
		PseudoClass pseudoClass,
		bool state,
		Panel panel,
		Panel unlessAncestorOf = null
	)
	{
		if ( panel == null )
			return;

		foreach ( var item in panel.AncestorsAndSelf )
		{
			if ( unlessAncestorOf == null || !unlessAncestorOf.IsAncestor( item ) )
			{
				item.Switch( pseudoClass, state );
			}
		}
	}

	private Panel FindPanelAt( Panel root, Vector2 pos )
	{
		Panel best = null;

		if ( root.Box.Rect.IsInside( pos ) )
		{
			best = root;
		}

		foreach ( var panel in root.Descendants )
		{
			if ( !panel.IsVisible )
				continue;

			if ( !panel.Box.Rect.IsInside( pos ) )
				continue;

			best = panel;
		}

		return best;
	}
}

