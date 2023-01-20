using Godot;

namespace Project.Gameplay
{
	/// <summary>
	/// Responsible for playing the player's animations and visual effects.
	/// </summary>
	public partial class CharacterAnimator : Node3D
	{
		[Export]
		private AnimationTree animatorTree;
		[Export]
		private AnimationPlayer extraAnimationPlayer;

		/// <summary> Reference to the root blend tree of the animation tree. </summary>
		private AnimationNodeBlendTree animationRoot;
		/// <summary> Transition node for switching between states (normal, balancing, sidling, etc) </summary>
		private AnimationNodeTransition animationStateTransition;
		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			animatorTree.Active = true; //Activate animator

			animationRoot = animatorTree.TreeRoot as AnimationNodeBlendTree;
			animationStateTransition = animationRoot.GetNode("state_transition") as AnimationNodeTransition;
			oneShotTransition = animationRoot.GetNode("oneshot_trigger") as AnimationNodeOneShot;

			normalState = (AnimationNodeStateMachinePlayback)animatorTree.Get("parameters/normal_state/playback");
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
		}

		/// <summary> How much should the turning animation be smoothed by? </summary>
		private const float TURN_SMOOTHING = .2f;
		/// <summary> Max amount of turning allowed. </summary>
		private readonly float MAX_TURN_ANGLE = Mathf.Pi * .4f;
		/// <summary>
		/// Calculates turn ratio based on current input with -1 being left and 1 being right.
		/// </summary>
		private float CalculateTurnRatio()
		{
			float delta;
			float inputAngle = Character.GetTargetInputAngle();
			delta = ExtensionMethods.SignedDeltaAngleRad(Character.MovementAngle, inputAngle);

			if (ExtensionMethods.DotAngle(Character.MovementAngle, inputAngle) < 0) //Input is backwards
				delta = -ExtensionMethods.SignedDeltaAngleRad(Character.MovementAngle + Mathf.Pi, inputAngle);

			delta = Mathf.Clamp(delta, -MAX_TURN_ANGLE, MAX_TURN_ANGLE);
			return delta / MAX_TURN_ANGLE;
		}

		#region Oneshot Animations
		private AnimationNodeOneShot oneShotTransition;
		/// <summary> Animation index for countdown animation. </summary>
		private const int COUNTDOWN_INDEX = 0;
		[Export]
		private Node3D countdownCameraController;

		public void PlayCountdown()
		{
			PlayOneshotAnimation(COUNTDOWN_INDEX);
			extraAnimationPlayer.Play("countdown-flame");

			//Prevent sluggish transitions into gameplay
			disableSpeedSmoothing = true;
			oneShotTransition.FadeinTime = oneShotTransition.FadeoutTime = 0;
			Character.Camera.ExternalController = countdownCameraController;
		}

		private readonly StringName ONESHOT_TRIGGER = "parameters/oneshot_trigger/active";
		private readonly StringName ONESHOT_SEEK_PARAMETER = "parameters/oneshot_tree/oneshot_seek/current";
		private readonly StringName ONESHOT_TRANSITION_PARAMETER = "parameters/oneshot_tree/oneshot_transition/current";
		public void PlayOneshotAnimation(int index) //Play a specific one-shot animation
		{
			animatorTree.Set(ONESHOT_TRIGGER, true);
			animatorTree.Set(ONESHOT_SEEK_PARAMETER, 0);
			animatorTree.Set(ONESHOT_TRANSITION_PARAMETER, index);
		}

		/// <summary>
		/// Cancels the oneshot animation early.
		/// </summary>
		public void CancelOneshot() => animatorTree.Set(ONESHOT_TRIGGER, false);
		#endregion

		#region States
		private const string STATE_PARAMETER = "parameters/state_transition/current";
		public void ResetState(float xfadeTime = -1) //Reset any state, while optionally setting the xfade time
		{
			SetStateXfade(xfadeTime);
			animatorTree.Set(STATE_PARAMETER, 0); //Revert to normal state
		}

		/// <summary>
		/// Sets the crossfade length of the primary state transition node.
		/// </summary>
		private void SetStateXfade(float xfadeTime = -1)
		{
			if (Mathf.IsEqualApprox(xfadeTime, -1)) return; //Invalid time
			animationStateTransition.XfadeTime = xfadeTime;
		}
		#endregion

		#region Normal Animations
		/// <summary> Gets the normal state's StateMachinePlayback </summary>
		private AnimationNodeStateMachinePlayback normalState;

		private bool canTransitionToFalling;
		private readonly StringName FALL_STATE_PARAMETER = "fall";
		private readonly StringName JUMP_CANCEL_TIME_PARAMETER = "parameters/air_state/jump_cancel_time/scale";

		private readonly StringName JUMP_STATE_PARAMETER = "jump";
		public void Jump()
		{
			canTransitionToFalling = true;
			normalState.Travel(JUMP_STATE_PARAMETER);
		}

		private readonly StringName AIR_DASH_PARAMETER = "jump-accel";
		private readonly StringName LAUNCH_PARAMETER = "jump-launch";
		public void AirAttackAnimation()
		{
			canTransitionToFalling = false;
			normalState.Travel(AIR_DASH_PARAMETER);
		}
		public void LaunchAnimation()
		{
			canTransitionToFalling = false;
			normalState.Travel(LAUNCH_PARAMETER);
		}

		private readonly StringName BACKFLIP_STATE_PARAMETER = "backflip";
		public void Backflip()
		{
			normalState.Travel(BACKFLIP_STATE_PARAMETER);
		}

		public void Stomp()
		{
			animatorTree.Set(JUMP_CANCEL_TIME_PARAMETER, 1.5f);
		}

		private readonly StringName GROUND_STATE_PARAMETER = "ground_tree";
		private readonly StringName AIR_STATE_PARAMETER = "air_tree";

		private readonly StringName BACKSTEP_TRANSITION_PARAMETER = "parameters/normal_state/ground_tree/backstep_transition/current";

		private readonly StringName MOVE_TRANSITION_PARAMETER = "parameters/normal_state/ground_tree/move_transition/current";
		private readonly StringName MOVE_SPEED_PARAMETER = "parameters/normal_state/ground_tree/move_speed/scale";
		private readonly StringName MOVE_SEEK_PARAMETER = "parameters/normal_state/ground_tree/move_seek/seek_position";
		private readonly StringName MOVE_BLEND_PARAMETER = "parameters/normal_state/ground_tree/move_blend/blend_position";

		private readonly StringName TURN_BLEND_PARAMETER = "parameters/normal_state/ground_tree/turn_blend/blend_position";
		private readonly StringName LAND_TRIGGER_PARAMETER = "parameters/normal_state/ground_tree/land_trigger/active";

		/// <summary> Disables speed smoothing. </summary>
		private bool disableSpeedSmoothing;
		[Export]
		private Curve movementAnimationSpeedCurve;
		/// <summary> What speedratio should be considered as fully running? </summary>
		private const float RUN_RATIO = .9f;
		/// <summary> How much should the animation speed be smoothed by? </summary>
		private const float SPEED_SMOOTHING = .04f;
		/// <summary> How much should the transition from idling be smoothed by? </summary>
		private const float IDLE_SMOOTHING = .2f;

		private void GroundAnimations()
		{
			//TODO Speed break animation
			if (Character.Skills.IsSpeedBreakCharging) return;

			int targetState = 0;
			float speedRatio = Mathf.Abs(Character.GroundSettings.GetSpeedRatio(Character.MoveSpeed));
			float targetAnimationSpeed = 1f;
			float targetTurnRatio = 0;

			if (Character.JustLandedOnGround) //Play landing animation
			{
				animatorTree.Set(MOVE_SEEK_PARAMETER, 0);
				animatorTree.Set(LAND_TRIGGER_PARAMETER, true);
				normalState.Travel(GROUND_STATE_PARAMETER);
			}

			if (!Mathf.IsZeroApprox(Character.MoveSpeed))
			{
				targetState = 1;

				if (Character.IsMovingBackward) //Backstep
				{
					animatorTree.Set(BACKSTEP_TRANSITION_PARAMETER, 0);
					speedRatio = Mathf.Abs(Character.BackstepSettings.GetSpeedRatio(Character.MoveSpeed));
					targetAnimationSpeed = 1.2f + speedRatio;
				}
				else //Moving
				{
					animatorTree.Set(BACKSTEP_TRANSITION_PARAMETER, 1);
					if (speedRatio >= RUN_RATIO) //Running
					{
						float extraSpeed = Mathf.Clamp((speedRatio - RUN_RATIO) / .2f, 0f, 1.4f);
						targetAnimationSpeed = 2f + extraSpeed;
					}
					else //Jogging
						targetAnimationSpeed = movementAnimationSpeedCurve.Sample(speedRatio / RUN_RATIO); //Normalize speed ratio

					if (Character.IsLockoutActive && Character.ActiveLockoutData.movementMode != LockoutResource.MovementModes.Free)
						targetTurnRatio = 0;
					else
						targetTurnRatio = CalculateTurnRatio();
				}
			}

			animatorTree.Set(MOVE_TRANSITION_PARAMETER, targetState);
			animatorTree.Set(MOVE_BLEND_PARAMETER, speedRatio);

			if (disableSpeedSmoothing)
			{
				animatorTree.Set(MOVE_SPEED_PARAMETER, targetAnimationSpeed);
				disableSpeedSmoothing = false;
			}
			else
				animatorTree.Set(MOVE_SPEED_PARAMETER, Mathf.Lerp((float)animatorTree.Get(MOVE_SPEED_PARAMETER), targetAnimationSpeed, SPEED_SMOOTHING));

			if (Character.MovementState == CharacterController.MovementStates.External) //Disable turning when controlled externally
				targetTurnRatio = 0;
			else
				targetTurnRatio = Mathf.Lerp(((Vector2)animatorTree.Get(TURN_BLEND_PARAMETER)).x, targetTurnRatio, TURN_SMOOTHING);

			animatorTree.Set(TURN_BLEND_PARAMETER, new Vector2(targetTurnRatio, speedRatio));
		}

		private void AirAnimations()
		{
			if (canTransitionToFalling)
			{
				if (Character.MovementState == CharacterController.MovementStates.Launcher) return;

				if (Character.ActionState != CharacterController.ActionStates.Jumping ||
				Character.VerticalSpd <= 0)
				{
					canTransitionToFalling = false;
					normalState.Travel(FALL_STATE_PARAMETER);
				}
			}
		}
		#endregion

		#region Visual Rotation
		/// <summary> Angle to use when character's MovementState is CharacterController.MovementStates.External. </summary>
		public float ExternalAngle { get; set; }
		/// <summary> Rotation (in radians) currently applied to Transform. </summary>
		private float VisualAngle { get; set; }
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
			if (Character.IsGrindstepJump) return; //Use the same angle as the grindrail

			//Don't update directions when externally controlled or on launchers
			float targetRotation = Character.MovementAngle;
			float smoothing = MOVEMENT_ROTATION_SMOOTHING;

			if (Character.MovementState == CharacterController.MovementStates.External)
				targetRotation = ExternalAngle;
			else if (Character.Lockon.IsHomingAttacking) //Face target
				targetRotation = Character.CalculateForwardAngle(Character.Lockon.HomingAttackDirection);
			else if (Character.Skills.IsSpeedBreakActive || Character.IsMovingBackward) //Speed break, use path's forward direction
				targetRotation = Character.PathFollower.ForwardAngle;
			else if (Character.IsLockoutActive && Character.ActiveLockoutData.recenterPlayer)
				targetRotation = Character.PathFollower.ForwardAngle;

			VisualAngle = ExtensionMethods.SmoothDampAngle(VisualAngle, targetRotation, ref rotationVelocity, MOVEMENT_ROTATION_SMOOTHING);
			ApplyVisualRotation();
		}

		/// <summary>
		/// Apply currentRotation on Transform.
		/// </summary>
		private void ApplyVisualRotation() => Rotation = Vector3.Up * VisualAngle;
		#endregion

		#region Grinding and Balancing Animations
		private const int BALANCE_STATE_INDEX = 1;
		private const string BALANCE_RIGHT_STATE_PARAMETER = "parameters/balance_state/lean_right_tree/balance_blend/blend_position";
		private const string WIND_BLEND_PARAMETER = "parameters/balance_state/lean_right_tree/wind_blend/blend_position";
		private const string BALANCE_SPEED_PARAMETER = "parameters/balance_state/lean_right_tree/balance_speed/scale";
		public void StartBalancing()
		{
			SetStateXfade(0); //Don't blend into state
			animatorTree.Set(STATE_PARAMETER, BALANCE_STATE_INDEX); //Turn on balancing animations

			//Reset current balance
			animatorTree.Set(BALANCE_RIGHT_STATE_PARAMETER, 0);
			UpdateBalanceSpeed();
		}

		public void StartGrindStep()
		{

		}

		public void StartGrindShuffle()
		{

		}

		public void UpdateBalancing()
		{
			float targetBalance = CalculateTurnRatio();
			targetBalance = Mathf.Lerp(((float)animatorTree.Get(BALANCE_RIGHT_STATE_PARAMETER)), targetBalance, TURN_SMOOTHING);
			animatorTree.Set(BALANCE_RIGHT_STATE_PARAMETER, targetBalance);

			UpdateBalanceSpeed(true);
		}

		private void UpdateBalanceSpeed(bool enableSmoothing = false)
		{
			float currentSpeed = Character.Skills.grindSettings.GetSpeedRatioClamped(Character.MoveSpeed);
			if (enableSmoothing)
				currentSpeed = Mathf.Lerp((float)animatorTree.Get(WIND_BLEND_PARAMETER), currentSpeed, SPEED_SMOOTHING);

			animatorTree.Set(WIND_BLEND_PARAMETER, currentSpeed);
			animatorTree.Set(BALANCE_SPEED_PARAMETER, 1f + currentSpeed);
		}
		#endregion


		#region Sidle
		private const int SIDLE_STATE_INDEX = 2;

		private readonly string STRAFE_SEEK_PARAMETER = "parameters/sidle_tree/sidle_seek/seek_position";
		private readonly string STRAFE_DIRECTION_PARAMETER = "parameters/sidle_tree/facing_right/current";

		public void StartSidle(bool facingRight)
		{
			if (Character.IsRespawning) //Cut directly
				SetStateXfade(0);
			else //Quick crossfade into sidle
				SetStateXfade(0.1f);

			animatorTree.Set(STATE_PARAMETER, SIDLE_STATE_INDEX);
			animatorTree.Set(STRAFE_DIRECTION_PARAMETER, facingRight ? 0 : 1);
		}

		public void UpdateSidle(float cyclePosition)
		{
			animatorTree.Set(STRAFE_SEEK_PARAMETER, cyclePosition * .8f); //Sidle animation length is .8 seconds, so normalize cycle position.
		}
		#endregion
	}
}