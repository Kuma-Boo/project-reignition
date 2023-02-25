using Godot;

namespace Project.Gameplay
{
	/// <summary>
	/// Responsible for playing the player's animations and visual effects.
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
		/// <summary> Transition node for switching between states (normal, balancing, sidling, etc) </summary>
		private AnimationNodeTransition animationStateTransition;

		//For toggle transitions
		private readonly StringName ENABLED_CONSTANT = "enabled";
		private readonly StringName DISABLED_CONSTANT = "disabled";
		//For directional transitions
		private readonly StringName RIGHT_CONSTANT = "right";
		private readonly StringName LEFT_CONSTANT = "left";

		public override void _Ready()
		{
			animationTree.Active = true; //Activate animator

			animationRoot = animationTree.TreeRoot as AnimationNodeBlendTree;
			animationStateTransition = animationRoot.GetNode("state_transition") as AnimationNodeTransition;
			oneShotTransition = animationRoot.GetNode("oneshot_trigger") as AnimationNodeOneShot;

			//Get normal state
			normalState = animationTree.Get("parameters/normal_state/playback").Obj as AnimationNodeStateMachinePlayback;

			//Get drift state
			driftLeftState = animationTree.Get("parameters/normal_state/drift_tree/left_state/playback").Obj as AnimationNodeStateMachinePlayback;
			driftRightState = animationTree.Get("parameters/normal_state/drift_tree/right_state/playback").Obj as AnimationNodeStateMachinePlayback;

			//Get balance state
			balanceState = animationTree.Get("parameters/balance_tree/balance_state/playback").Obj as AnimationNodeStateMachinePlayback;

			//Get sidle states
			sidleRightState = animationTree.Get("parameters/sidle_tree/sidle_right_state/playback").Obj as AnimationNodeStateMachinePlayback;
			sidleLeftState = animationTree.Get("parameters/sidle_tree/sidle_left_state/playback").Obj as AnimationNodeStateMachinePlayback;
		}

		public void StartInvincibility()
		{
			eventAnimationPlayer.Play("invincibility");
			eventAnimationPlayer.Seek(0.0, true);
		}

		/// <summary> Called when the player respawns. Resets all animations. </summary>
		public void Respawn()
		{
			normalState.Travel(GROUND_TREE_STATE);
			eventAnimationPlayer.Play("respawn");
		}

		private readonly StringName PLAYER_POSITION_SHADER_PARAMETER = "player_position";
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

			//Update player position for shaders
			RenderingServer.GlobalShaderParameterSet(PLAYER_POSITION_SHADER_PARAMETER, GlobalPosition);
		}

		#region Oneshot Animations
		private AnimationNodeOneShot oneShotTransition;
		/// <summary> Animation index for countdown animation. </summary>
		private readonly StringName COUNTDOWN_ANIMATION = "countdown";
		[Export]
		private Node3D countdownCameraController;

		public void PlayCountdown()
		{
			PlayOneshotAnimation(COUNTDOWN_ANIMATION);
			eventAnimationPlayer.Play(COUNTDOWN_ANIMATION);

			//Prevent sluggish transitions into gameplay
			disableSpeedSmoothing = true;
			oneShotTransition.FadeInTime = oneShotTransition.FadeOutTime = 0;
			Character.Camera.EventController = countdownCameraController;
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
		public void CancelOneshot() => animationTree.Set(ONESHOT_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		#endregion

		#region States
		private readonly StringName NORMAL_STATE = "normal";
		private readonly StringName BALANCE_STATE = "balance";
		private readonly StringName SIDLE_STATE = "sidle";

		private readonly StringName STATE_PARAMETER = "parameters/state_transition/transition_request";

		public void ResetState(float xfadeTime = -1) //Reset any state, while optionally setting the xfade time
		{
			if (xfadeTime != -1)
				SetStateXfade(xfadeTime);

			animationTree.Set(STATE_PARAMETER, NORMAL_STATE); //Revert to normal state
		}

		/// <summary>
		/// Sets the crossfade length of the primary state transition node.
		/// </summary>
		private void SetStateXfade(float xfadeTime) => animationStateTransition.XfadeTime = xfadeTime;
		#endregion

		#region Normal Animations
		/// <summary> Gets the normal state's StateMachinePlayback </summary>
		private AnimationNodeStateMachinePlayback normalState;

		private bool canTransitionToFalling;
		private readonly StringName FALL_STATE_PARAMETER = "fall";
		public void Fall()
		{
			canTransitionToFalling = true;
		}

		private readonly StringName JUMP_STATE_PARAMETER = "jump";
		public void Jump()
		{
			canTransitionToFalling = true;
			normalState.Travel(JUMP_STATE_PARAMETER);
		}

		private readonly StringName HURT_STATE_PARAMETER = "hurt";
		public void Hurt()
		{
			normalState.Travel(HURT_STATE_PARAMETER);
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

		private readonly StringName GROUND_TREE_STATE = "ground_tree";

		private readonly StringName IDLE_STATE = "idle";
		private readonly StringName MOVE_FORWARD_STATE = "forward";
		private readonly StringName MOVE_BACK_STATE = "backward";

		private readonly StringName MOVE_REQUEST_PARAMETER = "parameters/normal_state/ground_tree/move_transition/transition_request";
		private readonly StringName MOVE_SPEED_PARAMETER = "parameters/normal_state/ground_tree/move_speed/scale";
		private readonly StringName MOVE_SEEK_PARAMETER = "parameters/normal_state/ground_tree/move_seek/seek_request";
		private readonly StringName MOVE_BLEND_PARAMETER = "parameters/normal_state/ground_tree/move_blend/blend_position";

		private readonly StringName TURN_BLEND_PARAMETER = "parameters/normal_state/ground_tree/turn_blend/blend_position";
		private readonly StringName LAND_TRIGGER_PARAMETER = "parameters/normal_state/ground_tree/land_trigger/request";

		/// <summary> Disables speed smoothing. </summary>
		private bool disableSpeedSmoothing;
		[Export]
		private Curve movementAnimationSpeedCurve;
		/// <summary> What speedratio should be considered as fully running? </summary>
		private const float RUN_RATIO = .9f;
		/// <summary> How much should the animation speed be smoothed by? </summary>
		private const float SPEED_SMOOTHING = .06f;
		/// <summary> How much should the transition from idling be smoothed by? </summary>
		private const float IDLE_SMOOTHING = .2f;

		private void GroundAnimations()
		{
			//TODO Speed break animation
			if (Character.Skills.IsSpeedBreakCharging) return;

			StringName targetMoveState = IDLE_STATE;
			float speedRatio = Mathf.Abs(Character.GroundSettings.GetSpeedRatio(Character.MoveSpeed));
			float targetAnimationSpeed = 1f;
			groundTurnRatio = 0;

			if (Character.JustLandedOnGround) //Play landing animation
			{
				animationTree.Set(MOVE_SEEK_PARAMETER, 0);
				animationTree.Set(LAND_TRIGGER_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
				normalState.Travel(GROUND_TREE_STATE);
			}

			if (Character.IsLockoutActive && Character.ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe &&
				speedRatio < .5f)
			{
				//Use the pythagorean theorem to get true movespeed
				float trueSpeed = Mathf.Pow(Character.MoveSpeed, 2) + Mathf.Pow(Character.StrafeSpeed, 2);
				trueSpeed = Mathf.Sqrt(trueSpeed);
				speedRatio = Character.GroundSettings.GetSpeedRatio(trueSpeed);
			}

			if (!Mathf.IsZeroApprox(Character.MoveSpeed))
			{
				if (Character.IsMovingBackward) //Backstep
				{
					targetMoveState = MOVE_BACK_STATE;
					speedRatio = Mathf.Abs(Character.BackstepSettings.GetSpeedRatio(Character.MoveSpeed));
					targetAnimationSpeed = 1.2f + speedRatio;
				}
				else //Moving
				{
					targetMoveState = MOVE_FORWARD_STATE;

					if (speedRatio >= RUN_RATIO) //Running
					{
						if (Character.Skills.IsSpeedBreakActive)
							targetAnimationSpeed = 2.5f;
						else
						{
							float extraSpeed = Mathf.Clamp((speedRatio - RUN_RATIO) / .2f, 0f, 1.4f);
							targetAnimationSpeed = 2f + extraSpeed;
						}
					}
					else //Jogging
					{
						targetAnimationSpeed = movementAnimationSpeedCurve.Sample(speedRatio / RUN_RATIO); //Normalize speed ratio

						//Only use walking animation when player is pressing control stick softly
						if (Character.Controller.MovementAxisLength >= .8f &&
							speedRatio < Character.GroundSettings.GetSpeedRatio(Character.BackstepSettings.speed))
						{
							if (speedRatio < .3f)
								speedRatio = .3f;
							targetAnimationSpeed += 1.0f;
						}
					}
				}

				if (Character.MovementState == CharacterController.MovementStates.External) //Disable turning when controlled externally
					groundTurnRatio = 0;
				else if (!Character.IsLockoutActive || Character.ActiveLockoutData.movementMode != LockoutResource.MovementModes.Replace)
					groundTurnRatio = CalculateTurnRatio();
			}

			animationTree.Set(MOVE_REQUEST_PARAMETER, targetMoveState);
			animationTree.Set(MOVE_BLEND_PARAMETER, speedRatio);
			if (disableSpeedSmoothing)
			{
				animationTree.Set(MOVE_SPEED_PARAMETER, targetAnimationSpeed);
				disableSpeedSmoothing = false;
			}
			else
				animationTree.Set(MOVE_SPEED_PARAMETER, Mathf.Lerp((float)animationTree.Get(MOVE_SPEED_PARAMETER), targetAnimationSpeed, SPEED_SMOOTHING));

			groundTurnRatio = Mathf.Lerp(((Vector2)animationTree.Get(TURN_BLEND_PARAMETER)).X, groundTurnRatio, TURN_SMOOTHING); //Blend from animator

			if (Character.IsMovingBackward)
				animationTree.Set(TURN_BLEND_PARAMETER, new Vector2(groundTurnRatio, 0));
			else
				animationTree.Set(TURN_BLEND_PARAMETER, new Vector2(groundTurnRatio, speedRatio));
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

		/// <summary> Blend from -1 <-> 1 of how much the player is turning. </summary>
		private float groundTurnRatio;
		/// <summary> How much should the turning animation be smoothed by? </summary>
		private const float TURN_SMOOTHING = .1f;
		/// <summary> Max amount of turning allowed. </summary>
		private readonly float MAX_TURN_ANGLE = Mathf.Pi * .4f;
		/// <summary>
		/// Calculates turn ratio based on current input with -1 being left and 1 being right.
		/// </summary>
		private float CalculateTurnRatio()
		{
			if (Character.Skills.IsSpeedBreakActive) //Use strafe/movespeed
				return Character.Skills.strafeSettings.GetSpeedRatio(Character.StrafeSpeed * 1.5f);

			float referenceAngle = Character.IsMovingBackward ? Character.PathFollower.ForwardAngle : Character.MovementAngle;
			float inputAngle = Character.GetTargetInputAngle();
			float delta = ExtensionMethods.SignedDeltaAngleRad(referenceAngle, inputAngle);

			if (ExtensionMethods.DotAngle(referenceAngle, inputAngle) < 0) //Input is backwards
				delta = -ExtensionMethods.SignedDeltaAngleRad(referenceAngle + Mathf.Pi, inputAngle);

			delta = Mathf.Clamp(delta, -MAX_TURN_ANGLE, MAX_TURN_ANGLE);
			return delta / MAX_TURN_ANGLE;
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
			if (Character.IsGrindstepping) return; //Use the same angle as the grindrail

			//Don't update directions when externally controlled or on launchers
			float targetRotation = Character.MovementAngle;
			float smoothing = MOVEMENT_ROTATION_SMOOTHING;

			if (Character.MovementState == CharacterController.MovementStates.External)
				targetRotation = ExternalAngle;
			else if (Character.Lockon.IsHomingAttacking) //Face target
				targetRotation = Character.CalculateForwardAngle(Character.Lockon.HomingAttackDirection);
			else if (Character.IsMovingBackward) //Backstepping
				targetRotation = Character.PathFollower.ForwardAngle + groundTurnRatio * Mathf.Pi * .15f;
			else if (Character.IsLockoutActive)
			{
				if (Character.ActiveLockoutData.recenterPlayer)
					targetRotation = Character.PathFollower.ForwardAngle;
				else if (Character.ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe)
				{
					if (Mathf.IsZeroApprox(Character.MoveSpeed)) return;
					float angle = Vector2.Down.AngleTo(new Vector2(Character.StrafeSpeed, Character.MoveSpeed));
					float ratio = Mathf.Clamp(1.0f - Character.Skills.GroundSettings.GetSpeedRatioClamped(Character.MoveSpeed), 0, 1);
					if (!Character.IsOnGround)
						ratio = 1;

					targetRotation += angle * ratio;
				}
			}

			VisualAngle = ExtensionMethods.SmoothDampAngle(VisualAngle, targetRotation, ref rotationVelocity, MOVEMENT_ROTATION_SMOOTHING);
			ApplyVisualRotation();
		}

		/// <summary>
		/// Apply currentRotation on Transform.
		/// </summary>
		private void ApplyVisualRotation() => Rotation = Vector3.Up * VisualAngle;
		#endregion

		#region Drift
		private readonly StringName DRIFT_STATE = "drift_tree";
		private AnimationNodeStateMachinePlayback driftLeftState;
		private AnimationNodeStateMachinePlayback driftRightState;

		private readonly StringName DRIFT_DIRECTION_PARAMETER = "parameters/normal_state/drift_tree/direction_transition/transition_request";
		private readonly StringName DRIFT_START_STATE = "drift-start";
		private readonly StringName DRIFT_LAUNCH_STATE = "drift-launch";

		public void StartDrift(bool isRightTurn)
		{
			driftLeftState.Start(DRIFT_START_STATE);
			driftRightState.Start(DRIFT_START_STATE);

			normalState.Travel(DRIFT_STATE);
			animationTree.Set(DRIFT_DIRECTION_PARAMETER, isRightTurn ? RIGHT_CONSTANT : LEFT_CONSTANT);
		}

		public void LaunchDrift()
		{
			driftLeftState.Travel(DRIFT_LAUNCH_STATE);
			driftRightState.Travel(DRIFT_LAUNCH_STATE);
		}

		public void StopDrift()
		{
			normalState.Travel(GROUND_TREE_STATE);
			animationRoot.Set(MOVE_SEEK_PARAMETER, 0);
		}
		#endregion

		#region Grinding and Balancing Animations
		/// <summary> Reference to the balance state's StateMachinePlayback </summary>
		private AnimationNodeStateMachinePlayback balanceState;

		/// <summary> Is the current balancing direction facing right? </summary>
		private bool IsBalancingRight { get; set; }
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
			IsBalancingRight = true; //Default to facing right
			balanceState.Start(SHUFFLE_RIGHT_PARAMETER, true); //Start with a shuffle

			//Reset current balance
			animationTree.Set(BALANCE_LEFT_LEAN_PARAMETER, 0);
			animationTree.Set(BALANCE_RIGHT_LEAN_PARAMETER, 0);

			SetStateXfade(0); //Don't blend into state
			animationTree.Set(STATE_PARAMETER, BALANCE_STATE); //Turn on balancing animations
			animationTree.Set(BALANCE_GRINDSTEP_ACTIVE_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Abort); //Disable any grindstepping

			UpdateBalanceSpeed();
		}

		private readonly StringName BALANCE_GRINDSTEP_ACTIVE_PARAMETER = "parameters/balance_tree/grindstep_active/request";
		private readonly StringName BALANCE_GRINDSTEP_TRANSITION_PARAMETER = "parameters/balance_tree/grindstep_transition/transition_request";
		/// <summary> How many variations of the grindstep animation are there? </summary>
		private readonly int GRINDSTEP_ANIMATION_VARIATION_COUNT = 4;
		public void StartGrindStep()
		{
			int index = Core.Runtime.randomNumberGenerator.RandiRange(1, GRINDSTEP_ANIMATION_VARIATION_COUNT);
			string targetPose = IsBalancingRight ? "step-right-0" : "step-left-0";
			animationTree.Set(BALANCE_GRINDSTEP_TRANSITION_PARAMETER, targetPose + index.ToString());
			animationTree.Set(BALANCE_GRINDSTEP_ACTIVE_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}

		public void StartGrindShuffle()
		{
			IsBalanceShuffleActive = true;
			IsBalancingRight = !IsBalancingRight;
			balanceState.Travel(IsBalancingRight ? SHUFFLE_RIGHT_PARAMETER : SHUFFLE_LEFT_PARAMETER);
		}

		private float balanceTurnVelocity;
		/// <summary> How much should the balancing animation be smoothed by? </summary>
		private const float BALANCE_TURN_SMOOTHING = .15f;
		public void UpdateBalancing()
		{
			float targetBalance = 0;

			StringName currentNode = balanceState.GetCurrentNode();
			IsBalanceShuffleActive = currentNode == SHUFFLE_LEFT_PARAMETER || currentNode == SHUFFLE_RIGHT_PARAMETER;
			if (IsBalanceShuffleActive)
			{
				if ((IsBalancingRight && currentNode == BALANCE_RIGHT_PARAMETER) ||
				(!IsBalancingRight && currentNode == BALANCE_LEFT_PARAMETER))
					IsBalanceShuffleActive = false;
			}
			else
				targetBalance = CalculateTurnRatio();

			targetBalance = ExtensionMethods.SmoothDamp((float)animationTree.Get(BALANCE_RIGHT_LEAN_PARAMETER), targetBalance, ref balanceTurnVelocity, BALANCE_TURN_SMOOTHING);
			animationTree.Set(BALANCE_RIGHT_LEAN_PARAMETER, targetBalance);
			animationTree.Set(BALANCE_LEFT_LEAN_PARAMETER, -targetBalance);
			UpdateBalanceSpeed();
		}

		private readonly StringName BALANCE_SPEED_PARAMETER = "parameters/balance_tree/balance_speed/scale";
		private readonly StringName BALANCE_WIND_BLEND_PARAMETER = "parameters/balance_tree/wind_blend/blend_position";
		private void UpdateBalanceSpeed()
		{
			float currentSpeed = Character.Skills.grindSettings.GetSpeedRatioClamped(Character.MoveSpeed);
			animationTree.Set(BALANCE_SPEED_PARAMETER, currentSpeed + .8f);
			animationTree.Set(BALANCE_WIND_BLEND_PARAMETER, currentSpeed);
		}
		#endregion

		#region Sidle
		public bool IsSidleMoving => sidleRightState.GetFadingFromNode().IsEmpty && sidleRightState.GetCurrentNode() == SIDLE_LOOP_STATE_PARAMETER;

		private AnimationNodeStateMachinePlayback sidleRightState;
		private AnimationNodeStateMachinePlayback sidleLeftState;

		private readonly StringName SIDLE_LOOP_STATE_PARAMETER = "sidle-loop";
		private readonly StringName SIDLE_DAMAGE_STATE_PARAMETER = "sidle-damage-loop";
		private readonly StringName SIDLE_HANG_STATE_PARAMETER = "sidle-hang-loop";
		private readonly StringName SIDLE_HANG_FALL_STATE_PARAMETER = "sidle-hang-fall";
		private readonly StringName SIDLE_FALL_STATE_PARAMETER = "sidle-fall";

		private readonly StringName SIDLE_SPEED_PARAMETER = "parameters/sidle_tree/sidle_speed/scale";
		private readonly StringName SIDLE_SEEK_PARAMETER = "parameters/sidle_tree/sidle_seek/seek_request";
		private readonly StringName SIDLE_DIRECTION_PARAMETER = "parameters/sidle_tree/direction_transition/transition_request";

		public void StartSidle(bool facingRight)
		{
			if (Character.IsRespawning) //Cut directly
				SetStateXfade(0);
			else //Quick crossfade into sidle
				SetStateXfade(0.1f);

			sidleRightState.Start(SIDLE_LOOP_STATE_PARAMETER);
			sidleLeftState.Start(SIDLE_LOOP_STATE_PARAMETER);
			animationTree.Set(STATE_PARAMETER, SIDLE_STATE);
			animationTree.Set(SIDLE_DIRECTION_PARAMETER, facingRight ? RIGHT_CONSTANT : LEFT_CONSTANT);
		}

		public void UpdateSidle(float cyclePosition)
		{
			animationTree.Set(SIDLE_SPEED_PARAMETER, 0f);
			animationTree.Set(SIDLE_SEEK_PARAMETER, cyclePosition * .8f); //Sidle animation length is .8 seconds, so normalize cycle position.
		}

		/// <summary>
		/// Starts damage (stagger) animation.
		/// </summary>
		public void SidleDamage()
		{
			animationTree.Set(SIDLE_SPEED_PARAMETER, 1f);
			animationTree.Set(SIDLE_SEEK_PARAMETER, -1);

			sidleRightState.Travel(SIDLE_DAMAGE_STATE_PARAMETER);
			sidleLeftState.Travel(SIDLE_DAMAGE_STATE_PARAMETER);
		}

		/// <summary>
		/// Start hanging onto the ledge.
		/// </summary>
		public void SidleHang()
		{
			sidleRightState.Travel(SIDLE_HANG_STATE_PARAMETER);
			sidleLeftState.Travel(SIDLE_HANG_STATE_PARAMETER);
		}

		/// <summary>
		/// Fall while hanging on the ledge.
		/// </summary>
		public void SidleHangFall()
		{
			sidleRightState.Travel(SIDLE_HANG_FALL_STATE_PARAMETER);
			sidleLeftState.Travel(SIDLE_HANG_FALL_STATE_PARAMETER);
		}

		/// <summary>
		/// Fall from the ledge.
		/// </summary>
		public void SidleFall()
		{
			sidleRightState.Travel(SIDLE_FALL_STATE_PARAMETER);
			sidleLeftState.Travel(SIDLE_FALL_STATE_PARAMETER);
		}

		/// <summary>
		/// Recover back to the ledge.
		/// </summary>
		public void SidleRecovery()
		{
			sidleRightState.Travel(SIDLE_LOOP_STATE_PARAMETER);
			sidleLeftState.Travel(SIDLE_LOOP_STATE_PARAMETER);
		}
		#endregion
	}
}