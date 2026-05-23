using Sandbox;
using Sandbox.UI;
using System.Linq;

namespace PanelRenderTarget;

public class TargetPanelInput
{
	private Panel _hovered;
	private Panel _active;
	private bool _lastMouseDown;

	private Panel _potentialDrag;

	private MousePanelEvent CreateMouseEvent( string type, Panel panel, Vector2 mousePos, string button )
	{
		var panelEvent = new MousePanelEvent( type, panel, button );
		panelEvent.LocalPosition = mousePos - panel.Box.Rect.Position + panel.ScrollOffset;
		return panelEvent;
	}

	public void Tick( RootPanel root, Vector2 mousePos, bool mouseDown, Vector2 mouseWheel )
	{
		if ( !root.IsValid() )
			return;

		var target = FindPanelAt( root, mousePos );
		var screenRoot = root as TargetRootPanel;
		screenRoot.MousePosition = mousePos;
		
		SetHovered( target );
		if ( _hovered != null )
		{
			_hovered.CreateEvent(
				CreateMouseEvent( "onmousemove", _hovered, mousePos, "none" )
			);
		}

		if ( _hovered != null && mouseWheel.Length > 0 )
		{
			_hovered.OnMouseWheel( mouseWheel.y * Vector2.Up );
		}

		if ( mouseDown && !_lastMouseDown )
		{
			_active = _hovered;

			if ( _active != null )
			{
				_potentialDrag = FindDragTarget( _active );
				SwitchPseudoClass( PseudoClass.Active, true, _active );

				_active.CreateEvent(
					CreateMouseEvent( "onmousedown", _active, mousePos, "mouseleft" )
				);
			}
		}
		if ( !mouseDown && _lastMouseDown )
		{
			if ( _active != null )
			{
				_active.CreateEvent(
					CreateMouseEvent( "onmouseup", _active, mousePos, "mouseleft" )
				);

				if ( _active == _hovered )
				{
					_active.CreateEvent(
						CreateMouseEvent( "onclick", _active, mousePos, "mouseleft" )
					);

					if (_active.AcceptsFocus)
					{
						_active.Focus();
					}
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
		if ( root == null || !root.IsValid() || !root.IsVisible )
			return null;

		if ( !root.Box.Rect.IsInside( pos ) )
			return null;

		// Trier les enfants par z-index décroissant pour tester le plus haut en premier
		var children = root.Children?
			.Where( x => x != null && x.IsValid() && x.IsVisible )
			.OrderByDescending( x => x.ComputedStyle?.ZIndex ?? 0 )
			.ThenByDescending( x => x.SiblingIndex );

		if ( children != null )
		{
			foreach ( var child in children )
			{
				var hit = FindPanelAt( child, pos );
				if ( hit != null )
					return hit;
			}
		}

		return root;
	}
}
