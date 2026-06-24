using Sandbox;

namespace PanelRenderTarget;

// Stores a panel type by name so scene serialization remains stable.
public sealed class PanelTypeReference
{
	public string TypeName { get; set; }

	public TypeDescription Resolve()
	{
		if ( string.IsNullOrWhiteSpace( TypeName ) )
			return null;

		var typeLibrary = Game.TypeLibrary;
		if ( typeLibrary is null )
			return null;

		return typeLibrary.GetType( typeof( PanelRenderTarget.ScreenPanel ), TypeName, true, true );
	}
}
