using Sandbox.UI;
using Sandbox;

using System.Collections.Generic;
using System.Linq;
using Editor;

namespace PanelRenderTarget;

[CustomEditor( typeof( PanelTypeReference ) )]
public class PanelTypeReferenceEditor : ControlObjectWidget
{
    public override bool SupportsMultiEdit => false;

    private readonly ComboBox _typeSelector;
    private readonly List<TypeDescription> _panelTypes;

    public PanelTypeReferenceEditor( SerializedProperty property ) : base( property, false )
    {
        Layout = Layout.Column();
        Layout.Spacing = 4;

        _panelTypes = EditorTypeLibrary.GetTypes<ScreenPanel>()
            .Where( x => !x.IsAbstract && x.TargetType != null )
            .OrderBy( x => x.Title ?? x.Name )
            .ToList();

        var row = Layout.AddRow();
        row.Add( new Editor.Label( "Panel Type" ) { MinimumWidth = 120 } );
        _typeSelector = row.Add( new ComboBox( this ), 1 );

        _typeSelector.AddItem( "(Aucun)" );

        for ( int i = 0; i < _panelTypes.Count; i++ )
        {
            _typeSelector.AddItem( _panelTypes[i].Title ?? _panelTypes[i].Name );
        }

        var current = SerializedProperty.GetValue<PanelTypeReference>();
        var currentTypeName = current?.TypeName;

        if ( string.IsNullOrWhiteSpace( currentTypeName ) )
        {
            _typeSelector.CurrentIndex = 0;
        }
        else
        {
            var idx = _panelTypes.FindIndex( x =>
                x.TargetType?.FullName == currentTypeName ||
                x.ClassName == currentTypeName ||
                x.Name == currentTypeName );

            _typeSelector.CurrentIndex = idx >= 0 ? idx + 1 : 0;
        }

        _typeSelector.ItemChanged += OnTypeChanged;
    }

    private void OnTypeChanged()
    {
        if ( _typeSelector.CurrentIndex <= 0 )
        {
            SerializedProperty.SetValue( new PanelTypeReference() );
            return;
        }

        var selected = _panelTypes[_typeSelector.CurrentIndex - 1];

        SerializedProperty.SetValue( new PanelTypeReference
        {
            TypeName = selected.TargetType.FullName
        } );
    }
}
