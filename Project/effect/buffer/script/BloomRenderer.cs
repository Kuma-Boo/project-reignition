using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Renders bloom objects
	/// BloomRenderer blends between different LODs based on distance
	/// BloomRenderer does a depth test based on the depth buffer
	/// BloomRenderer is overlayed on top of the original scene render
	/// </summary>
	public partial class BloomRenderer : Node
	{
		public ViewportTexture BloomTexture { get; private set; }

		public readonly StringName BLOOM_PARAMETER = "bloom_texture";
		public readonly StringName MULTIPLY_PARAMETER = "multiply";

		[Export]
		private CompositeMode compositeMode;
		private enum CompositeMode
		{
			Add,
			Multiply
		}

		[Export]
		private Camera3D bloomCamera;
		[Export]
		private SubViewport bloomViewport;
		[Export]
		private SubViewport compositeViewport; //Blurred viewport, ready to be composited onto the final render
		[Export]
		private ShaderMaterial bloomMaterial; //Material that compares bloom's depth with geometry
		[Export]
		private ShaderMaterial compositeMaterial; //Material that overlays the final bloom image to the screen

		private Camera3D MainCamera => CameraController.instance.Camera;
		private Callable ApplyTextureCallable => new Callable(this, MethodName.ApplyTexture);

		public override void _Ready()
		{
			if (!RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable))
				RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable, (uint)ConnectFlags.Deferred);

			bloomCamera.Visible = true;

			bloomMaterial.Set(MULTIPLY_PARAMETER, compositeMode == CompositeMode.Multiply);
			compositeMaterial.Set(MULTIPLY_PARAMETER, compositeMode == CompositeMode.Multiply);
		}

		public override void _ExitTree()
		{
			if (RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable))
				RenderingServer.Singleton.Disconnect(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable);
		}

		public override void _Process(double _)
		{
			bloomCamera.Fov = MainCamera.Fov;
			bloomCamera.Size = MainCamera.Size;
			bloomCamera.Projection = MainCamera.Projection;

			bloomCamera.Near = MainCamera.Near;
			bloomCamera.Far = MainCamera.Far;
			bloomCamera.GlobalTransform = MainCamera.GlobalTransform;

			bloomViewport.Size = compositeViewport.Size = RuntimeConstants.SCREEN_SIZE;
			bloomViewport.RenderTargetUpdateMode = compositeViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;

			bloomMaterial.Set(DepthRenderer.FAR_CLIP_PARAMETER, MainCamera.Far);
		}

		private void ApplyTexture()
		{
			BloomTexture = compositeViewport.GetTexture();
			compositeMaterial.Set(BLOOM_PARAMETER, BloomTexture);

			bloomMaterial.Set(DepthRenderer.DEPTH_PARAMETER, DepthRenderer.DepthTexture);
		}
	}
}
