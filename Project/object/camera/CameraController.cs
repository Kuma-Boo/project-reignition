using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Gameplay
{
	/// <summary>
	/// Follows the player based on the settings provided from CameraSettingsResource.cs
	/// </summary>
	public partial class CameraController : Node3D
	{
		public static CameraController instance;
		public Node3D EventController { get; set; } //Node3D to follow (i.e. in a cutscene)

		[ExportSubgroup("Gameplay Camera")]
		[Export]
		private Node3D cameraRoot;
		[Export]
		private Node3D debugMesh;
		[Export]
		private Camera3D camera;
		public Camera3D Camera => camera;

		[Export]
		private TextureRect _crossfade;
		[Export]
		private AnimationPlayer _crossfadeAnimator;

		private CharacterController Character => CharacterController.instance;
		private CharacterPathFollower PathFollower => Character.PathFollower;

		public Vector2 ConvertToScreenSpace(Vector3 worldSpace) => camera.UnprojectPosition(worldSpace);
		public bool IsOnScreen(Vector3 worldSpace) => camera.IsPositionInFrustum(worldSpace);
		public bool IsBehindCamera(Vector3 worldSpace) => camera.IsPositionBehind(worldSpace);
		/// <summary> Angle to use when transforming from world space to camera space </summary>
		private float xformAngle;
		public float TransformAngle(float angle) => xformAngle + angle;

		public override void _EnterTree()
		{
			if (Engine.IsEditorHint()) return;

			instance = this;

			UpdateCameraSettings(new CameraBlendData()
			{
				SettingsResource = defaultSettings,
			}); //Apply default settings
		}

		public override void _PhysicsProcess(double _)
		{
			if (EventController != null)
			{
				cameraRoot.GlobalTransform = EventController.GlobalTransform;
				return;
			}

			//Don't update the camera when the player is defeated
			if (Character.IsDefeated) return;

			UpdateGameplayCamera();

			if (OS.IsDebugBuild())
				UpdateFreeCam();
		}

		#region Gameplay Camera
		#region Transitions and Settings
		[Export]
		/// <summary> Default camera settings to use when nothing is set </summary>
		public CameraSettingsResource defaultSettings;
		/// <summary> Reference to active CameraBlendData. </summary>
		public CameraBlendData ActiveBlendData => CameraBlendList[CameraBlendList.Count - 1];
		/// <summary> Reference to active CameraSettingsResource. </summary>
		public CameraSettingsResource ActiveSettings => ActiveBlendData.SettingsResource;
		/// <summary> A list of all camera settings that are influencing camera. </summary>
		private readonly List<CameraBlendData> CameraBlendList = new List<CameraBlendData>();

		public void StartCrossfade()
		{
			ImageTexture tex = new ImageTexture(); //Render the viewport
			tex.SetImage(GetViewport().GetTexture().GetImage());
			_crossfade.Texture = tex;
			_crossfadeAnimator.Play("activate"); //Play crossfade animation
		}

		/// <summary>
		/// Change the current camera settings.
		/// </summary>
		public void UpdateCameraSettings(CameraBlendData data)
		{
			if (data.SettingsResource == null) return; //No Data

			if (Mathf.IsZeroApprox(data.BlendTime)) //Cut transition
				SnapFlag = true;
			else if (data.IsCrossfadeEnabled) //Crossfade transition
			{
				StartCrossfade();
				SnapFlag = true;
			}
			else
				data.CalculateBlendSpeed(); //Cache blend speed so we don't have to do it every frame

			//Add current data
			CameraBlendList.Add(data);
		}

		/// <summary> Set to true to skip smoothing. </summary>
		public bool SnapFlag { get; set; }
		/// <summary>
		/// Update the transition timer.
		/// </summary>
		private void UpdateTransitionTimer()
		{
			//Clear all lists (except active one) when snapping
			if (SnapFlag)
			{
				//Remove all blend data except the latest (active)
				for (int i = CameraBlendList.Count - 2; i >= 0; i--)
				{
					CameraBlendList[i].Free();
					CameraBlendList.RemoveAt(i);
				}

				//Remaining blend data is active, so it's influence gets set to 1
				CameraBlendList[0].SetInfluence(1);
			}
			else
			{
				for (int i = CameraBlendList.Count - 1; i >= 0; i--)
				{
					//Remove completed blends (Except for active blend data)
					if (i < CameraBlendList.Count - 1
						&& Mathf.IsEqualApprox(CameraBlendList[i + 1].LinearInfluence, 1.0f))
					{
						CameraBlendList[i].Free();
						CameraBlendList.RemoveAt(i);
						continue;
					}

					float influence = Mathf.MoveToward(CameraBlendList[i].LinearInfluence, 1f,
						CameraBlendList[i].BlendSpeed * PhysicsManager.physicsDelta);
					CameraBlendList[i].SetInfluence(influence);
				}
			}
		}

		public void Respawn()
		{
			//Revert camera settings
			UpdateCameraSettings(new CameraBlendData()
			{
				SettingsResource = LevelSettings.instance.CheckpointCameraSettings,
			});
		}
		#endregion

		private void UpdateGameplayCamera()
		{
			UpdateTransitionTimer();
			CameraPositionData data = new CameraPositionData()
			{
				offsetBasis = Basis.Identity,
				blendData = ActiveBlendData
			};

			float staticBlendRatio = 0; //Blend value of whether to use static camera positions or not
			Vector2 viewportOffset = Vector2.Zero;
			Vector3 staticPosition = Vector3.Zero;

			for (int i = 0; i < CameraBlendList.Count; i++) //Simulate each blend data separately
			{
				CameraPositionData iData = CalculateCameraTransform(i);
				data.offsetBasis = data.offsetBasis.Slerp(iData.offsetBasis, CameraBlendList[i].SmoothedInfluence);
				data.precalculatedPosition = data.precalculatedPosition.Lerp(iData.precalculatedPosition, CameraBlendList[i].SmoothedInfluence);

				data.yawTrackingRotation = Mathf.LerpAngle(data.yawTrackingRotation, iData.yawTrackingRotation, CameraBlendList[i].SmoothedInfluence);
				data.pitchTrackingRotation = Mathf.Lerp(data.pitchTrackingRotation, iData.pitchTrackingRotation, CameraBlendList[i].SmoothedInfluence);

				data.horizontalTrackingOffset = Mathf.Lerp(data.horizontalTrackingOffset, iData.horizontalTrackingOffset, CameraBlendList[i].SmoothedInfluence);
				data.verticalTrackingOffset = Mathf.Lerp(data.verticalTrackingOffset, iData.verticalTrackingOffset, CameraBlendList[i].SmoothedInfluence);

				data.distance = Mathf.Lerp(data.distance, iData.distance, CameraBlendList[i].SmoothedInfluence);

				staticBlendRatio = Mathf.Lerp(staticBlendRatio, CameraBlendList[i].SettingsResource.isStaticCamera ? 1 : 0, CameraBlendList[i].SmoothedInfluence);
				viewportOffset = viewportOffset.Lerp(CameraBlendList[i].SettingsResource.viewportOffset, CameraBlendList[i].SmoothedInfluence);
			}

			//Recalculate non-static camera positions for better transition rotations.
			Transform3D cameraTransform = new Transform3D(data.offsetBasis, CalculatePosition(data).Lerp(data.precalculatedPosition, staticBlendRatio));
			cameraTransform = cameraTransform.RotatedLocal(Vector3.Up, data.yawTrackingRotation);
			cameraTransform = cameraTransform.RotatedLocal(Vector3.Right, data.pitchTrackingRotation);
			cameraTransform.Origin = AddTrackingOffset(cameraTransform.Origin, data);

			cameraRoot.GlobalTransform = cameraTransform; //Update transform

			//Update view offset
			camera.HOffset = viewportOffset.X;
			camera.VOffset = viewportOffset.Y;
			debugMesh.Position = new Vector3(viewportOffset.X, viewportOffset.Y, 0); //Update debug mesh

			xformAngle = Character.CalculateForwardAngle(-data.offsetBasis.Z);

			if (SnapFlag) //Reset flag after camera was updating
				SnapFlag = false;
		}

		private Vector3 AddTrackingOffset(Vector3 position, CameraPositionData data)
		{
			position += data.offsetBasis.X.Normalized() * data.horizontalTrackingOffset;
			position += Vector3.Up * data.verticalTrackingOffset; //Use Pathfollower's up axis for vertical offset
			return position;
		}

		private Vector3 CalculatePosition(CameraPositionData data)
		{
			Vector3 position = data.offsetBasis.Z.Normalized() * data.distance;
			position += PathFollower.GlobalPosition;
			return position;
		}

		/// <summary>
		/// Simulates a camera setting and returns the Transform of where it would end up.
		/// </summary>
		private CameraPositionData CalculateCameraTransform(int index)
		{
			CameraSettingsResource settings = CameraBlendList[index].SettingsResource;
			CameraPositionData data = new CameraPositionData()
			{
				offsetBasis = Basis.Identity.Rotated(Vector3.Up, Mathf.Pi),
				blendData = CameraBlendList[index],
			};

			float yawAngle = settings.yawAngle;
			float pitchAngle = settings.pitchAngle;

			if (!settings.isStaticCamera) //ALL BROKEN :(
			{
				data.distance = settings.distance;

				//TODO add extra distance based on backstep
				if (settings.distanceCalculationMode == CameraSettingsResource.DistanceModeEnum.Offset)
				{
					if (settings.yawOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
						yawAngle += PathFollower.ForwardAngle;

					if (settings.pitchOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
						pitchAngle += PathFollower.Forward().AngleTo(PathFollower.Forward().RemoveVertical().Normalized()) * Mathf.Sign(PathFollower.Forward().Y);
				}
				else
				{
					float currentProgress = PathFollower.Progress; //Cache progress

					float extrapolatedDistance = settings.distance - PathFollower.Progress; //Used to prevent the camera getting stuck at the beginning of paths
					if (extrapolatedDistance < 0)
						extrapolatedDistance = 0;
					PathFollower.Progress -= settings.distance;
					Vector3 sampledForward = PathFollower.Forward();
					Vector3 sampledDelta = PathFollower.GlobalPosition + PathFollower.Back() * extrapolatedDistance;
					PathFollower.Progress = currentProgress; //Revert progress
					sampledDelta -= PathFollower.GlobalPosition;

					if (settings.yawOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
						yawAngle += Character.CalculateForwardAngle(-sampledDelta.Normalized());

					if (settings.yawOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
						pitchAngle += sampledForward.AngleTo(sampledForward.RemoveVertical().Normalized()) * Mathf.Sign(sampledForward.Y);
				}

				data.offsetBasis = data.offsetBasis.Rotated(Vector3.Up, yawAngle);
				data.offsetBasis = data.offsetBasis.Rotated(data.offsetBasis.X.Normalized(), pitchAngle);

				if (settings.followPathTilt)
				{
					float tiltAngle = PathFollower.Right().SignedAngleTo(-PathFollower.RightAxis, PathFollower.Forward());
					data.offsetBasis = data.offsetBasis.Rotated(data.offsetBasis.Z.Normalized(), tiltAngle);
				}

				//Update Tracking
				//Calculate position for tracking calculations
				data.precalculatedPosition = data.offsetBasis.Z.Normalized() * data.distance;
				data.precalculatedPosition += PathFollower.GlobalPosition;

				Vector3 globalDelta = Character.GlobalPosition - data.precalculatedPosition;
				Vector3 delta = data.offsetBasis.Inverse() * globalDelta;

				if (settings.horizontalTrackingMode == CameraSettingsResource.TrackingModeEnum.Move)
					data.horizontalTrackingOffset = delta.X;
				else if (settings.horizontalTrackingMode == CameraSettingsResource.TrackingModeEnum.Rotate)
					data.yawTrackingRotation = delta.Normalized().Flatten().AngleTo(Vector2.Up);

				if (settings.verticalTrackingMode == CameraSettingsResource.TrackingModeEnum.Move)
					data.verticalTrackingOffset = globalDelta.Y;
				else if (settings.verticalTrackingMode == CameraSettingsResource.TrackingModeEnum.Rotate) //Rotational tracking
					data.pitchTrackingRotation = delta.Normalized().AngleTo(delta.RemoveVertical().Normalized()) * Mathf.Sign(delta.Y);

				//Recalculate position after applying rotational tracking
				data.precalculatedPosition = data.offsetBasis.Z.Normalized() * data.distance;
				data.precalculatedPosition += PathFollower.GlobalPosition;
				data.precalculatedPosition = AddTrackingOffset(data.precalculatedPosition, data);
			}
			else
			{
				data.precalculatedPosition = data.blendData.StaticPosition;

				Vector3 delta = Character.GlobalPosition - data.precalculatedPosition;
				data.distance = delta.Length();
				delta = delta.Normalized();

				if (settings.yawOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
					yawAngle += delta.Flatten().AngleTo(Vector2.Up) + Mathf.Pi;
				if (settings.pitchOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
					pitchAngle += delta.AngleTo(delta.RemoveVertical().Normalized()) * Mathf.Sign(delta.Y);

				data.offsetBasis = data.offsetBasis.Rotated(Vector3.Up, yawAngle);
				data.offsetBasis = data.offsetBasis.Rotated(data.offsetBasis.X.Normalized(), pitchAngle);
			}

			return data;
		}

		private struct CameraPositionData
		{
			/// <summary> Rotation data used for offset calculation. </summary>
			public Basis offsetBasis;

			/// <summary> How much to offset. </summary>
			public float distance;

			/// <summary> Yaw rotation data used for tracking. </summary>
			public float yawTrackingRotation;
			/// <summary> Pitch rotation data used for tracking. </summary>
			public float pitchTrackingRotation;

			/// <summary> How much to move camera for horizontal tracking. </summary>
			public float horizontalTrackingOffset;
			/// <summary> How much to move camera for vertical tracking. </summary>
			public float verticalTrackingOffset;

			/// <summary> Only used when blending with a static camera. </summary>
			public Vector3 precalculatedPosition;

			/// <summary> Reference to the CameraBlendData being used. </summary>
			public CameraBlendData blendData;
		}
		#endregion

		#region Free Cam
		private float freecamMovespeed = 20;
		private const float MOUSE_SENSITIVITY = .1f;

		private bool isFreeCamEnabled;
		private bool freeCamRotating;
		private bool freeCamTilting;

		private void UpdateFreeCam()
		{
			if (Input.IsKeyPressed(Key.R))
			{
				isFreeCamEnabled = freeCamRotating = false;
				camera.Transform = Transform3D.Identity;
			}

			freeCamRotating = Input.IsMouseButtonPressed(MouseButton.Left);
			freeCamTilting = Input.IsMouseButtonPressed(MouseButton.Right);
			if (freeCamRotating || freeCamTilting)
			{
				isFreeCamEnabled = true;
				Input.MouseMode = Input.MouseModeEnum.Captured;
			}
			else
			{
				Input.MouseMode = Input.MouseModeEnum.Visible;
			}

			debugMesh.Visible = isFreeCamEnabled;

			if (!isFreeCamEnabled) return;
			float targetMoveSpeed = freecamMovespeed;

			if (Input.IsKeyPressed(Key.Shift))
				targetMoveSpeed *= 2;
			else if (Input.IsKeyPressed(Key.Ctrl))
				targetMoveSpeed *= .5f;

			if (Input.IsKeyPressed(Key.E))
				camera.GlobalTranslate(camera.Up() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed(Key.Q))
				camera.GlobalTranslate(camera.Down() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed(Key.W))
				camera.GlobalTranslate(camera.Back() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed(Key.S))
				camera.GlobalTranslate(camera.Forward() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed(Key.D))
				camera.GlobalTranslate(camera.Right() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed(Key.A))
				camera.GlobalTranslate(camera.Left() * targetMoveSpeed * PhysicsManager.physicsDelta);
		}

		public override void _Input(InputEvent e)
		{
			if (e is InputEventMouseMotion)
			{
				if (freeCamRotating)
				{
					camera.RotateObjectLocal(Vector3.Up, Mathf.DegToRad(-(e as InputEventMouseMotion).Relative.X) * MOUSE_SENSITIVITY);
					camera.RotateObjectLocal(Vector3.Right, Mathf.DegToRad(-(e as InputEventMouseMotion).Relative.Y) * MOUSE_SENSITIVITY);
				}
				else if (freeCamTilting)
					camera.RotateObjectLocal(Vector3.Forward, Mathf.DegToRad((e as InputEventMouseMotion).Relative.X) * MOUSE_SENSITIVITY);
			}
			else if (e is InputEventMouseButton emb)
			{
				if (emb.IsPressed())
				{
					if (emb.ButtonIndex == MouseButton.WheelUp)
					{
						freecamMovespeed += 5;
						GD.Print($"Free cam Speed set to {freecamMovespeed}.");
					}
					if (emb.ButtonIndex == MouseButton.WheelDown)
					{
						freecamMovespeed -= 5;
						if (freecamMovespeed < 0)
							freecamMovespeed = 0;
						GD.Print($"Free cam Speed set to {freecamMovespeed}.");
					}
				}
			}

			e.Dispose();
		}
		#endregion
	}

	public partial class CameraBlendData : GodotObject
	{
		/// <summary> Use crossfading? </summary>
		public bool IsCrossfadeEnabled { get; set; }

		/// <summary> Ratio [0 <-> 1] of how much influence this blend has. </summary>
		public float LinearInfluence { get; private set; }
		/// <summary> Influence, smoothed with Mathf.Smoothstep. </summary>
		public float SmoothedInfluence { get; private set; }
		/// <summary> Actual amount to blend each frame. </summary>
		public float BlendSpeed { get; private set; }

		/// <summary> How long blending takes in seconds. </summary>
		public float BlendTime { get; set; }
		/// <summary> Camera's static position. Only used when CameraSettingsResource.isStaticCamera is true. </summary>
		public Vector3 StaticPosition { get; set; }

		/// <summary> CameraSettingsResource for this camera setting. </summary>
		public CameraSettingsResource SettingsResource { get; set; }

		public void SetInfluence(float rawInfluence)
		{
			LinearInfluence = rawInfluence;
			SmoothedInfluence = Mathf.SmoothStep(0.0f, 1.0f, rawInfluence);
		}

		public void CalculateBlendSpeed() => BlendSpeed = 1f / BlendTime;
	}
}