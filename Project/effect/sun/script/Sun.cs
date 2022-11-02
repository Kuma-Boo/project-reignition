using Godot;
using Godot.Collections;
using Project.Core;
using Project.Gameplay;

namespace Project
{
	public partial class Sun : Node3D
	{
		[Export]
		private Camera3D depthCamera; //Used to get the depth texture to determine whether the sun is occluded
		[Export]
		private SubViewport depthViewport;
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

		private CameraController Camera => CameraController.instance;
		private readonly StringName SHADER_GLOBAL_SUN_OCCLUSION = new StringName("sun_occlusion");
		private readonly StringName SHADER_GLOBAL_SUN_MOVEMENT = new StringName("sun_movement");

		public override void _Ready()
		{
			depthCamera.Visible = true; //Enable the depth camera

			for (int i = 0; i < lensFlareBase.GetChildCount(); i++)
				_lensPieces.Add(lensFlareBase.GetChild<Node2D>(i));

			RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePreDraw, new Callable(this, MethodName.SyncCamera), (uint)ConnectFlags.Deferred);
			RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw, new Callable(this, MethodName.UpdateFrame), (uint)ConnectFlags.Deferred);
		}

		public override void _ExitTree()
		{
			RenderingServer.Singleton.Disconnect(RenderingServer.SignalName.FramePreDraw, new Callable(this, MethodName.SyncCamera));
			RenderingServer.Singleton.Disconnect(RenderingServer.SignalName.FramePostDraw, new Callable(this, MethodName.UpdateFrame));
		}

		private void SyncCamera()
		{
			if (Camera == null) return; //No camera found			
			depthCamera.GlobalTransform = Camera.CameraTransform; //Keep cameras in sync
		}

		private void UpdateFrame()
		{
			if (Camera == null) return; //No camera found

			bool isOccluded = true;
			if (!Camera.IsPositionBehind(GlobalPosition) && Camera.IsOnScreen(GlobalPosition))
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
			screenUV = Camera.ConvertToScreenSpace(GlobalPosition) / RuntimeConstants.SCREEN_SIZE;
			Image depthBuffer = depthViewport.GetTexture().GetImage();
			Vector2 samplePosition = screenUV * depthBuffer.GetSize();
			return depthBuffer.GetPixel(Mathf.FloorToInt(samplePosition.x), Mathf.FloorToInt(samplePosition.y)).r;
		}

		private void UpdateLensFlare()
		{
			Vector2 originPosition = Camera.ConvertToScreenSpace(GlobalPosition);
			Vector2 flareDirection = lensFlareBase.GlobalPosition - originPosition; //Get the direction to the center of the screen

			lensFlareBase.Modulate = Colors.White.Lerp(Colors.Transparent, currentOcclusion);
			for (int i = 0; i < _lensPieces.Count; i++)
				_lensPieces[i].Position = flareDirection * i * LENS_FLARE_SPACING;

		}
	}
}
