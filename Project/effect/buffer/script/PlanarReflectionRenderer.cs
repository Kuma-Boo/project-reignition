using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Renders planar reflections into a global texture, then updates materials in reflectionMaterials.
	/// To have multiple reflections at different elevations, create multiple PlanarReflectionRenderers
	/// and use separate materials for each.
	/// </summary>
	public partial class PlanarReflectionRenderer : Node3D
	{
		// All shaders that use reflections must have this parameter
		public static readonly StringName REFLECTION_PARAMETER = "reflection_texture";

		[Export]
		private Camera3D reflectionCamera;
		[Export]
		private SubViewport reflectionViewport;
		private Camera3D GameplayCamera => CharacterController.instance.Camera.Camera;


		[Export]
		/// <summary> List of materials that use reflection_texture. </summary>
		public Array<ShaderMaterial> reflectionMaterials;

		private Callable UpdatePositionCallable => new(this, MethodName.UpdatePosition);
		private Callable ApplyTextureCallable => new(this, MethodName.ApplyTexture);

		public override void _EnterTree()
		{
			reflectionViewport.Size = SaveManager.WINDOW_SIZES[SaveManager.Config.windowSize];
			switch (SaveManager.Config.reflectionQuality)
			{
				case SaveManager.QualitySetting.DISABLED:
					reflectionCamera.ClearCurrent();
					return;
				case SaveManager.QualitySetting.LOW: // Quarter resolution
					reflectionViewport.Size /= 4;
					break;
				case SaveManager.QualitySetting.MEDIUM: // Half resolution
					reflectionViewport.Size /= 2;
					break;
			}

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

			ApplyTexture();
		}


		//Mirror main camera along plane
		private void UpdatePosition()
		{
			reflectionCamera.CullMask = GameplayCamera.CullMask;
			reflectionCamera.Fov = GameplayCamera.Fov;
			reflectionCamera.Size = GameplayCamera.Size;
			reflectionCamera.Projection = GameplayCamera.Projection;

			reflectionCamera.Near = GameplayCamera.Near;
			reflectionCamera.Far = GameplayCamera.Far;

			// Calculate reflection position
			Vector3 reflectionPosition = GlobalPosition;

			// Update reflectionCamera's position
			Vector3 reflectionAxis = this.Up();
			Vector3 projection = reflectionAxis * reflectionAxis.Dot(GameplayCamera.GlobalPosition - reflectionPosition);
			Vector3 targetPosition = (GameplayCamera.GlobalPosition - projection * 2f);

			// Update reflectionCamera's rotation
			Vector3 upDirection = GameplayCamera.Up().Reflect(reflectionAxis.Normalized());
			Vector3 forwardDirection = GameplayCamera.Forward().Reflect(reflectionAxis.Normalized());

			reflectionCamera.LookAtFromPosition(targetPosition, targetPosition + forwardDirection, upDirection);

			if (Engine.IsEditorHint())
				reflectionViewport.Size = (Vector2I)GameplayCamera.GetViewport().GetVisibleRect().Size;
			else
				reflectionViewport.Size = Runtime.HALF_SCREEN_SIZE;

			reflectionViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		}


		/// <summary> Applies reflection texture to associated shaders. </summary>
		private void ApplyTexture()
		{
			ViewportTexture ReflectionTexture = reflectionViewport.GetTexture();

			if (reflectionMaterials != null)
			{
				for (int i = 0; i < reflectionMaterials.Count; i++)
					reflectionMaterials[i].SetShaderParameter(REFLECTION_PARAMETER, ReflectionTexture);
			}
		}
	}
}
