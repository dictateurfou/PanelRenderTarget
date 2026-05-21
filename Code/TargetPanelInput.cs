using Sandbox;
using Sandbox.UI;
using System.Linq;

namespace PanelRenderTarget;

public class TargetPanelInput
{
	private Panel _hovered;
	private Panel _active;

	private bool _lastMouseDown;
	private Vector2 _lastMousePos;

	private Panel _dragTarget;
	private Panel _dropTarget;

	private bool _dragged;

	private Vector2 _dragStartLocal;
	private Vector2 _dragStartScreen;

	private MousePanelEvent CreateMouseEvent(string type, Panel panel, Vector2 mousePos, string button )
	{
		var panelEvent = new MousePanelEvent( type, panel, button );
		panelEvent.LocalPosition = GetLocalMousePosition( panel, mousePos );
		return panelEvent;
	}


	public void Tick( RootPanel root, Vector2 mousePos, bool mouseDown, Vector2 mouseWheel )
	{
		if ( !root.IsValid() )
			return;

		var mouseDelta = mousePos - _lastMousePos;
		var mouseMoved = !mouseDelta.IsNearZeroLength;
		//root.MousePosition = mousePos;
		var screenRoot = root as TargetRootPanel;
		screenRoot.MousePosition = mousePos;

		var target = FindPanelAt( root, mousePos );

		if ( _dragged )
			UpdateDropTarget( target );
		else
			SetHovered( target );

		_hovered?.CreateEvent(
			CreateMouseEvent( "onmousemove", _hovered, mousePos, "none" )
		);

		if ( _hovered != null && mouseWheel.Length > 0 )
		{
			_hovered.OnMouseWheel( mouseWheel.y * Vector2.Up );
		}

		if ( mouseDown && !_lastMouseDown )
		{
			_active = _hovered;

			if ( _active != null )
			{
				SwitchPseudoClass( PseudoClass.Active, true, _active );

				_dragTarget = FindDragTarget( _active );
				_dragged = false;

				if ( _dragTarget != null )
				{
					_dragStartLocal = GetLocalMousePosition( _dragTarget, mousePos );
					_dragStartScreen = mousePos;
				}

				_active.CreateEvent(
					CreateMouseEvent( "onmousedown", _active, mousePos, "mouseleft" )
				);
			}
		}

		if ( mouseDown && _lastMouseDown && _dragTarget != null && mouseMoved )
		{
			var currentLocal = GetLocalMousePosition( _dragTarget, mousePos );
			var delta = _dragStartLocal - currentLocal;

			if ( !_dragged && delta.Length > 5.0f )
			{
				_dragged = true;
				//wait the next maj with DragEvent support for uncoment
				/*_dragTarget.CreateEvent(
					new DragEvent( "ondragstart", _dragTarget, _dragStartLocal, _dragStartScreen )
				);*/

				if ( _active != null )
				{
					SwitchPseudoClass( PseudoClass.Active, false, _active );
					SwitchPseudoClass( PseudoClass.Hover, false, _active );

					_active.CreateEvent(
						CreateMouseEvent( "onmouseup", _active, mousePos, "mouseleft" )
					);

					_active = null;
				}
			}

			if ( _dragged )
			{
				//wait the next maj with DragEvent support for uncoment
				/*_dragTarget.CreateEvent(
					new DragEvent( "ondrag", _dragTarget, _dragStartLocal, _dragStartScreen )
					{
						MouseDelta = mouseDelta
					}
				);*/
			}
		}

		if ( !mouseDown && _lastMouseDown )
		{
			if ( _dragged && _dragTarget != null )
			{
				//wait the next maj with DragEvent support for uncoment
				/*_dragTarget.CreateEvent(
					new DragEvent( "ondragend", _dragTarget, _dragStartLocal, _dragStartScreen )
				);*/

				if ( _dropTarget != null )
				{
					_dropTarget.CreateEvent(
						new PanelEvent( "ondrop", _dragTarget )
					);
				}

				ClearDropTarget();
				ClearDrag();
			}
			else if ( _active != null )
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
		_lastMousePos = mousePos;
	}

	private void UpdateDropTarget( Panel current )
	{
		if ( current == _dropTarget )
			return;

		_dropTarget?.CreateEvent(
			new PanelEvent( "ondragleave", _dragTarget )
		);

		_dropTarget = current;

		_dropTarget?.CreateEvent(
			new PanelEvent( "ondragenter", _dragTarget )
		);
	}

	private void ClearDropTarget()
	{
		if ( _dropTarget == null )
			return;

		_dropTarget.CreateEvent(
			new PanelEvent( "ondragleave", _dragTarget )
		);

		_dropTarget = null;
	}

	private void ClearDrag()
	{
		_dragTarget = null;
		_dragged = false;
		_dragStartLocal = default;
		_dragStartScreen = default;
	}

	private static Vector2 GetLocalMousePosition( Panel panel, Vector2 mousePos )
	{
		return mousePos - panel.Box.Rect.Position + panel.ScrollOffset;
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
		ClearDropTarget();
		ClearDrag();

		if ( _active != null )
		{
			SwitchPseudoClass( PseudoClass.Active, false, _active );
			_active = null;
		}

		_lastMouseDown = false;
		_lastMousePos = default;
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
		Panel current = null;
		CheckHoverRecursive( root, pos, ref current );
		return current;
	}

	private bool CheckHoverRecursive( Panel panel, Vector2 pos, ref Panel current )
	{
		if ( panel == null )
			return false;

		if ( !panel.IsVisible )
			return false;

		//we cant use that maybe find another way for make panel ignore input
		/*if ( panel.ComputedStyle?.PointerEvents == PointerEvents.None )
			return false;*/

		var inside = panel.Box.Rect.IsInside( pos );
		var found = false;

		if ( inside )
		{
			current = panel;
			found = true;
		}

		
		if ( !inside && PanelClipsChildren( panel ) )
			return found;

		var children = panel.Children?.ToArray();

		if ( children == null || children.Length == 0 )
			return found;

		
		for ( var i = children.Length - 1; i >= 0; i-- )
		{
			var child = children[i];

			if ( CheckHoverRecursive( child, pos, ref current ) )
			{
				found = true;
				break;
			}
		}

		return found;
	}

	private static bool PanelClipsChildren( Panel panel )
	{
		var overflow = panel.ComputedStyle?.Overflow ?? OverflowMode.Visible;
		return overflow != OverflowMode.Visible;
	}
}
