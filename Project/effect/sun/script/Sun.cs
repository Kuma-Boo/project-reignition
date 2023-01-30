using Godot;
using Godot.Collections;
using Project.Core;
using Project.Gameplay;

namespace Project
{
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
		private bool isOccluded = true; //Start occluded
		private float currentOcclusion;
		private float currentOcclusionVelocity;
		private readonly float OCCLUSION_SMOOTHING = .5f;
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

			currentOcclusion = ExtensionMethods.SmoothDamp(currentOcclusion, isOccluded ? 1f : 0f, ref currentOcclusionVelocity, OCCLUSION_SMOOTHING);
			RenderingServer.GlobalShaderParameterSet(SHADER_GLOBAL_SUN_OCCLUSION, currentOcclusion);

			screenUV = MainCamera.ConvertToScreenSpace(GlobalPosition) / RuntimeConstants.SCREEN_SIZE;
			if ((screenUV - previousScreenUV).LengthSquared() * 100.0f > movementThreshold)
				currentMovement = 0f;
			previousScreenUV = screenUV;
			RenderingServer.GlobalShaderParameterSet(SHADER_GLOBAL_SUN_MOVEMENT, currentMovement);
			currentMovement = ExtensionMethods.SmoothDamp(currentMovement, 1f, ref currentMovementVelocity, MOVEMENT_SMOOTHING);

			UpdateSample();
			UpdateLensFlare();
		}

		private float updateTimer;
		private readonly float UPDATE_INTERVAL = .5f; //How often to update the sun. Reduces frame drops
		private void UpdateSample()
		{
			if (updateTimer <= 0f)
			{
				if (MainCamera.IsBehindCamera(GlobalPosition) || screenUV.x < 0.0f || screenUV.x > 1.0f || screenUV.y < 0.0f || screenUV.y > 1.0f)
					isOccluded = true; //Always occluded when the camera faces away
				else if (MainCamera.IsOnScreen(GlobalPosition))
				{
					//Sample texture. VERY SLOW!!!
					Image depthBuffer = DepthRenderer.DepthTexture.GetImage();
					Vector2i samplePosition = (Vector2i)(screenUV * depthBuffer.GetSize());
					samplePosition = samplePosition.Clamp(Vector2i.Zero, depthBuffer.GetSize() - Vector2i.One);

					//Since the sun is so far away, a simple alpha check can determine occlusion.
					float alpha = depthBuffer.GetPixelv(samplePosition).a;
					isOccluded = !Mathf.IsZeroApprox(alpha);
					depthBuffer.Dispose(); //Don't forget to dispose the image!
				}

				updateTimer = UPDATE_INTERVAL;
			}
			else
				updateTimer -= PhysicsManager.physicsDelta;
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
