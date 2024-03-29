using Godot;

namespace Project.Interface.Menus
{
	public partial class Options : Control
	{
		[Export]
		public ShaderMaterial menuOverlay;
		[Export]
		public SubViewport menuViewport;
		private Callable ApplyTextureCallable => new(this, MethodName.ApplyTexture);
		public static readonly StringName MENU_PARAMETER = "menu_texture";

		public override void _Ready()
		{
			if (!RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable))
				RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable);
		}

		public override void _ExitTree()
		{
			if (RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable))
				RenderingServer.Singleton.Disconnect(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable);
		}

		public void ApplyTexture()
		{
			menuOverlay.SetShaderParameter(MENU_PARAMETER, menuViewport.GetTexture());
		}
	}
}
