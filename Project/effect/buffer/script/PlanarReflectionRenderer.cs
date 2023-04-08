using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary> Renders planar reflections into a global texture, then updates materials in reflectionMaterials. </summary>
	[Tool]
	public partial class PlanarReflectionRenderer : Node3D
	{
		public static ViewportTexture ReflectionTexture { get; private set; }
		public static readonly StringName REFLECTION_PARAMETER = "reflection_texture"; //All shaders that use reflections must have this parameter

		[Export(PropertyHint.Layers3DRender)]
		private uint renderMask;

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
		/// <summary> List of materials that use reflection_texture. </summary>
		public Array<ShaderMaterial> reflectionMaterials;

		[Export]
		/// <summary> Attempt to preview in the editor? </summary>
		private bool editorPreview;
		private Vector3 previousCapturePosition;
		private Vector3 previousCaptureRotation;

		private Callable UpdatePositionCallable => new Callable(this, MethodName.UpdatePosition);
		private Callable ApplyTextureCallable => new Callable(this, MethodName.ApplyTexture);

		public override void _EnterTree()
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

			reflectionCamera.CullMask = renderMask;
			reflectionCamera.Fov = mainCamera.Fov;
			reflectionCamera.Size = mainCamera.Size;
			reflectionCamera.Projection = mainCamera.Projection;

			reflectionCamera.Near = nearClip;
			reflectionCamera.Far = mainCamera.Far;

			// Update reflectionCamera's position
			Vector3 reflectionAxis = this.Up();
			Vector3 projection = reflectionAxis * reflectionAxis.Dot(mainCamera.GlobalPosition - GlobalPosition);
			Vector3 targetPosition = (mainCamera.GlobalPosition - projection * 2f);

			// Update reflectionCamera's rotation
			Vector3 upDirection = mainCamera.Up().Reflect(reflectionAxis.Normalized());
			Vector3 forwardDirection = mainCamera.Forward().Reflect(reflectionAxis.Normalized());

			reflectionCamera.LookAtFromPosition(targetPosition, targetPosition + forwardDirection, upDirection);

			if (Engine.IsEditorHint())
				reflectionViewport.Size = (Vector2I)mainCamera.GetViewport().GetVisibleRect().Size;
			else
				reflectionViewport.Size = Runtime.HALF_SCREEN_SIZE;

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
					reflectionMaterials[i].SetShaderParameter(REFLECTION_PARAMETER, ReflectionTexture);
			}
		}
	}
}
