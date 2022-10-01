using Godot;
using Godot.Collections;
using Project.Core;
using Project.Gameplay;

namespace Project
{
	public partial class Sun : Node3D
	{
		[Export]
		public NodePath depthCamera;
		private Camera3D _depthCamera;
		[Export]
		public NodePath depthViewport;
		private SubViewport _depthViewport;
		[Export]
		public NodePath lensFlareBase;
		private Node2D _lensFlareBase;
		private readonly Array<Node2D> _lensPieces = new Array<Node2D>();
		private readonly float LENS_FLARE_SPACING = .2f;

		[Export(PropertyHint.Range, "0,1,0.1")]
		public float backgroundSeparation;
		[Export(PropertyHint.Range, "0,1")]
		public float movementThreshold;

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
			_depthCamera = GetNode<Camera3D>(depthCamera);
			_depthCamera.Visible = true; //Enable the depth camera

			_depthViewport = GetNode<SubViewport>(depthViewport);

			_lensFlareBase = GetNode<Node2D>(lensFlareBase);
			for (int i = 0; i < _lensFlareBase.GetChildCount(); i++)
				_lensPieces.Add(_lensFlareBase.GetChild<Node2D>(i));

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
			_depthCamera.GlobalTransform = Camera.CameraTransform; //Keep cameras in sync
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
			GD.Print(currentMovement);

			UpdateLensFlare();
		}

		private float SampleTexture()
		{
			screenUV = Camera.ConvertToScreenSpace(GlobalPosition) / RuntimeConstants.SCREEN_SIZE;
			Image depthBuffer = _depthViewport.GetTexture().GetImage();
			Vector2i samplePosition = (Vector2i)(screenUV * depthBuffer.GetSize()).Round();
			return depthBuffer.GetPixelv(samplePosition).r;
		}

		private void UpdateLensFlare()
		{
			Vector2 originPosition = Camera.ConvertToScreenSpace(GlobalPosition);
			Vector2 flareDirection = _lensFlareBase.GlobalPosition - originPosition; //Get the direction to the center of the screen

			_lensFlareBase.Modulate = Colors.White.Lerp(Colors.Transparent, currentOcclusion);
			for (int i = 0; i < _lensPieces.Count; i++)
				_lensPieces[i].Position = flareDirection * i * LENS_FLARE_SPACING;

		}
	}
}
