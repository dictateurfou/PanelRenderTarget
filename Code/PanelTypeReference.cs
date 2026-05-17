using Sandbox;
using System.Linq;

namespace PanelRenderTarget;

//class used for autoreflection of panel
public sealed class PanelTypeReference
{
	public string TypeName { get; set; }

	public TypeDescription Resolve()
	{
		if ( string.IsNullOrWhiteSpace( TypeName ) )
			return null;

		return Game.TypeLibrary?.GetType( TypeName )
			?? Game.TypeLibrary?.GetTypes()
				.FirstOrDefault( x => x.TargetType?.FullName == TypeName );
	}
}
