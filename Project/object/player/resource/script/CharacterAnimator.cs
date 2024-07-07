using Godot;
using Project.Core;

namespace Project.Gameplay;

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
	/// <summary> Transition node for switching between crouch and moving. </summary>
	private AnimationNodeTransition crouchTransition;

	/// <summary> Determines facing directions for certain animation actions. </summary>
	private bool isFacingRight;
	/// <summary> Keeps track of the leading foot during run animations. </summary>
	private bool isLeadingWithRightFoot;
	private void SetLeadingFoot(bool value) => isLeadingWithRightFoot = value;

	// For toggle transitions
	private readonly StringName EnabledConstant = "enabled";
	private readonly StringName DisabledConstant = "disabled";
	// For directional transitions
	private readonly StringName RightConstant = "right";
	private readonly StringName LeftConstant = "left";

	public override void _EnterTree()
	{
		animationTree.Active = true; // Activate animator

		animationRoot = animationTree.TreeRoot as AnimationNodeBlendTree;
		stateTransition = animationRoot.GetNode("state_transition") as AnimationNodeTransition;
		groundTransition = animationRoot.GetNode("ground_transition") as AnimationNodeTransition;
		crouchTransition = (animationRoot.GetNode("ground_tree") as AnimationNodeBlendTree).GetNode("crouch_transition") as AnimationNodeTransition;
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

		UpdateBrake();
		UpdateVisualRotation();
		UpdateShaderVariables();
	}

	#region Oneshot Animations
	private AnimationNodeOneShot oneShotTransition;
	/// <summary> Animation index for countdown animation. </summary>
	private readonly StringName CountdownAnimation = "countdown";

	public void PlayCountdown()
	{
		PlayOneshotAnimation(CountdownAnimation);

		// Prevent sluggish transitions into gameplay
		DisabledSpeedSmoothing = true;
		oneShotTransition.FadeInTime = oneShotTransition.FadeOutTime = 0;
	}

	private readonly StringName OneshotTrigger = "parameters/oneshot_trigger/request";
	private readonly StringName OneshotSeek = "parameters/oneshot_tree/oneshot_seek/current";
	private readonly StringName OneshotTransition = "parameters/oneshot_tree/oneshot_transition/transition_request";
	public void PlayOneshotAnimation(StringName animation) // Play a specific one-shot animation
	{
		animationTree.Set(OneshotTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		animationTree.Set(OneshotSeek, 0);
		animationTree.Set(OneshotTransition, animation);
	}

	/// <summary>
	/// Cancels the oneshot animation early.
	/// </summary>
	public void CancelOneshot(float fadeout = 0)
	{
		oneShotTransition.FadeOutTime = fadeout;

		// Abort accidental landing animations
		if (!Character.JustLandedOnGround || Mathf.IsZeroApprox(fadeout))
			animationTree.Set(LandTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		else
			animationTree.Set(OneshotTrigger, (int)AnimationNodeOneShot.OneShotRequest.FadeOut);
	}
	#endregion

	#region States
	private readonly StringName NormalState = "normal";
	private readonly StringName DriftState = "drift";
	private readonly StringName BalanceState = "balance";
	private readonly StringName SidleState = "sidle";
	private readonly StringName SpinState = "spin";

	private readonly StringName StateTransition = "parameters/state_transition/transition_request";

	public void ResetState(float xfadeTime = -1) // Reset any state, while optionally setting the xfade time
	{
		if (xfadeTime != -1)
			SetStateXfade(xfadeTime);

		animationTree.Set(StateTransition, NormalState); // Revert to normal state
	}

	/// <summary>
	/// Sets the crossfade length of the primary state transition node.
	/// </summary>
	private void SetStateXfade(float xfadeTime) => stateTransition.XfadeTime = xfadeTime;
	#endregion

	#region Normal Animations
	private readonly StringName IdleBlend = "parameters/ground_tree/idle_blend/blend_amount";
	private readonly StringName ForwardSeek = "parameters/ground_tree/forward_seek/seek_request";
	private readonly StringName BackwardSeek = "parameters/ground_tree/backward_seek/seek_request";

	private readonly StringName GroundSpeed = "parameters/ground_tree/ground_speed/scale";
	private readonly StringName GroundSeek = "parameters/ground_tree/ground_seek/seek_request";
	private readonly StringName ForwardBlend = "parameters/ground_tree/forward_blend/blend_position";

	private readonly StringName TurnBlend = "parameters/ground_tree/turn_blend/blend_position";
	private readonly StringName LandTrigger = "parameters/ground_tree/land_trigger/request";

	private readonly StringName SpeedBreakTrigger = "parameters/ground_tree/speedbreak_trigger/request";

	[Export]
	private Curve movementAnimationSpeedCurve;
	/// <summary> Disables speed smoothing. </summary>
	public bool DisabledSpeedSmoothing { get; set; }
	private float idleBlendVelocity;
	/// <summary> How much should the animation speed be smoothed by? </summary>
	private const float SpeedSmoothing = .06f;
	/// <summary> How much should the transition from idling be smoothed by? </summary>
	private const float IdleSmoothing = .05f;
	/// <summary> What speedratio should be considered as fully running? </summary>
	public const float RunRatio = .9f;

	/// <summary> Forces the player's animation back to the grounded state. </summary>
	private readonly StringName GroundTransition = "parameters/ground_transition/transition_request";
	public void SnapToGround()
	{
		groundTransition.XfadeTime = 0.0f;
		animationTree.Set(GroundTransition, EnabledConstant);
		ResetGroundTree();
	}

	private void ResetGroundTree()
	{
		DisabledSpeedSmoothing = true;
		animationTree.Set(GroundSeek, 0);
		animationTree.Set(LandTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animationTree.Set(CrouchTransition, DisabledConstant);
	}

	public void SpeedBreak()
	{
		animationTree.Set(ForwardSeek, .5f);
		animationTree.Set(SpeedBreakTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		animationTree.Set(LandTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
	}

	private void GroundAnimations()
	{
		Character.Effect.IsEmittingStepDust = !Mathf.IsZeroApprox(Character.MoveSpeed); // Emit step dust based on speed

		if (Character.Skills.IsSpeedBreakCharging) return;

		float idleBlend = (float)animationTree.Get(IdleBlend);
		float speedRatio = Mathf.Abs(Character.GroundSettings.GetSpeedRatio(Character.MoveSpeed));
		float targetAnimationSpeed = 1f;
		groundTurnRatio = 0;

		if (Character.JustLandedOnGround) // Play landing animation
		{
			ResetGroundTree();
			animationTree.Set(LandTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			groundTransition.XfadeTime = .05f;
			animationTree.Set(GroundTransition, EnabledConstant);
			StopHurt();
		}

		if (Character.IsLockoutActive && Character.ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe &&
			speedRatio < .5f)
		{
			speedRatio = Character.GroundSettings.GetSpeedRatio(Character.MoveSpeed);
		}

		if (!Mathf.IsZeroApprox(Character.MoveSpeed))
		{
			if (Character.IsMovingBackward) // Backstep
			{
				if (DisabledSpeedSmoothing)
					idleBlend = -1;
				else
					idleBlend = ExtensionMethods.SmoothDamp(idleBlend, -1, ref idleBlendVelocity, IdleSmoothing);

				speedRatio = Mathf.Abs(Character.BackstepSettings.GetSpeedRatio(Character.MoveSpeed));
				targetAnimationSpeed = 0.5f + (speedRatio * 2f);
			}
			else // Moving forward
			{
				if (DisabledSpeedSmoothing)
					idleBlend = 1;
				else
					idleBlend = ExtensionMethods.SmoothDamp(idleBlend, 1, ref idleBlendVelocity, IdleSmoothing);

				if (Character.Skills.IsSpeedBreakActive) // Constant animation speed
				{
					targetAnimationSpeed = 4f;
				}
				else if (speedRatio >= RunRatio) // Running
				{
					float extraSpeed = Mathf.Clamp((speedRatio - RunRatio) / .2f, 0f, 1f);
					targetAnimationSpeed = 2.2f + extraSpeed;
				}
				else // Jogging
				{
					targetAnimationSpeed = movementAnimationSpeedCurve.Sample(speedRatio / RunRatio); // Normalize speed ratio

					// Speed up animation if player is trying to start running
					if (Character.InputVector.Length() >= .5f &&
						speedRatio < .3f &&
						!Character.IsOnWall())
					{
						targetAnimationSpeed = 2.5f;
					}
				}
			}

			if (Character.MovementState == CharacterController.MovementStates.External)
				groundTurnRatio = 0; // Disable turning when controlled externally
			else
				groundTurnRatio = CalculateTurnRatio();
		}
		else
		{
			idleBlend = ExtensionMethods.SmoothDamp(idleBlend, 0, ref idleBlendVelocity, IdleSmoothing);

			if (Mathf.IsZeroApprox(idleBlend))
			{
				animationTree.Set(BackwardSeek, 0);
				animationTree.Set(ForwardSeek, 0);
			}
		}

		animationTree.Set(IdleBlend, idleBlend);
		animationTree.Set(ForwardBlend, speedRatio);
		if (DisabledSpeedSmoothing)
		{
			animationTree.Set(GroundSpeed, targetAnimationSpeed);
			DisabledSpeedSmoothing = false;
		}
		else
		{
			animationTree.Set(GroundSpeed, Mathf.Lerp((float)animationTree.Get(GroundSpeed), targetAnimationSpeed, SpeedSmoothing));
		}

		groundTurnRatio = Mathf.Lerp(((Vector2)animationTree.Get(TurnBlend)).X, groundTurnRatio, TurnSmoothing); // Blend from animator
		animationTree.Set(TurnBlend, new Vector2(groundTurnRatio, Character.IsMovingBackward ? 0 : speedRatio));
	}

	public bool IsBrakeAnimationActive { get; private set; }
	private AnimationNodeStateMachinePlayback BrakeStatePlayback => animationTree.Get(BrakePlayback).Obj as AnimationNodeStateMachinePlayback;

	private readonly StringName BrakePlayback = "parameters/ground_tree/brake_state/playback";
	private readonly StringName BrakeTrigger = "parameters/ground_tree/brake_trigger/request";
	private readonly StringName BrakeActive = "parameters/ground_tree/brake_trigger/active";
	private readonly StringName BrakeStartState = "-start";
	private readonly StringName BrakeStopState = "-stop";
	private const float BrakeDeadzone = 5f;

	public void StartBrake()
	{
		if (IsBrakeAnimationActive) return; // Already active

		if (!Character.IsOnGround) return; // Only animate when moving forward
		if (Character.IsMovingBackward) return; // Only animate when moving forward
		if (Character.GroundSettings.GetSpeedRatio(Character.MoveSpeed) < RunRatio)
			return;

		isFacingRight = isLeadingWithRightFoot;
		Character.Effect.PlayActionSFX(Character.Effect.SlideSfx);
		animationTree.Set(BrakeTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		BrakeStatePlayback.Travel(isFacingRight ? "r" + BrakeStartState : "l" + BrakeStartState);
		IsBrakeAnimationActive = true;
	}

	public void UpdateBrake()
	{
		if (!IsBrakeAnimationActive) return; // Not braking

		if (Character.MoveSpeed <= BrakeDeadzone || Character.IsMovingBackward || !Character.IsOnGround)
			StopBrake();
	}

	public void StopBrake()
	{
		if (!IsBrakeAnimationActive) return; // Not braking

		IsBrakeAnimationActive = false;
		BrakeStatePlayback.Travel(isFacingRight ? "r" + BrakeStopState : "l" + BrakeStopState);
	}

	/// <summary> Blend from -1 <-> 1 of how much the player is turning. </summary>
	private float groundTurnRatio;
	/// <summary> How much should the turning animation be smoothed by? </summary>
	private const float TurnSmoothing = .1f;
	/// <summary> Max amount of turning allowed. </summary>
	private readonly float MaxTurnAngle = Mathf.Pi * .4f;
	/// <summary> How much to visually lean into turns. </summary>
	private readonly float PathTurnStrength = 10f;
	/// <summary> Calculates turn ratio based on current input with -1 being left and 1 being right. </summary>
	public float CalculateTurnRatio()
	{
		float referenceAngle = Character.IsMovingBackward ? Character.PathFollower.ForwardAngle : Character.MovementAngle;
		float inputAngle = Character.GetInputAngle() + (Character.PathFollower.DeltaAngle * PathTurnStrength);
		float delta = ExtensionMethods.SignedDeltaAngleRad(referenceAngle, inputAngle);

		if (ExtensionMethods.DotAngle(referenceAngle, inputAngle) < 0) // Input is backwards
			delta = -ExtensionMethods.SignedDeltaAngleRad(referenceAngle + Mathf.Pi, inputAngle);

		delta = Mathf.Clamp(delta, -MaxTurnAngle, MaxTurnAngle);
		return delta / MaxTurnAngle;
	}

	public bool IsFallTransitionEnabled { get; set; }
	private readonly StringName FallTrigger = "parameters/air_tree/fall_trigger/request";
	private readonly StringName FallSpeed = "parameters/air_tree/fall_speed/scale";
	private readonly StringName AirStateTransition = "parameters/air_tree/state_transition/transition_request";
	private readonly StringName AccelJumpTrigger = "parameters/air_tree/jump_accel_trigger/request";
	private readonly StringName FallState = "fall";
	private void UpdateAirState(StringName state, bool enableFallTransition)
	{
		IsFallTransitionEnabled = enableFallTransition;
		animationTree.Set(AirStateTransition, state);
		animationTree.Set(FallTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animationTree.Set(AccelJumpTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animationTree.Set(BounceTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animationTree.Set(BackflipTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animationTree.Set(StompTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
	}

	public void JumpAnimation()
	{
		ResetState();
		UpdateAirState("jump", true);
	}
	public void JumpAccelAnimation()
	{
		IsFallTransitionEnabled = false;
		animationTree.Set(AccelJumpTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}
	public void LaunchAnimation() => UpdateAirState("launch", false);

	private readonly StringName StompState = "stomp";
	private readonly StringName StompTrigger = "parameters/air_tree/stomp_trigger/request";
	public void StompAnimation(bool offensive)
	{
		if (offensive)
		{
			// Offensive stomp animation
			Character.Effect.StartStompFX();
			UpdateAirState(StompState, false);
			animationTree.Set(StompTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}
		else
		{
			UpdateAirState(FallState, false);
			animationTree.Set(FallSpeed, 2.5f);
			animationTree.Set(FallTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}
	}

	private readonly StringName BackflipTrigger = "parameters/air_tree/backflip_trigger/request";
	public void BackflipAnimation()
	{
		animationTree.Set(AirStateTransition, FallState);
		animationTree.Set(BackflipTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		animationTree.Set(BrakeTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
	}

	private readonly StringName BounceTransition = "parameters/air_tree/bounce_transition/transition_request";
	private readonly StringName BounceTrigger = "parameters/air_tree/bounce_trigger/request";
	private const int BounceVariationCount = 4;
	public void BounceTrick()
	{
		UpdateAirState(FallState, false);
		animationTree.Set(BounceTransition, Runtime.randomNumberGenerator.RandiRange(1, BounceVariationCount).ToString());
		animationTree.Set(BounceTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private void AirAnimations()
	{
		Character.Effect.IsEmittingStepDust = false;
		animationTree.Set(GroundTransition, DisabledConstant);

		if (IsFallTransitionEnabled)
		{
			if (Character.MovementState == CharacterController.MovementStates.Launcher)
			{
				if (!Character.LaunchSettings.IsJump || Character.VerticalSpeed >= 0)
					return;
			}

			if (Character.ActionState != CharacterController.ActionStates.Jumping ||
			Character.VerticalSpeed <= 0)
			{
				UpdateAirState(FallState, false);
				animationTree.Set(FallSpeed, 1.0f);
				animationTree.Set(FallTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			}
		}
	}

	private AnimationNodeStateMachinePlayback CrouchStatePlayback => animationTree.Get(CrouchPlayback).Obj as AnimationNodeStateMachinePlayback;
	private readonly StringName CrouchPlayback = "parameters/ground_tree/crouch_state/playback";

	private readonly StringName CrouchStateStart = "crouch-start";
	private readonly StringName CrouchStateStop = "crouch-stop";

	private readonly StringName SlideStateStart = "slide-start";
	private readonly StringName SlideStateStop = "slide-stop";
	private readonly StringName CrouchTransition = "parameters/ground_tree/crouch_transition/transition_request";
	private readonly StringName CurrentCrouchState = "parameters/ground_tree/crouch_transition/current_state";

	public bool IsCrouchingActive => (StringName)animationTree.Get(CurrentCrouchState) == EnabledConstant;
	public bool IsSlideTransitionActive => CrouchStatePlayback.GetCurrentNode() == SlideStateStart;
	public void StartCrouching()
	{
		if (Character.ActionState == CharacterController.ActionStates.Sliding)
		{
			crouchTransition.XfadeTime = .05;
			CrouchStatePlayback.Travel(SlideStateStart);
		}
		else
		{
			CrouchStatePlayback.Travel(CrouchStateStart);
			crouchTransition.XfadeTime = .1;
		}

		animationTree.Set(CrouchTransition, EnabledConstant);
	}

	public void ToggleSliding()
	{
		if (Character.ActionState == CharacterController.ActionStates.Sliding)
			CrouchStatePlayback.Travel(SlideStateStart);
		else
			CrouchStatePlayback.Travel(SlideStateStop);
	}

	public void StopCrouching()
	{
		if (Character.ActionState == CharacterController.ActionStates.Sliding)
			crouchTransition.XfadeTime = 0.2;
		else
			crouchTransition.XfadeTime = 0.0;

		CrouchStatePlayback.Travel(CrouchStateStop);
	}

	public void CrouchToMoveTransition()
	{
		// Limit blending to the time remaining in current animation
		float max = CrouchStatePlayback.GetCurrentLength() - CrouchStatePlayback.GetCurrentPlayPosition();
		crouchTransition.XfadeTime = Mathf.Clamp(0.2, 0, max);
		animationTree.Set(CrouchTransition, DisabledConstant);
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
	private readonly float MovementRotationSmoothing = .1f;

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
			targetRotation = Character.PathFollower.ForwardAngle + (groundTurnRatio * Mathf.Pi * .15f);
		else if (Character.IsLockoutActive && Character.ActiveLockoutData.recenterPlayer)
			targetRotation = Character.PathFollower.ForwardAngle;

		if (Character.Skills.IsSpeedBreakActive && Character.MovementState != CharacterController.MovementStates.External)
			VisualAngle += Character.PathFollower.DeltaAngle;

		VisualAngle = ExtensionMethods.ClampAngleRange(VisualAngle, Character.PathFollower.ForwardAngle, Mathf.Pi);
		VisualAngle = ExtensionMethods.SmoothDampAngle(VisualAngle, targetRotation, ref rotationVelocity, MovementRotationSmoothing);
		ApplyVisualRotation();
	}

	/// <summary>
	/// Apply VisualAngle onto Transform.
	/// </summary>
	private void ApplyVisualRotation() => Rotation = Vector3.Up * VisualAngle;
	#endregion

	#region Drift
	private AnimationNodeStateMachinePlayback ActiveDriftStatePlayback => isFacingRight ? DriftRightStatePlayback : DriftLeftStatePlayback;
	private AnimationNodeStateMachinePlayback DriftRightStatePlayback => animationTree.Get(DriftRightPlayback).Obj as AnimationNodeStateMachinePlayback;
	private AnimationNodeStateMachinePlayback DriftLeftStatePlayback => animationTree.Get(DriftLeftPlayback).Obj as AnimationNodeStateMachinePlayback;

	private readonly StringName DriftLeftPlayback = "parameters/drift_tree/left_state/playback";
	private readonly StringName DriftRightPlayback = "parameters/drift_tree/right_state/playback";

	private readonly StringName DriftDirectionTransition = "parameters/drift_tree/direction_transition/transition_request";
	private readonly StringName DriftStartState = "drift-start";
	private readonly StringName DriftLaunchState = "drift-launch";

	public void StartDrift(bool isDriftFacingRight)
	{
		isFacingRight = isDriftFacingRight;
		ActiveDriftStatePlayback.Start(DriftStartState);
		animationTree.Set(DriftDirectionTransition, isFacingRight ? RightConstant : LeftConstant);

		SetStateXfade(.2f); // Transition into drift
		animationTree.Set(StateTransition, DriftState);
	}

	/// <summary> Called when drift is performed. </summary>
	public void LaunchDrift()
	{
		ActiveDriftStatePlayback.Travel(DriftLaunchState);
		SetStateXfade(0.1f); // Remove xfade in case player wants to jump early
	}
	#endregion

	#region Grinding and Balancing Animations
	/// <summary> Reference to the balance state's StateMachinePlayback </summary>
	private AnimationNodeStateMachinePlayback BalanceStatePlayback => animationTree.Get(BalancePlayback).Obj as AnimationNodeStateMachinePlayback;
	private readonly StringName BalancePlayback = "parameters/balance_tree/balance_state/playback";

	/// <summary> Reference to the balance state's StateMachinePlayback </summary>
	private AnimationNodeStateMachinePlayback GrindStepStatePlayback => animationTree.Get(GrindstepPlayback).Obj as AnimationNodeStateMachinePlayback;
	private readonly StringName GrindstepPlayback = "parameters/balance_tree/grindstep_state/playback";

	/// <summary> Is the shuffling animation currently active? </summary>
	public bool IsBalanceShuffleActive { get; private set; }

	private readonly StringName ShuffleRight = "balance-right-shuffle";
	private readonly StringName ShuffleLeft = "balance-left-shuffle";
	private readonly StringName BalanceRight = "balance_right_blend";
	private readonly StringName BalanceLeft = "balance_left_blend";

	private readonly StringName BalanceRightLean = "parameters/balance_tree/balance_state/balance_right_blend/blend_position";
	private readonly StringName BalanceLeftLean = "parameters/balance_tree/balance_state/balance_left_blend/blend_position";

	private readonly StringName BalanceCrouchAdd = "parameters/balance_tree/crouch_add/add_amount";
	private readonly StringName BalanceDirectionTransition = "parameters/balance_tree/direction_transition/transition_request";

	public void StartBalancing()
	{
		IsBalanceShuffleActive = true;
		isFacingRight = true; // Default to facing right
		BalanceStatePlayback.Start(ShuffleRight, true); // Start with a shuffle

		// Reset current balance
		animationTree.Set(BalanceLeftLean, 0);
		animationTree.Set(BalanceRightLean, 0);

		SetStateXfade(0.05f);
		animationTree.Set(StateTransition, BalanceState); // Turn on balancing animations
		animationTree.Set(BalanceGrindstepTrigger, (int)AnimationNodeOneShot.OneShotRequest.FadeOut); // Disable any grindstepping
	}

	public void UpdateBalanceCrouch(bool isCrouching)
	{
		float current = (float)animationTree.Get(BalanceCrouchAdd);
		float target = isCrouching ? 1.0f : 0.0f;
		current = Mathf.Lerp(current, target, .2f);
		animationTree.Set(BalanceCrouchAdd, current);
	}

	private readonly StringName BalanceGrindstepTrigger = "parameters/balance_tree/grindstep_trigger/request";
	/// <summary> How many variations of the grindstep animation are there? </summary>
	private readonly int GrindstepVariationCount = 3;
	public void StartGrindStep()
	{
		int index = Runtime.randomNumberGenerator.RandiRange(1, GrindstepVariationCount);
		string targetPose = isFacingRight ? "step-right-0" : "step-left-0";
		GrindStepStatePlayback.Start(targetPose + index.ToString(), true);
		animationTree.Set(BalanceGrindstepTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	public void StartGrindShuffle()
	{
		IsBalanceShuffleActive = true;
		isFacingRight = !isFacingRight;
		BalanceStatePlayback.Travel(isFacingRight ? ShuffleRight : ShuffleLeft);
	}

	private float balanceTurnVelocity;
	/// <summary> How much should the balancing animation be smoothed by? </summary>
	private const float BalanceTurnSmoothing = .15f;
	public void UpdateBalancing(float balanceRatio)
	{
		if (IsBalanceShuffleActive)
		{
			StringName currentNode = BalanceStatePlayback.GetCurrentNode();
			if ((isFacingRight && currentNode == BalanceRight) ||
				(!isFacingRight && currentNode == BalanceLeft))
			{
				IsBalanceShuffleActive = false;
				animationTree.Set(BalanceDirectionTransition, isFacingRight ? RightConstant : LeftConstant);
			}

			balanceRatio = 0;
		}

		balanceRatio = ExtensionMethods.SmoothDamp((float)animationTree.Get(BalanceRightLean), balanceRatio, ref balanceTurnVelocity, BalanceTurnSmoothing);
		animationTree.Set(BalanceRightLean, balanceRatio);
		animationTree.Set(BalanceLeftLean, -balanceRatio);
	}

	private readonly StringName BalanceSpeed = "parameters/balance_tree/balance_speed/scale";
	private readonly StringName BalanceWindBlend = "parameters/balance_tree/wind_blend/blend_position";
	public void UpdateBalanceSpeed(float speedRatio)
	{
		animationTree.Set(BalanceSpeed, speedRatio + .8f);
		animationTree.Set(BalanceWindBlend, speedRatio);
	}
	#endregion

	#region Sidle
	public bool IsSidleMoving => ActiveSidleStatePlayback.GetFadingFromNode().IsEmpty && ActiveSidleStatePlayback.GetCurrentNode() == SidleLoopState;

	private AnimationNodeStateMachinePlayback ActiveSidleStatePlayback => isFacingRight ? SidleRightStatePlayback : SidleLeftStatePlayback;
	private AnimationNodeStateMachinePlayback SidleRightStatePlayback => animationTree.Get(SidleRightPlayback).Obj as AnimationNodeStateMachinePlayback;
	private AnimationNodeStateMachinePlayback SidleLeftStatePlayback => animationTree.Get(SidleLeftPlayback).Obj as AnimationNodeStateMachinePlayback;

	// Get sidle states
	private readonly StringName SidleRightPlayback = "parameters/sidle_tree/sidle_right_state/playback";
	private readonly StringName SidleLeftPlayback = "parameters/sidle_tree/sidle_left_state/playback";

	private readonly StringName SidleLoopState = "sidle-loop";
	private readonly StringName SidleDamateState = "sidle-damage-loop";
	private readonly StringName SidleHangState = "sidle-hang-loop";
	private readonly StringName SidleHangFallState = "sidle-hang-fall";
	private readonly StringName SidleFallState = "sidle-fall";

	private readonly StringName SidleSpeed = "parameters/sidle_tree/sidle_speed/scale";
	private readonly StringName SidleSeek = "parameters/sidle_tree/sidle_seek/seek_request";
	private readonly StringName SidleDirectionTransition = "parameters/sidle_tree/direction_transition/transition_request";

	public void StartSidle(bool isSidleFacingRight)
	{
		if (Character.ActionState == CharacterController.ActionStates.Teleport) // Skip transition
			SetStateXfade(0);
		else // Quick crossfade into sidle
			SetStateXfade(0.1f);

		isFacingRight = isSidleFacingRight;
		ActiveSidleStatePlayback.Start(SidleLoopState);
		animationTree.Set(StateTransition, SidleState);
		animationTree.Set(SidleDirectionTransition, isFacingRight ? RightConstant : LeftConstant);
	}

	public void UpdateSidle(float cyclePosition)
	{
		animationTree.Set(SidleSpeed, 0f);
		animationTree.Set(SidleSeek, cyclePosition * .8f); // Sidle animation length is .8 seconds, so normalize cycle position.
	}

	/// <summary> Starts damage (stagger) animation. </summary>
	public void SidleDamage()
	{
		animationTree.Set(SidleSpeed, 1f);
		animationTree.Set(SidleSeek, -1);

		ActiveSidleStatePlayback.Travel(SidleDamateState);
	}

	/// <summary> Start hanging onto the ledge. </summary>
	public void SidleHang() => ActiveSidleStatePlayback.Travel(SidleHangState);

	/// <summary> Recover back to the ledge. </summary>
	public void SidleRecovery() => ActiveSidleStatePlayback.Travel(SidleLoopState);

	/// <summary> Fall while hanging on the ledge. </summary>
	public void SidleHangFall() => ActiveSidleStatePlayback.Travel(SidleHangFallState);

	/// <summary> Fall from the ledge. </summary>
	public void SidleFall()
	{
		animationTree.Set(SidleSpeed, 1f);
		ActiveSidleStatePlayback.Travel(SidleFallState);
	}
	#endregion

	#region Hurt
	private readonly StringName HurtTrigger = "parameters/hurt_trigger/request";
	public void StartHurt() => animationTree.Set(HurtTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);

	public void StopHurt() => animationTree.Set(HurtTrigger, (int)AnimationNodeOneShot.OneShotRequest.FadeOut);
	#endregion

	#region Spin
	private readonly StringName SpinSpeed = "parameters/spin_speed/scale";
	public void StartSpin(float speed = 1.0f)
	{
		SetSpinSpeed(speed);
		SetStateXfade(.2f);
		animationTree.Set(StateTransition, SpinState);
		UpdateAirState(FallState, true);
	}

	public void SetSpinSpeed(float speed = 1.0f) => animationTree.Set(SpinSpeed, speed);
	#endregion

	// Shaders
	private readonly StringName ShaderPlayerPositionParameter = "player_position";
	private void UpdateShaderVariables()
	{
		// Update player position for shaders
		RenderingServer.GlobalShaderParameterSet(ShaderPlayerPositionParameter, GlobalPosition);
	}
}