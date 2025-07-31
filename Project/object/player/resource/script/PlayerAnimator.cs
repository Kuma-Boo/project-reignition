using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary>
/// Responsible for playing the player's animations.
/// </summary>
public partial class PlayerAnimator : Node3D
{
	[Signal] public delegate void CountdownLandingEventHandler();

	private PlayerController Player;
	public void Initialize(PlayerController player)
	{
		Player = player;

		animationTree.Active = true; // Activate animator

		animationRoot = animationTree.TreeRoot as AnimationNodeBlendTree;
		stateTransition = animationRoot.GetNode("state_transition") as AnimationNodeTransition;
		groundTransition = animationRoot.GetNode("ground_transition") as AnimationNodeTransition;
		crouchTransition = (animationRoot.GetNode("ground_tree") as AnimationNodeBlendTree).GetNode("crouch_transition") as AnimationNodeTransition;
		oneShotTrigger = animationRoot.GetNode("oneshot_trigger") as AnimationNodeOneShot;

		AnimationNodeBlendTree oneShotTree = animationRoot.GetNode("oneshot_tree") as AnimationNodeBlendTree;
		oneShotTransition = oneShotTree.GetNode("oneshot_transition") as AnimationNodeTransition;
	}

	[Export]
	private AnimationTree animationTree;
	[Export]
	private AnimationPlayer eventAnimationPlayer;
	[Export]
	private MeshInstance3D bodyMesh;
	[Export]
	private ShaderMaterial blurOverrideMaterial;

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

	/// <summary>
	/// Called every frame. Only updates normal animations and visual rotation.
	/// </summary>
	public void ProcessPhysics()
	{
		AirAnimations();
		UpdateVisualRotation();
		UpdateShaderVariables();
	}

	#region Oneshot Animations
	private AnimationNodeTransition oneShotTransition;
	private AnimationNodeOneShot oneShotTrigger;
	/// <summary> Animation index for countdown animation. </summary>
	private readonly StringName CountdownAnimation = "countdown";

	public void PlayCountdown()
	{
		PlayOneshotAnimation(CountdownAnimation);

		// Prevent sluggish transitions into gameplay
		DisabledSpeedSmoothing = true;
		oneShotTrigger.FadeOutTime = 0;
	}

	private readonly StringName OneshotTrigger = "parameters/oneshot_trigger/request";
	private readonly StringName OneshotSeek = "parameters/oneshot_tree/oneshot_seek/current";
	private readonly StringName OneshotActive = "parameters/oneshot_trigger/active";
	private readonly StringName OneshotCurrent = "parameters/oneshot_tree/oneshot_transition/current_state";
	private readonly StringName OneshotTransition = "parameters/oneshot_tree/oneshot_transition/transition_request";
	public void PlayOneshotAnimation(StringName animation, float fadein = 0) // Play a specific one-shot animation
	{
		oneShotTrigger.FadeInTime = fadein;
		animationTree.Set(OneshotTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		animationTree.Set(OneshotSeek, 0);
		animationTree.Set(OneshotTransition, animation);

		StopCrouching(0f);
	}


	public bool IsOneshotAnimationValid(string animation)
	{
		if (string.IsNullOrEmpty(animation))
			return false;

		bool isAnimationValid = false;
		for (int i = 0; i < oneShotTransition.GetInputCount(); i++)
		{
			GD.Print(oneShotTransition.GetInputName(i));
			if (!oneShotTransition.GetInputName(i).Equals(animation))
				continue;

			isAnimationValid = true;
			break;
		}

		return isAnimationValid;
	}

	public void SeekOneshotAnimation(float time)
	{
		GD.Print("seeking player animation to " + time);
		animationTree.Set(OneshotSeek, time);
	}

	/// <summary>
	/// Cancels the oneshot animation early.
	/// </summary>
	public void CancelOneshot(float fadeout = 0)
	{
		oneShotTrigger.FadeOutTime = fadeout;

		// Abort accidental landing animations
		if (Mathf.IsZeroApprox(fadeout))
			animationTree.Set(LandTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);

		animationTree.Set(OneshotTrigger, (int)AnimationNodeOneShot.OneShotRequest.FadeOut);
	}
	#endregion

	#region States
	private readonly StringName NormalState = "normal";
	private readonly StringName DriftState = "drift";
	private readonly StringName BalanceState = "balance";
	private readonly StringName SidleState = "sidle";
	private readonly StringName SpinState = "spin";
	private readonly StringName GimmickState = "gimmick";

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
	private readonly StringName ReversePathTrigger = "parameters/ground_tree/reverse_path_trigger/request";
	private readonly StringName ReversePathActive = "parameters/ground_tree/reverse_path_trigger/active";

	private readonly StringName SpeedBreakTrigger = "parameters/ground_tree/speedbreak_trigger/request";

	[Export]
	private Curve movementAnimationSpeedCurve;
	/// <summary> Disables speed smoothing. </summary>
	public bool DisabledSpeedSmoothing { get; set; }
	private float idleBlendVelocity;
	/// <summary> How much should the animation speed be smoothed by? </summary>
	private const float SpeedSmoothing = .1f;
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

	public void LandingAnimation()
	{
		ResetGroundTree();
		animationTree.Set(LandTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		animationTree.Set(SplashJumpTrigger, (int)AnimationNodeOneShot.OneShotRequest.FadeOut);
		groundTransition.XfadeTime = .05f;
		animationTree.Set(GroundTransition, EnabledConstant);
	}

	public bool IsReversePathAnimationActive => (bool)animationTree.Get(ReversePathActive);
	public void ReversePathAnimation()
	{
		animationTree.Set(ReversePathTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		groundTransition.XfadeTime = .05f;
		animationTree.Set(GroundTransition, EnabledConstant);
	}

	private void ResetGroundTree()
	{
		DisabledSpeedSmoothing = true;
		animationTree.Set(GroundSeek, 0);
		animationTree.Set(LandTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animationTree.Set(ReversePathTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animationTree.Set(CrouchTransition, DisabledConstant);
	}

	public void SpeedBreak()
	{
		animationTree.Set(ForwardSeek, .5f);
		animationTree.Set(SpeedBreakTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		animationTree.Set(LandTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
	}

	public void IdleAnimation()
	{
		groundTurnRatio = 0;
		float idleBlend = (float)animationTree.Get(IdleBlend);
		if (DisabledSpeedSmoothing)
			idleBlend = 0;
		else
			idleBlend = ExtensionMethods.SmoothDamp(idleBlend, 0, ref idleBlendVelocity, IdleSmoothing);

		if (Mathf.IsZeroApprox(idleBlend))
		{
			animationTree.Set(BackwardSeek, 0);
			animationTree.Set(ForwardSeek, 0);
		}

		UpdateGroundAnimation(idleBlend, 0);
	}

	public void RunAnimation()
	{
		float idleBlend = (float)animationTree.Get(IdleBlend);
		if (DisabledSpeedSmoothing)
			idleBlend = 1;
		else
			idleBlend = ExtensionMethods.SmoothDamp(idleBlend, 1, ref idleBlendVelocity, IdleSmoothing);

		float speedRatio = Player.Stats.GroundSettings.GetSpeedRatio(Player.MoveSpeed);
		float baseSpeedRatio = Player.MoveSpeed / Player.Stats.baseGroundSpeed;
		float animationSpeed;
		if (Player.Skills.IsSpeedBreakActive) // Constant animation speed
		{
			animationSpeed = 4f;
		}
		else if (baseSpeedRatio >= RunRatio) // Running
		{
			float extraSpeed = Mathf.Clamp((baseSpeedRatio - 1.0f) * 5.0f, 0f, 2f);
			animationSpeed = 2.8f + extraSpeed;
		}
		else // Jogging
		{
			animationSpeed = movementAnimationSpeedCurve.Sample(baseSpeedRatio / RunRatio); // Normalize speed ratio

			// Speed up animation if player is trying to start running
			if (IsRunAccelerating(baseSpeedRatio))
			{
				animationSpeed += 2f * Mathf.Clamp(1f - baseSpeedRatio / .4f, 0f, 1f);
				speedRatio = Mathf.Max(speedRatio, 0.2f); // Limit to jog animation
			}
		}

		groundTurnRatio = CalculateTurnRatio();
		UpdateGroundAnimation(idleBlend, speedRatio, animationSpeed);
	}

	/// <summary>
	/// Checks whether the player is trying to run, or if they're just walking.
	/// </summary>
	/// <returns> True if the player is trying to accelerate. </returns>
	private bool IsRunAccelerating(float speedRatio)
	{
		if (Player.IsOnWall)
			return false;

		if (speedRatio > .5f)
			return false;

		float inputStrength = Player.Controller.GetInputStrength();
		if (inputStrength >= .8f)
			return true;

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun) && Mathf.IsZeroApprox(inputStrength))
			return true;

		return Player.IsLockoutActive &&
			Player.ActiveLockoutData.overrideSpeed &&
			!Mathf.IsZeroApprox(Player.ActiveLockoutData.speedRatio);
	}

	public void BackstepAnimation()
	{
		float idleBlend = (float)animationTree.Get(IdleBlend);
		if (DisabledSpeedSmoothing)
			idleBlend = -1;
		else
			idleBlend = ExtensionMethods.SmoothDamp(idleBlend, -1, ref idleBlendVelocity, IdleSmoothing);

		float speedRatio = Mathf.Abs(Player.Stats.BackstepSettings.GetSpeedRatio(Player.MoveSpeed));
		float animationSpeed = 0.5f + (speedRatio * 2f);

		groundTurnRatio = CalculateTurnRatio();
		UpdateGroundAnimation(idleBlend, speedRatio, animationSpeed);
	}

	private void UpdateGroundAnimation(float idleBlend, float speedRatio, float animationSpeed = 1)
	{
		if (StageSettings.Instance.LevelState == StageSettings.LevelStateEnum.Success &&
			StageSettings.Instance.Data.CompletionAnimation == LevelDataResource.CompletionAnimationType.ThumbsUp)
		{
			if (!(bool)animationTree.Get(OneshotActive))
			{
				PlayOneshotAnimation((Player.Stats.GroundSettings.GetSpeedRatio(Player.MoveSpeed) > .5f) ? "completion_standing" : "completion_crouching", .2f);
				Player.Camera.StartCompletionCamera();
			}

			return;
		}

		if (Player.Skills.IsSpeedBreakCharging) return;

		animationTree.Set(IdleBlend, idleBlend);
		animationTree.Set(ForwardBlend, speedRatio);
		if (DisabledSpeedSmoothing)
		{
			animationTree.Set(GroundSpeed, animationSpeed);
			DisabledSpeedSmoothing = false;
		}
		else
		{
			animationTree.Set(GroundSpeed, Mathf.Lerp((float)animationTree.Get(GroundSpeed), animationSpeed, SpeedSmoothing));
		}

		groundTurnRatio = Mathf.Lerp(((Vector2)animationTree.Get(TurnBlend)).X, groundTurnRatio, TurnSmoothing); // Blend from animator
		animationTree.Set(TurnBlend, new Vector2(groundTurnRatio, Player.IsMovingBackward ? 0 : speedRatio));
	}

	public bool IsBrakeAnimationActive { get; private set; }
	private AnimationNodeStateMachinePlayback BrakeStatePlayback => animationTree.Get(BrakePlayback).Obj as AnimationNodeStateMachinePlayback;

	private readonly StringName BrakePlayback = "parameters/ground_tree/brake_state/playback";
	private readonly StringName BrakeTrigger = "parameters/ground_tree/brake_trigger/request";
	private readonly StringName BrakeActive = "parameters/ground_tree/brake_trigger/active";
	private readonly StringName BrakeStartState = "-start";
	private readonly StringName BrakeStopState = "-stop";
	public void StartBrake()
	{
		isFacingRight = isLeadingWithRightFoot;
		animationTree.Set(BrakeTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		BrakeStatePlayback.Travel(isFacingRight ? "r" + BrakeStartState : "l" + BrakeStartState);
		IsBrakeAnimationActive = true;
	}

	public void StopBrake()
	{
		IsBrakeAnimationActive = false;
		BrakeStatePlayback.Travel(isFacingRight ? "r" + BrakeStopState : "l" + BrakeStopState);
	}

	private readonly StringName QuickStepTrigger = "parameters/ground_tree/quick_step_trigger/request";
	private readonly StringName QuickStepTransition = "parameters/ground_tree/quick_step_transition/transition_request";
	private readonly StringName QuickStepSpeed = "parameters/ground_tree/quick_step_speed/scale";
	public void StartQuickStep(bool isSteppingRight)
	{
		animationTree.Set(QuickStepTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		animationTree.Set(QuickStepTransition, isSteppingRight ? RightConstant : LeftConstant);
		animationTree.Set(QuickStepSpeed, 1.5f);
	}

	private AnimationNodeStateMachinePlayback LightDashStatePlayback => animationTree.Get(LightDashPlayback).Obj as AnimationNodeStateMachinePlayback;
	private readonly StringName LightDashPlayback = "parameters/air_tree/light_dash_state/playback";
	private readonly StringName LightDashTrigger = "parameters/air_tree/light_dash_trigger/request";
	private readonly StringName LightDashSpeed = "parameters/air_tree/light_dash_speed/scale";
	public void StartLightDashAnimation()
	{
		animationTree.Set(LightDashTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		animationTree.Set(LightDashSpeed, 2f);
		LightDashStatePlayback.Start("start");
	}

	public void StopLightDashAnimation()
	{
		LightDashStatePlayback.Start("stop");
		animationTree.Set(LightDashSpeed, 1f);
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
		if (Player.ExternalController != null && !Player.IsGrinding)
			return 0; // Disable turning when controlled externally

		float referenceAngle = Player.IsMovingBackward ? Player.PathFollower.ForwardAngle : Player.MovementAngle;
		float inputAngle = Player.PathFollower.DeltaAngle * PathTurnStrength;
		if (Player.IsLockoutActive && Player.ActiveLockoutData.movementMode == LockoutResource.MovementModes.Replace)
			inputAngle += referenceAngle;
		else if (!Mathf.IsZeroApprox(Player.Controller.GetInputStrength()))
			inputAngle = Player.Controller.GetTargetInputAngle();
		else
			inputAngle = referenceAngle;

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
		animationTree.Set(SplashJumpTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animationTree.Set(HurtTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animationTree.Set(QuickStepTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animationTree.Set(LightDashTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animationTree.Set(AutoJumpTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
	}

	public void JumpAnimation()
	{
		ResetState();
		UpdateAirState("jump", true);
	}

	private readonly StringName AutoJumpTrigger = "parameters/air_tree/autojump_trigger/request";
	public void AutoJumpAnimation()
	{
		ResetState();
		UpdateAirState(FallState, false);
		animationTree.Set(AutoJumpTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	public void JumpAccelAnimation()
	{
		IsFallTransitionEnabled = false;
		animationTree.Set(AccelJumpTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}
	public void JumpDashAnimation() => UpdateAirState("launch", false);
	public void LaunchAnimation() => UpdateAirState("launch", true);

	private readonly StringName StompState = "stomp";
	private readonly StringName StompTrigger = "parameters/air_tree/stomp_trigger/request";
	public void StompAnimation(bool offensive)
	{
		if (offensive)
		{
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
		IsFallTransitionEnabled = false;
		animationTree.Set(AirStateTransition, FallState);
		animationTree.Set(BackflipTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		animationTree.Set(BrakeTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animationTree.Set(FallTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
	}

	private readonly StringName SplashJumpTrigger = "parameters/air_tree/splash_jump_trigger/request";
	public void SplashJumpAnimation()
	{
		animationTree.Set(AirStateTransition, FallState);
		animationTree.Set(SplashJumpTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
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
		if (Player.IsOnGround)
			return;

		if (Player.ExternalController != null)
			return;

		Player.Effect.IsEmittingStepDust = false;
		animationTree.Set(GroundTransition, DisabledConstant);

		if (IsFallTransitionEnabled && Player.VerticalSpeed < 0 && !Player.IsLaunching)
		{
			UpdateAirState(FallState, false);
			animationTree.Set(FallSpeed, 1.0f);
			animationTree.Set(FallTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}
	}

	private AnimationNodeStateMachinePlayback CrouchStatePlayback => animationTree.Get(CrouchPlayback).Obj as AnimationNodeStateMachinePlayback;
	private readonly StringName CrouchPlayback = "parameters/ground_tree/crouch_state/playback";

	private readonly StringName CrouchStateStart = "crouch-start";
	private readonly StringName CrouchStateLoop = "crouch-loop";
	private readonly StringName CrouchStateStop = "crouch-stop";
	private readonly StringName ChargeStationaryStateStart = "charge-stationary-start";
	private readonly StringName ChargeStationaryStateStop = "charge-stationary-stop";

	private readonly StringName SlideStateStart = "slide-start";
	private readonly StringName SlideStateStop = "slide-stop";
	private readonly StringName ChargeSlideStateStart = "charge-slide-start";
	private readonly StringName ChargeSlideStateStop = "charge-slide-stop";
	private readonly StringName CrouchTransition = "parameters/ground_tree/crouch_transition/transition_request";
	private readonly StringName CurrentCrouchState = "parameters/ground_tree/crouch_transition/current_state";

	public bool IsCrouchTransitionActive => CrouchStatePlayback.GetCurrentNode() == CrouchStateStart || CrouchStatePlayback.GetCurrentNode() == ChargeStationaryStateStart;
	public bool IsSlideTransitionActive => CrouchStatePlayback.GetCurrentNode() == SlideStateStart || CrouchStatePlayback.GetCurrentNode() == ChargeSlideStateStart;

	public void StartSliding()
	{
		crouchTransition.XfadeTime = .05;
		CrouchStatePlayback.Travel(SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump) ?
			ChargeSlideStateStart : SlideStateStart);
		animationTree.Set(CrouchTransition, EnabledConstant);
	}

	public void SlideToCrouch() => CrouchStatePlayback.Travel(SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump) ?
		ChargeSlideStateStop : SlideStateStop);
	public void StartCrouching()
	{
		string currentAnimation = CrouchStatePlayback.GetCurrentNode().ToString();
		if (currentAnimation.Contains("slide")) // Slide transition
			return;

		CrouchStatePlayback.CallDeferred(AnimationNodeStateMachinePlayback.MethodName.Travel,
			SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump) ?
			ChargeStationaryStateStart : CrouchStateStart);

		crouchTransition.XfadeTime = .1;
		animationTree.SetDeferred(CrouchTransition, EnabledConstant);
	}

	public void StopCrouching(float transitionTime = 0.2f)
	{
		CrouchStatePlayback.Travel(SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump) ?
			ChargeStationaryStateStop : CrouchStateStop);

		crouchTransition.XfadeTime = transitionTime;
		animationTree.SetDeferred(CrouchTransition, DisabledConstant);
	}

	public void CrouchToMoveTransition()
	{
		// Limit blending to the time remaining in current animation
		float max = CrouchStatePlayback.GetCurrentLength() - CrouchStatePlayback.GetCurrentPlayPosition();
		StopCrouching(Mathf.Clamp(0.2f, 0f, max));
	}

	public void StartMotionBlur() => bodyMesh.MaterialOverride = blurOverrideMaterial;
	public void StopMotionBlur() => bodyMesh.MaterialOverride = null;

	public void StartInvincibility(float speedScale)
	{
		eventAnimationPlayer.Play("invincibility", -1, speedScale);
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
	/// <summary> Angle to use when Player's MovementState is PlayerController.MovementStates.External. </summary>
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
		Rotation = Vector3.Up * VisualAngle;
	}

	/// <summary>
	/// Calculates the target visual rotation and applies it.
	/// </summary>
	private void UpdateVisualRotation()
	{
		if (Player.IsGrindstepping)
			return;

		if (Player.IsLaunching)
			return;

		float targetRotation = Player.MovementAngle;
		if (Player.ExternalController != null)
			targetRotation = ExternalAngle;
		else if (Player.IsHomingAttacking) // Face target
			targetRotation = ExtensionMethods.CalculateForwardAngle(Player.Lockon.HomingAttackDirection);
		else if (Player.IsReversePath && Player.IsOnGround)
			targetRotation = Player.PathFollower.ForwardAngle;
		else if (Player.IsMovingBackward) // Backstepping
			targetRotation = Player.PathFollower.ForwardAngle + (groundTurnRatio * Mathf.Pi * .15f);
		else if (Player.IsLockoutActive && Player.ActiveLockoutData.recenterPlayer)
			targetRotation = Player.PathFollower.ForwardAngle + Player.PathTurnInfluence;
		else if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun) && Mathf.IsZeroApprox(Player.MoveSpeed))
			targetRotation = VisualAngle;

		if (Player.ExternalController == null &&
			(Player.Skills.IsSpeedBreakActive ||
			Player.IsLockoutOverridingMovementAngle))
		{
			// Fix sluggish angle changes during lockout overrides
			VisualAngle += Player.PathFollower.DeltaAngle;
		}

		VisualAngle = ExtensionMethods.ClampAngleRange(VisualAngle, Player.PathFollower.ForwardAngle, Mathf.Pi);
		VisualAngle = ExtensionMethods.SmoothDampAngle(VisualAngle, targetRotation, ref rotationVelocity, MovementRotationSmoothing);
		Rotation = Vector3.Up * VisualAngle;
	}
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
	private readonly StringName DriftFailState = "drift-fail";

	public void StartDrift(bool isDriftFacingRight)
	{
		isFacingRight = isDriftFacingRight;
		ActiveDriftStatePlayback.Start(DriftStartState);
		animationTree.Set(DriftDirectionTransition, isFacingRight ? RightConstant : LeftConstant);

		SetStateXfade(.1f); // Transition into drift
		animationTree.Set(StateTransition, DriftState);
	}

	/// <summary> Called when drift is failed. </summary>
	public void FailDrift()
	{
		ActiveDriftStatePlayback.Travel(DriftFailState);
		SetStateXfade(0.1f); // Remove xfade in case player wants to jump early
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
	private readonly StringName BalanceStaggerLeft = "balance_left_stagger";
	private readonly StringName BalanceStaggerRight = "balance_right_stagger";

	private readonly StringName BalanceRightLean = "parameters/balance_tree/balance_state/balance_right_blend/blend_position";
	private readonly StringName BalanceLeftLean = "parameters/balance_tree/balance_state/balance_left_blend/blend_position";

	private readonly StringName BalanceCrouchAdd = "parameters/balance_tree/crouch_add/add_amount";
	private readonly StringName BalanceDirectionTransition = "parameters/balance_tree/direction_transition/transition_request";

	private readonly StringName BalanceTrickTrigger = "parameters/balance_tree/trick_trigger/request";
	private readonly StringName BalanceTrickTransition = "parameters/balance_tree/trick_transition/transition_request";

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

	public void StartBalanceStagger() => BalanceStatePlayback.Travel(isFacingRight ? BalanceStaggerRight : BalanceStaggerLeft, true);

	public void StartBalanceTrick(StringName trickName)
	{
		animationTree.Set(BalanceTrickTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		animationTree.Set(BalanceTrickTransition, trickName);
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
	public void UpdateBalanceSpeed(float speedRatio, float overrideWindBlend = -1)
	{
		animationTree.Set(BalanceSpeed, speedRatio + .8f);
		animationTree.Set(BalanceWindBlend, Mathf.IsEqualApprox(overrideWindBlend, -1) ? speedRatio : overrideWindBlend);
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
		SetStateXfade(Player.IsTeleporting ? 0 : .1f);
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
	private readonly StringName HurtBackwardState = "hurt-backward-start";
	private readonly StringName HurtForwardStartState = "hurt-forward-start";
	private readonly StringName HurtForwardStopState = "hurt-forward-stop";
	private readonly StringName HurtPlayback = "parameters/hurt_state/playback";
	private AnimationNodeStateMachinePlayback HurtStatePlayback => animationTree.Get(HurtPlayback).Obj as AnimationNodeStateMachinePlayback;

	public void StartHurt(bool forwardLaunch)
	{
		HurtStatePlayback.Start(forwardLaunch ? HurtForwardStartState : HurtBackwardState);
		animationTree.Set(HurtTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	public void StopHurt(bool useTransition)
	{
		if (useTransition)
			HurtStatePlayback.Travel(HurtForwardStopState);
		else
			animationTree.Set(HurtTrigger, (int)AnimationNodeOneShot.OneShotRequest.FadeOut);
	}
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

	#region Gimmicks
	private readonly StringName GimmickTransition = "parameters/gimmick_tree/state_transition/transition_request";

	private readonly StringName IvyState = "ivy";
	private readonly StringName IvyBlend = "parameters/gimmick_tree/ivy_blend/blend_position";
	private readonly StringName IvyStartTrigger = "parameters/gimmick_tree/ivy_start_trigger/request";
	private readonly StringName IvySwingTrigger = "parameters/gimmick_tree/ivy_swing_trigger/request";
	private readonly StringName IvyStartActive = "parameters/gimmick_tree/ivy_start_trigger/active";
	private readonly StringName IvySwingActive = "parameters/gimmick_tree/ivy_swing_trigger/active";

	public bool IsIvyStartActive => (bool)animationTree.Get(IvyStartActive);
	public bool IsIvySwingActive => (bool)animationTree.Get(IvySwingActive);

	public void StartIvy()
	{
		SetStateXfade(.2f);
		animationTree.Set(StateTransition, GimmickState);
		animationTree.Set(GimmickTransition, IvyState);
		animationTree.Set(IvyStartTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	public void StartIvySwing() => animationTree.Set(IvySwingTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);

	public void SetIvyBlend(float ratio) => animationTree.Set(IvyBlend, ratio);

	private readonly StringName ZiplineState = "zipline";
	private readonly StringName ZiplineBlend = "parameters/gimmick_tree/zipline_blend/blend_position";
	private readonly StringName ZiplineDirection = "parameters/gimmick_tree/zipline_direction/transition_request";
	private readonly StringName ZiplineTapTrigger = "parameters/gimmick_tree/zipline_tap_trigger/request";
	private readonly StringName ZiplineTapActive = "parameters/gimmick_tree/zipline_tap_trigger/active";
	public void StartZipline()
	{
		SetStateXfade(.2f);
		animationTree.Set(StateTransition, GimmickState);
		animationTree.Set(GimmickTransition, ZiplineState);
	}
	public void SetZiplineBlend(float ratio) => animationTree.Set(ZiplineBlend, ratio);
	public float GetZiplineBlend() => (float)animationTree.Get(ZiplineBlend);

	public void StartZiplineTap(bool isFacingRight)
	{
		animationTree.Set(ZiplineDirection, isFacingRight ? RightConstant : LeftConstant);
		animationTree.Set(ZiplineTapTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	public bool IsZiplineTapActive => (bool)animationTree.Get(ZiplineTapActive);

	public void CancelZiplineTap() => animationTree.Set(ZiplineTapTrigger, (int)AnimationNodeOneShot.OneShotRequest.FadeOut);

	private readonly StringName HornState = "horn";
	public void StartBemothHorn()
	{
		SetStateXfade(.2f);
		animationTree.Set(StateTransition, GimmickState);
		animationTree.Set(GimmickTransition, HornState);
	}

	public void StartPetrify()
	{
		SetStateXfade(0f);
		animationTree.Set(StateTransition, GimmickState);

		eventAnimationPlayer.Play("petrify-start");
		eventAnimationPlayer.Advance(0.0);
	}

	public void ShakePetrify()
	{
		eventAnimationPlayer.Stop(true);
		eventAnimationPlayer.Play("petrify-shake");
	}

	private readonly StringName PetrifyState = "petrify";
	private readonly StringName PetrifyStopTrigger = "parameters/gimmick_tree/petrify_stop_trigger/request";
	public void StopPetrify()
	{
		animationTree.Set(GimmickTransition, PetrifyState);
		animationTree.Set(PetrifyStopTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		ResetState(0.5f);

		eventAnimationPlayer.Play("petrify-stop");
		eventAnimationPlayer.Advance(0.0);
	}

	private readonly StringName LeverState = "lever";
	private AnimationNodeStateMachinePlayback LeverStatePlayback => animationTree.Get(LeverPlayback).Obj as AnimationNodeStateMachinePlayback;

	private readonly StringName LeverPlayback = "parameters/gimmick_tree/lever_state/playback";
	public void StartLever(bool isRightLever)
	{
		SetStateXfade(0.2f);
		animationTree.Set(StateTransition, GimmickState);
		animationTree.Set(GimmickTransition, LeverState);
		LeverStatePlayback.Start((isRightLever ? RightConstant : LeftConstant) + "-loop");
	}

	public void StartLeverTurn(bool isRightLever) => LeverStatePlayback.Travel(isRightLever ? RightConstant : LeftConstant);
	#endregion

	// Shaders
	private readonly StringName ShaderPlayerPositionParameter = "player_position";
	private void UpdateShaderVariables()
	{
		// Update player position for shaders
		RenderingServer.GlobalShaderParameterSet(ShaderPlayerPositionParameter, GlobalPosition);
	}
}
