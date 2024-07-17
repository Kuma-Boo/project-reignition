using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	public partial class DepthRenderer : Node
	{
		//KNOWN BUG: Viewports are unable to render HDR images, so depth texture gets corrupted
		public static ViewportTexture DepthTexture { get; private set; }
		public static readonly StringName DEPTH_PARAMETER = "depth_texture"; //All shaders that use depth must have this parameter
		public static readonly StringName FAR_CLIP_PARAMETER = "far_clip_distance";

		[Export]
		private Camera3D depthCamera; //Used to get the depth texture to determine whether the sun is occluded
		[Export]
		private SubViewport depthViewport;
		[Export]
		private ShaderMaterial depthMaterial;

		[Export]
		public Array<ShaderMaterial> depthMaterials; //List of materials that use depth_texture

		private Camera3D Camera => CharacterController.instance.Camera.Camera;
		private Callable ApplyTextureCallable => new Callable(this, MethodName.ApplyTexture);

		public override void _Ready()
		{
			if (!RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable))
				RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable);

			depthCamera.Visible = true;
		}

		public override void _ExitTree()
		{
			if (RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable))
				RenderingServer.Singleton.Disconnect(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable);

			DepthTexture = null;
		}

		public override void _Process(double _)
		{
			depthCamera.Fov = Camera.Fov;
			depthCamera.Size = Camera.Size;
			depthCamera.Projection = Camera.Projection;

			depthCamera.Near = Camera.Near;

			depthCamera.Far = Camera.Far;
			depthCamera.GlobalTransform = Camera.GlobalTransform;

			depthViewport.Size = Runtime.HalfScreenSize;
			depthViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;

			depthMaterial.Set(FAR_CLIP_PARAMETER, depthCamera.Far);
		}

		private void ApplyTexture()
		{
			DepthTexture = depthViewport.GetTexture();

			if (depthMaterials != null)
			{
				for (int i = 0; i < depthMaterials.Count; i++)
				{
					depthMaterials[i].SetShaderParameter(DEPTH_PARAMETER, DepthTexture);
				}
			}
		}
	}
}
