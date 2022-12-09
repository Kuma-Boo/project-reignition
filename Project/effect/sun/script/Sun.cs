using Godot;
using Godot.Collections;
using Project.Core;
using Project.Gameplay;

namespace Project
{
	/// <summary>
	/// Requires the DepthRenderer to be enabled to function properly.
	/// </summary>
	public partial class Sun : Node3D
	{
		[Export]
		private Node2D lensFlareBase;
		private readonly Array<Node2D> _lensPieces = new Array<Node2D>();
		private readonly float LENS_FLARE_SPACING = .2f;

		[Export(PropertyHint.Range, "0,1,0.1")]
		private float backgroundSeparation;
		[Export(PropertyHint.Range, "0,1")]
		private float movementThreshold;

		private Vector2 previousScreenUV;
		private Vector2 screenUV;
		private float currentMovement;
		private float currentMovementVelocity;
		private float currentOcclusion;
		private float currentOcclusionVelocity;
		private readonly float OCCLUSION_SMOOTHING = .2f;
		private readonly float MOVEMENT_SMOOTHING = 4.0f;

		private CameraController MainCamera => CameraController.instance;
		private Callable UpdateSunCallable => new Callable(this, MethodName.UpdateSun);

		private readonly StringName SHADER_GLOBAL_SUN_OCCLUSION = "sun_occlusion";
		private readonly StringName SHADER_GLOBAL_SUN_MOVEMENT = "sun_movement";

		public override void _Ready()
		{
			for (int i = 0; i < lensFlareBase.GetChildCount(); i++)
				_lensPieces.Add(lensFlareBase.GetChild<Node2D>(i));

			if (!RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, UpdateSunCallable))
				RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw, UpdateSunCallable, (uint)ConnectFlags.Deferred);
		}

		public override void _ExitTree()
		{
			if (RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, UpdateSunCallable))
				RenderingServer.Singleton.Disconnect(RenderingServer.SignalName.FramePostDraw, UpdateSunCallable);
		}

		private void UpdateSun()
		{
			if (MainCamera == null || DepthRenderer.DepthTexture == null) return; //No camera/depth texture found

			bool isOccluded = true;
			if (!MainCamera.IsPositionBehind(GlobalPosition) && MainCamera.IsOnScreen(GlobalPosition))
				isOccluded = SampleTexture() < backgroundSeparation;

			currentOcclusion = ExtensionMethods.SmoothDamp(currentOcclusion, isOccluded ? 1f : 0f, ref currentOcclusionVelocity, OCCLUSION_SMOOTHING);
			RenderingServer.GlobalShaderParameterSet(SHADER_GLOBAL_SUN_OCCLUSION, currentOcclusion);

			if ((screenUV - previousScreenUV).LengthSquared() * 100.0f > movementThreshold)
				currentMovement = 0f;

			previousScreenUV = screenUV;
			RenderingServer.GlobalShaderParameterSet(SHADER_GLOBAL_SUN_MOVEMENT, currentMovement);
			currentMovement = ExtensionMethods.SmoothDamp(currentMovement, 1f, ref currentMovementVelocity, MOVEMENT_SMOOTHING);

			UpdateLensFlare();
		}

		private float SampleTexture()
		{
			screenUV = MainCamera.ConvertToScreenSpace(GlobalPosition) / RuntimeConstants.SCREEN_SIZE;
			Image depthBuffer = DepthRenderer.DepthTexture.GetImage();
			Vector2 samplePosition = screenUV * depthBuffer.GetSize();
			return depthBuffer.GetPixel(Mathf.FloorToInt(samplePosition.x), Mathf.FloorToInt(samplePosition.y)).r;
		}

		private void UpdateLensFlare()
		{
			Vector2 originPosition = MainCamera.ConvertToScreenSpace(GlobalPosition);
			Vector2 flareDirection = lensFlareBase.GlobalPosition - originPosition; //Get the direction to the center of the screen

			lensFlareBase.Modulate = Colors.White.Lerp(Colors.Transparent, currentOcclusion);
			for (int i = 0; i < _lensPieces.Count; i++)
				_lensPieces[i].Position = flareDirection * i * LENS_FLARE_SPACING;
		}
	}
}
