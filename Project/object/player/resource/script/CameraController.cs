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

		[ExportSubgroup("Gameplay Camera")]
		[Export]
		private Node3D cameraRoot;
		[Export]
		private Node3D debugMesh;
		[Export]
		private Camera3D camera;
		public Camera3D Camera => camera;
		public Vector2 ConvertToScreenSpace(Vector3 worldSpace) => camera.UnprojectPosition(worldSpace);
		public bool IsOnScreen(Vector3 worldSpace) => camera.IsPositionInFrustum(worldSpace);
		public bool IsBehindCamera(Vector3 worldSpace) => camera.IsPositionBehind(worldSpace);

		[Export]
		private TextureRect _crossfade;
		[Export]
		private AnimationPlayer _crossfadeAnimator;

		[Export]
		/// <summary> Camera's pathfollower. Different than Character.PathFollower. </summary>
		public CharacterPathFollower PathFollower { get; private set; }

		private CharacterController Character => CharacterController.instance;
		public Node3D EventController { get; set; } //Node3D to follow (i.e. in a cutscene)

		/// <summary> Angle to use when transforming from world space to camera space </summary>
		private float xformAngle;
		public float TransformAngle(float angle) => xformAngle + angle;

		public override void _EnterTree()
		{
			if (Engine.IsEditorHint()) return;

			instance = this;
		}

		public void Initialize()
		{
			if (Engine.IsEditorHint()) return;

			//Apply default settings
			CameraSettingsResource targetSettings = defaultSettings;
			if (StageSettings.instance != null && StageSettings.instance.initialCameraSettings != null)
				targetSettings = StageSettings.instance.initialCameraSettings;

			UpdateCameraSettings(new CameraBlendData()
			{
				SettingsResource = targetSettings,
			});

			Character.Connect(CharacterController.SignalName.Respawn, new Callable(this, MethodName.Respawn));
		}

		public void Respawn()
		{
			//Revert camera settings
			UpdateCameraSettings(new CameraBlendData()
			{
				SettingsResource = LevelSettings.instance.CheckpointCameraSettings,
			});
		}

		public void UpdateCamera()
		{
			PathFollower.Resync();

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
		#endregion

		/// <summary> Used to focus onto multi-HP enemies, bosses, etc. Not to be confused with CharacterLockon.Target. </summary>
		public Node3D LockonTarget { get; set; }
		/// <summary> [0 -> 1] ratio of how much to focus onto LockonTarget. </summary>
		private float lockonBlend;
		private float lockonBlendVelocity;
		/// <summary> Snappier blend when lockon is active to keep things in frame. </summary>
		private const float LOCKON_BLEND_IN_SMOOTHING = 10.0f;
		/// <summary> More smoothing/slower blend when resetting lockonBlend. </summary>
		private const float LOCKON_BLEND_OUT_SMOOTHING = 40.0f;
		/// <summary> Max blend between player and lockon target. (Higher - Bias towards target, Lower - Bias towards player) </summary>
		private const float MAX_LOCKON_BLEND = .6f;
		private void UpdateLockonTarget()
		{
			float targetBlend = 0.0f;
			float smoothing = LOCKON_BLEND_OUT_SMOOTHING;

			if (LockonTarget != null)
			{
				if (LockonTarget.IsInsideTree()) //Validate lockon target
				{
					targetBlend = MAX_LOCKON_BLEND;
					smoothing = LOCKON_BLEND_IN_SMOOTHING;
				}
				else
					LockonTarget = null; //Invalid LockonTarget
			}

			lockonBlend = ExtensionMethods.SmoothDamp(lockonBlend, targetBlend, ref lockonBlendVelocity, smoothing * PhysicsManager.physicsDelta);
		}

		private void UpdateGameplayCamera()
		{
			UpdateTransitionTimer();
			UpdateLockonTarget();

			CameraPositionData data = new CameraPositionData()
			{
				offsetBasis = Basis.Identity,
				blendData = ActiveBlendData
			};

			float distance = 0;
			float staticBlendRatio = 0; //Blend value of whether to use static camera positions or not
			Vector2 viewportOffset = Vector2.Zero;
			Vector3 staticPosition = Vector3.Zero;

			for (int i = 0; i < CameraBlendList.Count; i++) //Simulate each blend data separately
			{
				CameraPositionData iData = CalculateCameraTransform(i);
				data.offsetBasis = data.offsetBasis.Slerp(iData.offsetBasis, CameraBlendList[i].SmoothedInfluence);
				distance = Mathf.Lerp(distance, iData.blendData.distance, CameraBlendList[i].SmoothedInfluence);

				data.precalculatedPosition = data.precalculatedPosition.Lerp(iData.precalculatedPosition, CameraBlendList[i].SmoothedInfluence);

				data.yawTracking = Mathf.LerpAngle(data.yawTracking, iData.yawTracking, CameraBlendList[i].SmoothedInfluence);
				data.pitchTracking = Mathf.Lerp(data.pitchTracking, iData.pitchTracking, CameraBlendList[i].SmoothedInfluence);

				data.horizontalTrackingOffset = Mathf.Lerp(data.horizontalTrackingOffset, iData.horizontalTrackingOffset, CameraBlendList[i].SmoothedInfluence);
				data.verticalTrackingOffset = Mathf.Lerp(data.verticalTrackingOffset, iData.verticalTrackingOffset, CameraBlendList[i].SmoothedInfluence);

				staticBlendRatio = Mathf.Lerp(staticBlendRatio, CameraBlendList[i].SettingsResource.isStaticCamera ? 1 : 0, CameraBlendList[i].SmoothedInfluence);
				viewportOffset = viewportOffset.Lerp(CameraBlendList[i].SettingsResource.viewportOffset, CameraBlendList[i].SmoothedInfluence);
			}

			//Recalculate non-static camera positions for better transition rotations.
			Vector3 position = data.offsetBasis.Z.Normalized() * distance;
			position += Character.CenterPosition;

			Transform3D cameraTransform = new Transform3D(data.offsetBasis, position.Lerp(data.precalculatedPosition, staticBlendRatio));
			cameraTransform = cameraTransform.RotatedLocal(Vector3.Up, data.yawTracking);
			cameraTransform = cameraTransform.RotatedLocal(Vector3.Right, data.pitchTracking);
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
			position += PathFollower.Right() * data.horizontalTrackingOffset;
			position += PathFollower.Up() * data.verticalTrackingOffset; //Use Pathfollower's up axis for vertical offset
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
				blendData = CameraBlendList[index],
			};

			float targetYawAngle = settings.yawAngle;
			float targetPitchAngle = settings.pitchAngle;

			if (!settings.isStaticCamera)
			{
				//Calculate distance
				float targetDistance = settings.distance;
				if (Character.IsMovingBackward)
				{
					if (PathFollower.Progress < settings.backstepDistance)
						targetDistance += settings.backstepDistance - (settings.backstepDistance - PathFollower.Progress);
					else
						targetDistance += settings.backstepDistance;
				}
				data.blendData.DistanceSmoothDamp(targetDistance, SnapFlag);

				//Calculate targetAngles when DistanceMode is set to Sample.
				float sampledTargetYawAngle = targetYawAngle;
				float sampledTargetPitchAngle = targetPitchAngle;

				float currentProgress = PathFollower.Progress; //Cache progress
				PathFollower.Progress -= data.blendData.distance;
				Vector3 sampledPosition = PathFollower.GlobalPosition;
				PathFollower.Progress = currentProgress; //Revert progress
				Vector3 sampledForward = (PathFollower.GlobalPosition - sampledPosition).Normalized();

				if (settings.yawOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
					sampledTargetYawAngle += Character.CalculateForwardAngle(sampledForward);
				if (settings.yawOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
					sampledTargetPitchAngle += sampledForward.AngleTo(sampledForward.RemoveVertical().Normalized()) * Mathf.Sign(sampledForward.Y);

				//Calculate target angles when DistanceMode is set to Offset
				if (settings.yawOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
					targetYawAngle += PathFollower.ForwardAngle;
				if (settings.pitchOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
					targetPitchAngle += PathFollower.Forward().AngleTo(PathFollower.Forward().RemoveVertical().Normalized()) * Mathf.Sign(PathFollower.Forward().Y);

				if (settings.distanceCalculationMode == CameraSettingsResource.DistanceModeEnum.Auto) //Fixes slope changes
				{
					//Negative number -> Concave, Positive number -> Convex.
					float slopeDifference = sampledForward.Y - PathFollower.Forward().Y;
					if (Mathf.Abs(slopeDifference) > .01f) //Deadzone to prevent jittering
						data.blendData.SampleBlend = slopeDifference < 0 ? 1.0f : 0.0f;
				}
				else if (settings.distanceCalculationMode == CameraSettingsResource.DistanceModeEnum.Sample)
					data.blendData.SampleBlend = 1.0f;
				else
					data.blendData.SampleBlend = 0.0f;

				//Interpolate angles
				data.blendData.yawAngle = Mathf.LerpAngle(targetYawAngle, sampledTargetYawAngle, data.blendData.SampleBlend);
				data.blendData.pitchAngle = Mathf.Lerp(targetPitchAngle, sampledTargetPitchAngle, data.blendData.SampleBlend);
				if (settings.followPathTilt) //Calculate tilt
					data.blendData.tiltAngle = PathFollower.Right().SignedAngleTo(-PathFollower.RightAxis, PathFollower.Forward()); //Update tilt

				//Update Tracking
				//Calculate position for tracking calculations
				data.CalculateBasis();
				data.CalculatePosition(PathFollower.GlobalPosition);

				Vector3 globalDelta = Character.CenterPosition - data.precalculatedPosition;
				Vector3 delta = data.offsetBasis.Inverse() * globalDelta;

				if (settings.horizontalTrackingMode != CameraSettingsResource.TrackingModeEnum.Move)
				{
					data.horizontalTrackingOffset = -PathFollower.GlobalPlayerPositionDelta.X;

					if (settings.horizontalTrackingMode == CameraSettingsResource.TrackingModeEnum.Rotate)
						data.yawTracking = delta.Normalized().Flatten().AngleTo(Vector2.Up);
				}
				else if (!Mathf.IsZeroApprox(settings.hallWidth)) //Process hall width
				{
					float positionTracking = Mathf.Clamp(PathFollower.GlobalPlayerPositionDelta.X, -settings.hallWidth, settings.hallWidth);
					data.blendData.HallSmoothDamp(positionTracking, SnapFlag);

					data.horizontalTrackingOffset = -PathFollower.GlobalPlayerPositionDelta.X; //Recenter
					data.horizontalTrackingOffset += data.blendData.hallPosition; //Add clamped position tracking

					if (settings.isHallRotationEnabled)
					{
						float rotationTracking = (delta + Vector3.Right * data.blendData.hallPosition).Normalized().Flatten().AngleTo(Vector2.Up);
						data.yawTracking = rotationTracking;
					}
				}

				float targetPitchTracking = 0.0f;
				if (settings.verticalTrackingMode != CameraSettingsResource.TrackingModeEnum.Move)
				{
					//Stay on the floor
					data.verticalTrackingOffset = -PathFollower.GlobalPlayerPositionDelta.Y;

					if (settings.verticalTrackingMode == CameraSettingsResource.TrackingModeEnum.Rotate) //Rotational tracking
					{
						delta.X = 0; //Ignore x axis for pitch tracking
						data.pitchTracking = delta.Normalized().AngleTo(delta.RemoveVertical().Normalized()) * Mathf.Sign(delta.Y);
						targetPitchTracking = data.pitchTracking;
					}
				}

				//Track lockon
				if (LockonTarget != null) //Update lockon tracking
				{
					data.CalculatePosition(Character.CenterPosition);
					globalDelta = LockonTarget.GlobalPosition - data.precalculatedPosition;
					data.lockonPitchTracking = globalDelta.Normalized().AngleTo(globalDelta.RemoveVertical().Normalized()) * Mathf.Sign(globalDelta.Y);
				}

				data.pitchTracking = Mathf.Lerp(targetPitchTracking, data.lockonPitchTracking, lockonBlend);

				//Recalculate position after applying rotational tracking
				data.CalculatePosition(Character.CenterPosition);
				data.precalculatedPosition = AddTrackingOffset(data.precalculatedPosition, data);
			}
			else
			{
				data.precalculatedPosition = data.blendData.StaticPosition;

				Vector3 delta = Character.CenterPosition - data.precalculatedPosition;
				data.blendData.distance = delta.Length();
				delta = delta.Normalized();

				if (settings.yawOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
					targetYawAngle += delta.Flatten().AngleTo(Vector2.Up) + Mathf.Pi;
				if (settings.pitchOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
					targetPitchAngle += delta.AngleTo(delta.RemoveVertical().Normalized()) * Mathf.Sign(delta.Y);

				data.blendData.yawAngle = targetYawAngle;
				data.blendData.pitchAngle = targetPitchAngle;
				data.CalculateBasis();
			}

			if (!data.blendData.WasInitialized)
				data.blendData.WasInitialized = true;

			return data;
		}

		private struct CameraPositionData
		{
			/// <summary> Rotation data used for offset calculation. </summary>
			public Basis offsetBasis;
			public void CalculateBasis()
			{
				offsetBasis = Basis.Identity.Rotated(Vector3.Up, Mathf.Pi);
				offsetBasis = offsetBasis.Rotated(Vector3.Up, blendData.yawAngle);
				offsetBasis = offsetBasis.Rotated(offsetBasis.X.Normalized(), blendData.pitchAngle);
				offsetBasis = offsetBasis.Rotated(offsetBasis.Z.Normalized(), blendData.tiltAngle);
			}

			/// <summary> Yaw rotation data used for tracking. </summary>
			public float yawTracking;
			/// <summary> Pitch rotation data used for tracking. </summary>
			public float pitchTracking;

			/// <summary> Last frame's lockon pitch tracking </summary>
			public float lockonPitchTracking;

			/// <summary> How much to move camera for horizontal tracking. </summary>
			public float horizontalTrackingOffset;
			/// <summary> How much to move camera for vertical tracking. </summary>
			public float verticalTrackingOffset;

			/// <summary> Only used when blending with a static camera. </summary>
			public Vector3 precalculatedPosition;
			public void CalculatePosition(Vector3 referencePosition)
			{
				precalculatedPosition = offsetBasis.Z.Normalized() * blendData.distance;
				precalculatedPosition += referencePosition;
			}

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

		private bool isFreeCamLocked;
		private Vector3 freeCamLockedPosition;

		private void UpdateFreeCam()
		{
			if (Input.IsActionJustPressed("debug_free_cam_reset"))
			{
				isFreeCamLocked = false;
				isFreeCamEnabled = freeCamRotating = false;
				camera.Transform = Transform3D.Identity;
				GD.Print($"Free cam disabled.");
			}

			if (Input.IsActionJustPressed("debug_free_cam_lock"))
			{
				isFreeCamLocked = !isFreeCamLocked;
				freeCamLockedPosition = camera.GlobalPosition;
				GD.Print($"Free cam lock set to {isFreeCamLocked}.");
			}

			freeCamRotating = Input.IsMouseButtonPressed(MouseButton.Left);
			freeCamTilting = Input.IsMouseButtonPressed(MouseButton.Right);
			if (freeCamRotating || freeCamTilting)
			{
				isFreeCamEnabled = true;
				Input.MouseMode = Input.MouseModeEnum.Captured;
			}
			else
				Input.MouseMode = Input.MouseModeEnum.Visible;

			//Update visibilities
			debugMesh.Visible = isFreeCamEnabled;
			PathFollower.Visible = isFreeCamEnabled;
			Character.PathFollower.Visible = isFreeCamEnabled;

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

			if (isFreeCamLocked)
				camera.GlobalPosition = freeCamLockedPosition;
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

		/// <summary> Hall tracking position. </summary>
		public float hallPosition;
		/// <summary> Hall tracking velocity. </summary>
		private float hallVelocity;
		public const float HALL_SMOOTHING = 5.0f;
		public void HallSmoothDamp(float target, bool snap)
		{
			if (snap || !WasInitialized)
			{
				hallPosition = target;
				hallVelocity = 0;
				return;
			}

			hallPosition = ExtensionMethods.SmoothDamp(hallPosition, target, ref hallVelocity, HALL_SMOOTHING * PhysicsManager.physicsDelta);
		}

		/// <summary> [0 -> 1] Blend between offset and sample. </summary>
		public float SampleBlend { get; set; }

		/// <summary> How long blending takes in seconds. </summary>
		public float BlendTime { get; set; }
		/// <summary> Camera's static position. Only used when CameraSettingsResource.isStaticCamera is true. </summary>
		public Vector3 StaticPosition { get; set; }

		/// <summary> Current pitch angle. </summary>
		public float pitchAngle;
		/// <summary> Current yaw angle. </summary>
		public float yawAngle;
		/// <summary> Current tilt angle. </summary>
		public float tiltAngle;

		/// <summary> How far the camera should be. </summary>
		public float distance;
		/// <summary> Distance smoothdamp velocity. </summary>
		private float distanceVelocity;
		public const float DISTANCE_SMOOTHING = 20.0f;

		public void DistanceSmoothDamp(float target, bool snap)
		{
			if (snap || !WasInitialized)
			{
				distance = target;
				distanceVelocity = 0;
				return;
			}

			distance = ExtensionMethods.SmoothDamp(distance, target, ref distanceVelocity, DISTANCE_SMOOTHING * PhysicsManager.physicsDelta);
		}

		/// <summary> Has this blend data been processed before? </summary>
		public bool WasInitialized { get; set; }

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