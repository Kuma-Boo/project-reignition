using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Responsible for playing the player's animations.
	/// </summary>
	public partial class CharacterAnimator : Node3D
	{
		[Export]
		private AnimationTree animationTree;
		[Export]
		private AnimationPlayer eventAnimationPlayer;
		private CharacterController Character => CharacterController.instance;

		/// <summary> Reference to the root blend tree of the animation tree. </summary>
		private AnimationNodeBlendTree animationRoot;
		/// <summary> Transition node for switching between states (normal, balancing, sidling, etc). </summary>
		private AnimationNodeTransition stateTransition;
		/// <summary> Transition node for switching between ground and air trees. </summary>
		private AnimationNodeTransition groundTransition;

		/// <summary> Determines facing directions for certain animation actions. </summary>
		private bool isFacingRight;

		// For toggle transitions
		private readonly StringName ENABLED_CONSTANT = "enabled";
		private readonly StringName DISABLED_CONSTANT = "disabled";
		// For directional transitions
		private readonly StringName RIGHT_CONSTANT = "right";
		private readonly StringName LEFT_CONSTANT = "left";


		public override void _EnterTree()
		{
			animationTree.Active = true; // Activate animator

			animationRoot = animationTree.TreeRoot as AnimationNodeBlendTree;
			stateTransition = animationRoot.GetNode("state_transition") as AnimationNodeTransition;
			groundTransition = animationRoot.GetNode("ground_transition") as AnimationNodeTransition;
			oneShotTransition = animationRoot.GetNode("oneshot_trigger") as AnimationNodeOneShot;
		}


		/// <summary>
		/// Called every frame. Only updates normal animations and visual rotation.
		/// </summary>
		public void UpdateAnimation()
		{
			if (Character.IsOnGround)
				GroundAnimations();
			else
				AirAnimations();

			UpdateVisualRotation();
			UpdateShaderVariables();
		}

		#region Oneshot Animations
		private AnimationNodeOneShot oneShotTransition;
		/// <summary> Animation index for countdown animation. </summary>
		private readonly StringName COUNTDOWN_ANIMATION = "countdown";

		public void PlayCountdown()
		{
			PlayOneshotAnimation(COUNTDOWN_ANIMATION);

			// Prevent sluggish transitions into gameplay
			DisabledSpeedSmoothing = true;
			oneShotTransition.FadeInTime = oneShotTransition.FadeOutTime = 0;
		}

		private readonly StringName ONESHOT_TRIGGER = "parameters/oneshot_trigger/request";
		private readonly StringName ONESHOT_SEEK_PARAMETER = "parameters/oneshot_tree/oneshot_seek/current";
		private readonly StringName ONESHOT_TRANSITION_PARAMETER = "parameters/oneshot_tree/oneshot_transition/transition_request";
		public void PlayOneshotAnimation(StringName animation) //Play a specific one-shot animation
		{
			animationTree.Set(ONESHOT_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			animationTree.Set(ONESHOT_SEEK_PARAMETER, 0);
			animationTree.Set(ONESHOT_TRANSITION_PARAMETER, animation);
		}

		/// <summary>
		/// Cancels the oneshot animation early.
		/// </summary>
		public void CancelOneshot(float fadeout = 0)
		{
			oneShotTransition.FadeOutTime = fadeout;

			// Abort accidental landing animations
			if (!Character.JustLandedOnGround || Mathf.IsZeroApprox(fadeout))
				animationTree.Set(LAND_TRIGGER_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Abort);
			else
				animationTree.Set(ONESHOT_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.FadeOut);
		}
		#endregion


		#region States
		private readonly StringName NORMAL_STATE = "normal";
		private readonly StringName DRIFT_STATE = "drift";
		private readonly StringName BALANCE_STATE = "balance";
		private readonly StringName SIDLE_STATE = "sidle";

		private readonly StringName STATE_TRANSITION = "parameters/state_transition/transition_request";

		public void ResetState(float xfadeTime = -1) // Reset any state, while optionally setting the xfade time
		{
			if (xfadeTime != -1)
				SetStateXfade(xfadeTime);

			animationTree.Set(STATE_TRANSITION, NORMAL_STATE); // Revert to normal state
		}

		/// <summary>
		/// Sets the crossfade length of the primary state transition node.
		/// </summary>
		private void SetStateXfade(float xfadeTime) => stateTransition.XfadeTime = xfadeTime;
		#endregion


		#region Normal Animations
		private readonly StringName IDLE_BLEND_PARAMETER = "parameters/ground_tree/idle_blend/blend_amount";
		private readonly StringName FORWARD_SEEK_PARAMETER = "parameters/ground_tree/forward_seek/seek_request";
		private readonly StringName BACKWARD_SEEK_PARAMETER = "parameters/ground_tree/backward_seek/seek_request";

		private readonly StringName GROUND_SPEED_PARAMETER = "parameters/ground_tree/ground_speed/scale";
		private readonly StringName GROUND_SEEK_PARAMETER = "parameters/ground_tree/ground_seek/seek_request";
		private readonly StringName FORWARD_BLEND_PARAMETER = "parameters/ground_tree/forward_blend/blend_position";

		private readonly StringName TURN_BLEND_PARAMETER = "parameters/ground_tree/turn_blend/blend_position";
		private readonly StringName LAND_TRIGGER_PARAMETER = "parameters/ground_tree/land_trigger/request";

		private readonly StringName SPEEDBREAK_TRIGGER_PARAMETER = "parameters/ground_tree/speedbreak_trigger/request";

		[Export]
		private Curve movementAnimationSpeedCurve;
		/// <summary> Disables speed smoothing. </summary>
		public bool DisabledSpeedSmoothing { get; set; }
		private float idleBlendVelocity;
		/// <summary> What speedratio should be considered as fully running? </summary>
		private const float RUN_RATIO = .9f;
		/// <summary> How much should the animation speed be smoothed by? </summary>
		private const float SPEED_SMOOTHING = .06f;
		/// <summary> How much should the transition from idling be smoothed by? </summary>
		private const float IDLE_SMOOTHING = .05f;


		/// <summary> Forces the player's animation back to the grounded state. </summary>
		private readonly StringName GROUND_TRANSITION = "parameters/ground_transition/transition_request";
		public void SnapToGround()
		{
			groundTransition.XfadeTime = 0.0f;
			animationTree.Set(GROUND_TRANSITION, ENABLED_CONSTANT);
			ResetGroundTree();
		}


		private void ResetGroundTree()
		{
			DisabledSpeedSmoothing = true;
			animationTree.Set(GROUND_SEEK_PARAMETER, 0);
			animationTree.Set(LAND_TRIGGER_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Abort);
			animationTree.Set(CROUCH_TRANSITION, DISABLED_CONSTANT);
		}


		public void SpeedBreak()
		{
			animationTree.Set(SPEEDBREAK_TRIGGER_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}

		private void GroundAnimations()
		{
			Character.Effect.IsEmittingStepDust = !Mathf.IsZeroApprox(Character.MoveSpeed); // Emit step dust based on speed

			if (Character.Skills.IsSpeedBreakCharging) return;

			float idleBlend = (float)animationTree.Get(IDLE_BLEND_PARAMETER);
			float speedRatio = Mathf.Abs(Character.GroundSettings.GetSpeedRatio(Character.MoveSpeed));
			float targetAnimationSpeed = 1f;
			groundTurnRatio = 0;

			if (Character.JustLandedOnGround) // Play landing animation
			{
				ResetGroundTree();
				animationTree.Set(LAND_TRIGGER_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
				groundTransition.XfadeTime = .05f;
				animationTree.Set(GROUND_TRANSITION, ENABLED_CONSTANT);
				StopHurt();
			}

			if (Character.IsLockoutActive && Character.ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe &&
				speedRatio < .5f)
				speedRatio = Character.GroundSettings.GetSpeedRatio(Character.MoveSpeed);

			if (!Mathf.IsZeroApprox(Character.MoveSpeed))
			{
				if (Character.IsMovingBackward) // Backstep
				{
					if (DisabledSpeedSmoothing)
						idleBlend = -1;
					else
						idleBlend = ExtensionMethods.SmoothDamp(idleBlend, -1, ref idleBlendVelocity, IDLE_SMOOTHING);

					speedRatio = Mathf.Abs(Character.BackstepSettings.GetSpeedRatio(Character.MoveSpeed));
					targetAnimationSpeed = 1.2f + speedRatio;
				}
				else // Moving forward
				{
					if (DisabledSpeedSmoothing)
						idleBlend = 1;
					else
						idleBlend = ExtensionMethods.SmoothDamp(idleBlend, 1, ref idleBlendVelocity, IDLE_SMOOTHING);

					if (Character.Skills.IsSpeedBreakActive) // Constant animation speed
						targetAnimationSpeed = 2.5f;
					else if (speedRatio >= RUN_RATIO) // Running
					{
						float extraSpeed = Mathf.Clamp((speedRatio - RUN_RATIO) / .2f, 0f, 1f);
						targetAnimationSpeed = 2f + extraSpeed;
					}
					else // Jogging
					{
						targetAnimationSpeed = movementAnimationSpeedCurve.Sample(speedRatio / RUN_RATIO); //Normalize speed ratio

						// Speed up animation if player is trying to start running
						if (Character.InputVector.Length() >= .8f &&
							speedRatio < Character.GroundSettings.GetSpeedRatio(Character.BackstepSettings.speed) && !Character.IsOnWall())
							targetAnimationSpeed += 1.0f;
					}
				}

				if (Character.MovementState == CharacterController.MovementStates.External) //Disable turning when controlled externally
					groundTurnRatio = 0;
				else if (!Character.IsLockoutActive || Character.ActiveLockoutData.movementMode != LockoutResource.MovementModes.Replace)
					groundTurnRatio = CalculateTurnRatio();
			}
			else
			{
				idleBlend = ExtensionMethods.SmoothDamp(idleBlend, 0, ref idleBlendVelocity, IDLE_SMOOTHING);

				if (Mathf.IsZeroApprox(idleBlend))
				{
					animationTree.Set(BACKWARD_SEEK_PARAMETER, 0);
					animationTree.Set(FORWARD_SEEK_PARAMETER, 0);
				}
			}

			animationTree.Set(IDLE_BLEND_PARAMETER, idleBlend);
			animationTree.Set(FORWARD_BLEND_PARAMETER, speedRatio);
			if (DisabledSpeedSmoothing)
			{
				animationTree.Set(GROUND_SPEED_PARAMETER, targetAnimationSpeed);
				DisabledSpeedSmoothing = false;
			}
			else
				animationTree.Set(GROUND_SPEED_PARAMETER, Mathf.Lerp((float)animationTree.Get(GROUND_SPEED_PARAMETER), targetAnimationSpeed, SPEED_SMOOTHING));

			groundTurnRatio = Mathf.Lerp(((Vector2)animationTree.Get(TURN_BLEND_PARAMETER)).X, groundTurnRatio, TURN_SMOOTHING); // Blend from animator
			animationTree.Set(TURN_BLEND_PARAMETER, new Vector2(groundTurnRatio, Character.IsMovingBackward ? 0 : speedRatio));
		}


		/// <summary> Blend from -1 <-> 1 of how much the player is turning. </summary>
		private float groundTurnRatio;
		/// <summary> How much should the turning animation be smoothed by? </summary>
		private const float TURN_SMOOTHING = .1f;
		/// <summary> Max amount of turning allowed. </summary>
		private readonly float MAX_TURN_ANGLE = Mathf.Pi * .4f;
		/// <summary> How much to visually lean into turns. </summary>
		private readonly float PATH_TURN_STRENGTH = 15.0f;
		/// <summary> Calculates turn ratio based on current input with -1 being left and 1 being right. </summary>
		public float CalculateTurnRatio()
		{
			float referenceAngle = Character.IsMovingBackward ? Character.PathFollower.ForwardAngle : Character.MovementAngle;
			float inputAngle = Character.GetInputAngle() + Character.PathFollower.DeltaAngle * PATH_TURN_STRENGTH;
			float delta = ExtensionMethods.SignedDeltaAngleRad(referenceAngle, inputAngle);

			if (ExtensionMethods.DotAngle(referenceAngle, inputAngle) < 0) //Input is backwards
				delta = -ExtensionMethods.SignedDeltaAngleRad(referenceAngle + Mathf.Pi, inputAngle);

			delta = Mathf.Clamp(delta, -MAX_TURN_ANGLE, MAX_TURN_ANGLE);
			return delta / MAX_TURN_ANGLE;
		}


		public bool IsFallTransitionEnabled { get; set; }
		private readonly StringName FALL_TRIGGER = "parameters/air_tree/fall_trigger/request";
		private readonly StringName FALL_SPEED = "parameters/air_tree/fall_speed/scale";
		private readonly StringName AIR_STATE_TRANSITION = "parameters/air_tree/state_transition/transition_request";
		private readonly StringName FALL_STATE_PARAMETER = "fall";
		private void UpdateAirState(StringName state, bool enableFallTransition)
		{
			IsFallTransitionEnabled = enableFallTransition;
			animationTree.Set(AIR_STATE_TRANSITION, state);
			animationTree.Set(FALL_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.Abort);
			animationTree.Set(BOUNCE_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.Abort);
			animationTree.Set(BACKFLIP_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		}

		public void JumpAnimation() => UpdateAirState("jump", true);
		public void JumpAccelAnimation() => UpdateAirState("accel", false);
		public void LaunchAnimation() => UpdateAirState("launch", false);

		public void StompAnimation(bool offensive)
		{
			UpdateAirState(FALL_STATE_PARAMETER, false);
			if (offensive)
			{
				// TODO Separate stomp animation
			}
			else
			{
				animationTree.Set(FALL_SPEED, 2.5f);
				animationTree.Set(FALL_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			}
		}


		private readonly StringName BACKFLIP_TRIGGER = "parameters/air_tree/backflip_trigger/request";
		public void BackflipAnimation() => animationTree.Set(BACKFLIP_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.Fire);


		private readonly StringName BOUNCE_TRANSITION = "parameters/air_tree/bounce_transition/transition_request";
		private readonly StringName BOUNCE_TRIGGER = "parameters/air_tree/bounce_trigger/request";
		private const int BOUNCE_VARIATION_COUNT = 4;
		public void BounceTrick()
		{
			UpdateAirState(FALL_STATE_PARAMETER, false);
			animationTree.Set(BOUNCE_TRANSITION, Runtime.randomNumberGenerator.RandiRange(1, BOUNCE_VARIATION_COUNT).ToString());
			animationTree.Set(BOUNCE_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}


		private void AirAnimations()
		{
			Character.Effect.IsEmittingStepDust = false;
			animationTree.Set(GROUND_TRANSITION, DISABLED_CONSTANT);

			if (IsFallTransitionEnabled)
			{
				if (Character.MovementState == CharacterController.MovementStates.Launcher) return;

				if (Character.ActionState != CharacterController.ActionStates.Jumping ||
				Character.VerticalSpeed <= 0)
				{
					UpdateAirState(FALL_STATE_PARAMETER, false);
					animationTree.Set(FALL_SPEED, 1.0f);
					animationTree.Set(FALL_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
				}
			}
		}


		private AnimationNodeStateMachinePlayback CrouchStatePlayback => animationTree.Get(CROUCH_PLAYBACK_PARAMETER).Obj as AnimationNodeStateMachinePlayback;
		private readonly StringName CROUCH_PLAYBACK_PARAMETER = "parameters/ground_tree/crouch_state/playback";

		private readonly StringName CROUCH_STATE_START = "crouch-start";
		private readonly StringName CROUCH_STATE_STOP = "crouch-stop";

		private readonly StringName SLIDE_STATE_START = "slide-start";
		private readonly StringName SLIDE_STATE_STOP = "slide-stop";
		private readonly StringName CROUCH_TRANSITION = "parameters/ground_tree/crouch_transition/transition_request";
		public void StartCrouching()
		{
			if (Character.ActionState == CharacterController.ActionStates.Sliding)
				CrouchStatePlayback.Travel(SLIDE_STATE_START);
			else
				CrouchStatePlayback.Travel(CROUCH_STATE_START);
			animationTree.Set(CROUCH_TRANSITION, ENABLED_CONSTANT);
		}

		public void ToggleSliding()
		{
			if (Character.ActionState == CharacterController.ActionStates.Sliding)
				CrouchStatePlayback.Travel(SLIDE_STATE_START);
			else
				CrouchStatePlayback.Travel(SLIDE_STATE_STOP);
		}

		public void StopCrouching()
		{
			CrouchStatePlayback.Travel(CROUCH_STATE_STOP);
			animationTree.Set(CROUCH_TRANSITION, DISABLED_CONSTANT);
		}


		public void StartInvincibility()
		{
			eventAnimationPlayer.Play("invincibility");
			eventAnimationPlayer.Seek(0.0, true);
		}

		public void StartTeleport()
		{
			eventAnimationPlayer.Play("teleport-start");
			eventAnimationPlayer.Seek(0.0, true);
		}

		public void StopTeleport()
		{
			eventAnimationPlayer.Play("teleport-end");
			eventAnimationPlayer.Seek(0.0, true);
		}
		#endregion


		#region Visual Rotation
		/// <summary> Angle to use when character's MovementState is CharacterController.MovementStates.External. </summary>
		public float ExternalAngle { get; set; }
		/// <summary> Rotation (in radians) currently applied to Transform. </summary>
		public float VisualAngle { get; private set; }
		private float rotationVelocity;

		/// <summary> Rotation smoothing amount for movement. </summary>
		private readonly float MOVEMENT_ROTATION_SMOOTHING = .1f;

		/// <summary>
		/// Snaps visual rotation, without any smoothing applied.
		/// </summary>
		public void SnapRotation(float angle)
		{
			VisualAngle = angle;
			rotationVelocity = 0;
			ApplyVisualRotation();
		}

		/// <summary>
		/// Calculates the target visual rotation and applies it.
		/// </summary>
		private void UpdateVisualRotation()
		{
			if (Character.ActionState == CharacterController.ActionStates.Grindstep) return; // Use the same angle as the grindrail

			// Don't update directions when externally controlled or on launchers
			float targetRotation = Character.MovementAngle;

			if (Character.MovementState == CharacterController.MovementStates.External)
				targetRotation = ExternalAngle;
			else if (Character.Lockon.IsHomingAttacking) // Face target
				targetRotation = ExtensionMethods.CalculateForwardAngle(Character.Lockon.HomingAttackDirection);
			else if (Character.IsMovingBackward) // Backstepping
				targetRotation = Character.PathFollower.ForwardAngle + groundTurnRatio * Mathf.Pi * .15f;
			else if (Character.IsLockoutActive && Character.ActiveLockoutData.recenterPlayer)
				targetRotation = Character.PathFollower.ForwardAngle;

			if (Character.Skills.IsSpeedBreakActive && Character.MovementState != CharacterController.MovementStates.External)
				VisualAngle += Character.PathFollower.DeltaAngle;

			VisualAngle = ExtensionMethods.ClampAngleRange(VisualAngle, Character.PathFollower.ForwardAngle, Mathf.Pi);
			VisualAngle = ExtensionMethods.SmoothDampAngle(VisualAngle, targetRotation, ref rotationVelocity, MOVEMENT_ROTATION_SMOOTHING);
			ApplyVisualRotation();
		}

		/// <summary>
		/// Apply currentRotation on Transform.
		/// </summary>
		private void ApplyVisualRotation() => Rotation = Vector3.Up * VisualAngle;
		#endregion


		#region Drift
		private AnimationNodeStateMachinePlayback ActiveDriftState => isFacingRight ? DriftRightState : DriftLeftState;
		private AnimationNodeStateMachinePlayback DriftRightState => animationTree.Get(DRIFT_RIGHT_PLAYBACK).Obj as AnimationNodeStateMachinePlayback;
		private AnimationNodeStateMachinePlayback DriftLeftState => animationTree.Get(DRIFT_LEFT_PLAYBACK).Obj as AnimationNodeStateMachinePlayback;

		private readonly StringName DRIFT_LEFT_PLAYBACK = "parameters/drift_tree/left_state/playback";
		private readonly StringName DRIFT_RIGHT_PLAYBACK = "parameters/drift_tree/right_state/playback";

		private readonly StringName DRIFT_DIRECTION_TRANSITION = "parameters/drift_tree/direction_transition/transition_request";
		private readonly StringName DRIFT_START_STATE = "drift-start";
		private readonly StringName DRIFT_LAUNCH_STATE = "drift-launch";

		public void StartDrift(bool isDriftFacingRight)
		{
			isFacingRight = isDriftFacingRight;
			ActiveDriftState.Start(DRIFT_START_STATE);
			animationTree.Set(DRIFT_DIRECTION_TRANSITION, isFacingRight ? RIGHT_CONSTANT : LEFT_CONSTANT);

			SetStateXfade(.2f); // Transition into drift
			animationTree.Set(STATE_TRANSITION, DRIFT_STATE);
		}

		/// <summary> Called when drift is performed. </summary>
		public void LaunchDrift()
		{
			ActiveDriftState.Travel(DRIFT_LAUNCH_STATE);
			SetStateXfade(0f); // Remove xfade in case player wants to jump early
		}
		#endregion


		#region Grinding and Balancing Animations
		/// <summary> Reference to the balance state's StateMachinePlayback </summary>
		private AnimationNodeStateMachinePlayback BalanceState => animationTree.Get(BALANCE_PLAYBACK).Obj as AnimationNodeStateMachinePlayback;
		private readonly StringName BALANCE_PLAYBACK = "parameters/balance_tree/balance_state/playback";

		/// <summary> Reference to the balance state's StateMachinePlayback </summary>
		private AnimationNodeStateMachinePlayback GrindStepState => animationTree.Get(GRINDSTEP_PLAYBACK).Obj as AnimationNodeStateMachinePlayback;
		private readonly StringName GRINDSTEP_PLAYBACK = "parameters/balance_tree/grindstep_state/playback";

		/// <summary> Is the shuffling animation currently active? </summary>
		public bool IsBalanceShuffleActive { get; private set; }

		private readonly StringName SHUFFLE_RIGHT_PARAMETER = "balance-right-shuffle";
		private readonly StringName SHUFFLE_LEFT_PARAMETER = "balance-left-shuffle";
		private readonly StringName BALANCE_RIGHT_PARAMETER = "balance_right_blend";
		private readonly StringName BALANCE_LEFT_PARAMETER = "balance_left_blend";

		private readonly StringName BALANCE_RIGHT_LEAN_PARAMETER = "parameters/balance_tree/balance_state/balance_right_blend/blend_position";
		private readonly StringName BALANCE_LEFT_LEAN_PARAMETER = "parameters/balance_tree/balance_state/balance_left_blend/blend_position";

		public void StartBalancing()
		{
			IsBalanceShuffleActive = true;
			isFacingRight = true; //Default to facing right
			BalanceState.Start(SHUFFLE_RIGHT_PARAMETER, true); //Start with a shuffle

			//Reset current balance
			animationTree.Set(BALANCE_LEFT_LEAN_PARAMETER, 0);
			animationTree.Set(BALANCE_RIGHT_LEAN_PARAMETER, 0);

			SetStateXfade(0.05f);
			animationTree.Set(STATE_TRANSITION, BALANCE_STATE); //Turn on balancing animations
			animationTree.Set(BALANCE_GRINDSTEP_TRIGGER_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.FadeOut); //Disable any grindstepping
		}

		private readonly StringName BALANCE_GRINDSTEP_TRIGGER_PARAMETER = "parameters/balance_tree/grindstep_trigger/request";
		/// <summary> How many variations of the grindstep animation are there? </summary>
		private readonly int GRINDSTEP_VARIATION_COUNT = 3;
		public void StartGrindStep()
		{
			int index = Core.Runtime.randomNumberGenerator.RandiRange(1, GRINDSTEP_VARIATION_COUNT);
			string targetPose = isFacingRight ? "step-right-0" : "step-left-0";
			GrindStepState.Start(targetPose + index.ToString(), true);
			animationTree.Set(BALANCE_GRINDSTEP_TRIGGER_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}

		public void StartGrindShuffle()
		{
			IsBalanceShuffleActive = true;
			isFacingRight = !isFacingRight;
			BalanceState.Travel(isFacingRight ? SHUFFLE_RIGHT_PARAMETER : SHUFFLE_LEFT_PARAMETER);
		}

		private float balanceTurnVelocity;
		/// <summary> How much should the balancing animation be smoothed by? </summary>
		private const float BALANCE_TURN_SMOOTHING = .15f;
		public void UpdateBalancing(float balanceRatio)
		{
			StringName currentNode = BalanceState.GetCurrentNode();
			IsBalanceShuffleActive = currentNode == SHUFFLE_LEFT_PARAMETER || currentNode == SHUFFLE_RIGHT_PARAMETER;
			if (IsBalanceShuffleActive)
			{
				if ((isFacingRight && currentNode == BALANCE_RIGHT_PARAMETER) ||
				(!isFacingRight && currentNode == BALANCE_LEFT_PARAMETER))
					IsBalanceShuffleActive = false;

				balanceRatio = 0;
			}

			balanceRatio = ExtensionMethods.SmoothDamp((float)animationTree.Get(BALANCE_RIGHT_LEAN_PARAMETER), balanceRatio, ref balanceTurnVelocity, BALANCE_TURN_SMOOTHING);
			animationTree.Set(BALANCE_RIGHT_LEAN_PARAMETER, balanceRatio);
			animationTree.Set(BALANCE_LEFT_LEAN_PARAMETER, -balanceRatio);
		}

		private readonly StringName BALANCE_SPEED_PARAMETER = "parameters/balance_tree/balance_speed/scale";
		private readonly StringName BALANCE_WIND_BLEND_PARAMETER = "parameters/balance_tree/wind_blend/blend_position";
		public void UpdateBalanceSpeed(float speedRatio)
		{
			animationTree.Set(BALANCE_SPEED_PARAMETER, speedRatio + .8f);
			animationTree.Set(BALANCE_WIND_BLEND_PARAMETER, speedRatio);
		}
		#endregion


		#region Sidle
		public bool IsSidleMoving => ActiveSidleState.GetFadingFromNode().IsEmpty && ActiveSidleState.GetCurrentNode() == SIDLE_LOOP_STATE_PARAMETER;

		private AnimationNodeStateMachinePlayback ActiveSidleState => isFacingRight ? SidleRightState : SidleLeftState;
		private AnimationNodeStateMachinePlayback SidleRightState => animationTree.Get(SIDLE_RIGHT_PLAYBACK).Obj as AnimationNodeStateMachinePlayback;
		private AnimationNodeStateMachinePlayback SidleLeftState => animationTree.Get(SIDLE_LEFT_PLAYBACK).Obj as AnimationNodeStateMachinePlayback;

		//Get sidle states
		private readonly StringName SIDLE_RIGHT_PLAYBACK = "parameters/sidle_tree/sidle_right_state/playback";
		private readonly StringName SIDLE_LEFT_PLAYBACK = "parameters/sidle_tree/sidle_left_state/playback";

		private readonly StringName SIDLE_LOOP_STATE_PARAMETER = "sidle-loop";
		private readonly StringName SIDLE_DAMAGE_STATE_PARAMETER = "sidle-damage-loop";
		private readonly StringName SIDLE_HANG_STATE_PARAMETER = "sidle-hang-loop";
		private readonly StringName SIDLE_HANG_FALL_STATE_PARAMETER = "sidle-hang-fall";
		private readonly StringName SIDLE_FALL_STATE_PARAMETER = "sidle-fall";

		private readonly StringName SIDLE_SPEED_PARAMETER = "parameters/sidle_tree/sidle_speed/scale";
		private readonly StringName SIDLE_SEEK_PARAMETER = "parameters/sidle_tree/sidle_seek/seek_request";
		private readonly StringName SIDLE_DIRECTION_TRANSITION = "parameters/sidle_tree/direction_transition/transition_request";

		public void StartSidle(bool isSidleFacingRight)
		{
			if (Character.ActionState == CharacterController.ActionStates.Teleport) // Skip transition
				SetStateXfade(0);
			else // Quick crossfade into sidle
				SetStateXfade(0.1f);

			isFacingRight = isSidleFacingRight;
			ActiveSidleState.Start(SIDLE_LOOP_STATE_PARAMETER);
			animationTree.Set(STATE_TRANSITION, SIDLE_STATE);
			animationTree.Set(SIDLE_DIRECTION_TRANSITION, isFacingRight ? RIGHT_CONSTANT : LEFT_CONSTANT);
		}

		public void UpdateSidle(float cyclePosition)
		{
			animationTree.Set(SIDLE_SPEED_PARAMETER, 0f);
			animationTree.Set(SIDLE_SEEK_PARAMETER, cyclePosition * .8f); //Sidle animation length is .8 seconds, so normalize cycle position.
		}

		/// <summary> Starts damage (stagger) animation. </summary>
		public void SidleDamage()
		{
			animationTree.Set(SIDLE_SPEED_PARAMETER, 1f);
			animationTree.Set(SIDLE_SEEK_PARAMETER, -1);

			ActiveSidleState.Travel(SIDLE_DAMAGE_STATE_PARAMETER);
		}

		/// <summary> Start hanging onto the ledge. </summary>
		public void SidleHang() => ActiveSidleState.Travel(SIDLE_HANG_STATE_PARAMETER);

		/// <summary> Recover back to the ledge. </summary>
		public void SidleRecovery() => ActiveSidleState.Travel(SIDLE_LOOP_STATE_PARAMETER);

		/// <summary> Fall while hanging on the ledge. </summary>
		public void SidleHangFall() => ActiveSidleState.Travel(SIDLE_HANG_FALL_STATE_PARAMETER);

		/// <summary> Fall from the ledge. </summary>
		public void SidleFall()
		{
			animationTree.Set(SIDLE_SPEED_PARAMETER, 1f);
			ActiveSidleState.Travel(SIDLE_FALL_STATE_PARAMETER);
		}
		#endregion

		#region Hurt
		private readonly StringName HURT_TRIGGER = "parameters/hurt_trigger/request";
		public void StartHurt()
		{
			animationTree.Set(HURT_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}


		public void StopHurt()
		{
			animationTree.Set(HURT_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.FadeOut);
		}
		#endregion






		// Shaders
		private readonly StringName PLAYER_POSITION_SHADER_PARAMETER = "player_position";
		private void UpdateShaderVariables()
		{
			//Update player position for shaders
			RenderingServer.GlobalShaderParameterSet(PLAYER_POSITION_SHADER_PARAMETER, GlobalPosition);
		}
	}
}