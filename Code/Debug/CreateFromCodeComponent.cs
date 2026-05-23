using System;
using System.Collections.Generic;
using System.Text;
using Sandbox;

namespace PanelRenderTarget;

public class CreateFromCodeRtPanelComponent : Component, Component.DontExecuteOnServer
{

	protected override void OnEnabled()
	{
		base.OnEnabled();
		
		TargetPanelSystem.Current.CreatePanelScreen<Example>(GameObject, "flatscreen_tv_display", Material.Load("materials/screen.vmat"), new Vector2Int(1280, 720));
	}
}
