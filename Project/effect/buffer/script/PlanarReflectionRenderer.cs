using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Renders planar reflections into a global texture.
	/// Few side notes:
	/// 1. This method is hard-coded to only support reflections facing Vector3.Up
	/// 2. Near clipping cannot be aligned to the surface, so objects underneath will still be rendered (looking for a fix)
	/// </summary>
	[Tool]
	public partial class PlanarReflectionRenderer : Node
	{
		public static ViewportTexture ReflectionTexture { get; private set; }
		public static readonly StringName REFLECTION_PARAMETER = "reflection_texture"; //All shaders that use reflections must have this parameter

		[Export]
		private NodePath reflectionCameraPath;
		private Camera3D reflectionCamera;
		private Camera3D mainCamera;
		[Export]
		private NodePath reflectionViewportPath;
		private SubViewport reflectionViewport;
		[Export]
		private float nearClip = .05f;
		[Export]
		private bool editorPreview;

		[Export]
		public Array<ShaderMaterial> reflectionMaterials; //List of materials that use reflection_texture

		[Export]
		private Node3D reflectorNode; //Plane to reflect against. Must be facing up
		[Export]
		private float reflectorHeight; //Alternatively, specify reflection Y point here
		private Vector3 previousCapturePosition;
		private Vector3 previousCaptureRotation;

		private Callable UpdatePositionCallable => new Callable(this, MethodName.UpdatePosition);
		private Callable ApplyTextureCallable => new Callable(this, MethodName.ApplyTexture);

		public override void _Ready()
		{
			if (!RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePreDraw, UpdatePositionCallable))
				RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePreDraw, UpdatePositionCallable, (uint)ConnectFlags.Deferred);

			if (!RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable))
				RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable, (uint)ConnectFlags.Deferred);
		}

		public override void _ExitTree()
		{
			if (RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePreDraw, UpdatePositionCallable))
				RenderingServer.Singleton.Disconnect(RenderingServer.SignalName.FramePreDraw, UpdatePositionCallable);

			if (RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable))
				RenderingServer.Singleton.Disconnect(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable);

			ReflectionTexture = null;
		}

		private bool GetCamera()
		{
			if (reflectionCamera == null)
				reflectionCamera = GetNodeOrNull<Camera3D>(reflectionCameraPath);

			if (Engine.IsEditorHint())
				mainCamera = Editor.Plugin.editorCam;
			else if (CameraController.instance != null)
				mainCamera = CameraController.instance.Camera;

			return mainCamera != null && reflectionCamera != null;
		}

		//Mirror main camera along plane
		private void UpdatePosition()
		{
			if (!GetCamera() || !GetReflectionViewport()) return;
			if (Engine.IsEditorHint() && !editorPreview) return;

			if (Engine.IsEditorHint())
			{
				if (mainCamera.GlobalPosition.IsEqualApprox(previousCapturePosition) &&
				mainCamera.Forward().IsEqualApprox(previousCaptureRotation)) return; //Didn't move
				previousCapturePosition = mainCamera.GlobalPosition;
				previousCaptureRotation = mainCamera.Forward();
			}

			reflectionCamera.Fov = mainCamera.Fov;
			reflectionCamera.Size = mainCamera.Size;
			reflectionCamera.Projection = mainCamera.Projection;

			//TODO Update reflection camera's near clipping plane
			reflectionCamera.Near = nearClip;
			reflectionCamera.Far = mainCamera.Far;

			//Update reflectionCamera's position
			float reflectionHeight = reflectorHeight;
			if (reflectorNode != null)
				reflectionHeight = reflectorNode.GlobalPosition.y;
			Vector3 projection = Vector3.Down * (mainCamera.GlobalPosition.y - reflectionHeight);
			Vector3 targetPosition = mainCamera.GlobalPosition + projection * 2f;

			//Update reflectionCamera's rotation
			Vector3 upDirection = mainCamera.Up().Reflect(Vector3.Up);
			Vector3 forwardDirection = mainCamera.Forward().Reflect(Vector3.Up);

			reflectionCamera.LookAtFromPosition(targetPosition, targetPosition + forwardDirection, upDirection);

			reflectionViewport.Size = RuntimeConstants.HALF_SCREEN_SIZE;
			reflectionViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		}

		private bool GetReflectionViewport()
		{
			if (reflectionViewportPath == null) return false;
			if (reflectionViewport == null)
				reflectionViewport = GetNodeOrNull<SubViewport>(reflectionViewportPath);

			return reflectionViewport != null;
		}

		/// <summary> Applies reflection texture to associated shaders. </summary>
		private void ApplyTexture()
		{
			if (!GetReflectionViewport()) return;

			ReflectionTexture = reflectionViewport.GetTexture();

			if (reflectionMaterials != null)
			{
				for (int i = 0; i < reflectionMaterials.Count; i++)
				{
					reflectionMaterials[i].SetShaderParameter(REFLECTION_PARAMETER, ReflectionTexture);
				}
			}
		}
	}
}
