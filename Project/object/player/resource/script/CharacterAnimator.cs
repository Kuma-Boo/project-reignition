using Godot;

//TODO Rewrite this entire script
namespace Project.Gameplay
{
	/// <summary>
	/// Responsible for playing the player's animations and visual effects.
	/// </summary>
	public partial class CharacterAnimator : Node3D
	{
		[Export]
		private AnimationTree animator;
		private CharacterController Character => CharacterController.instance;

		public override void _Ready() => animator.Active = true; //Activate animator

		#region Oneshot event animations
		private readonly StringName COUNTDOWN_PARAMETER = "parameters/countdown/active";
		public void Countdown()
		{
			animator.Set(COUNTDOWN_PARAMETER, true);
		}
		#endregion

		public void PlayAnimation(string anim) //Play a specific animation
		{

		}

		#region Grinding and Balancing Animations
		private const string IS_BALANCING_STATE = "parameters/balancing/current";
		private const string BALANCE_STATE_PARAMETER = "parameters/balance_state/active";
		private const string BALANCE_LEFT_STATE_PARAMETER = "parameters/balance_state/balance_left/blend_position";
		private const string BALANCE_RIGHT_STATE_PARAMETER = "parameters/balance_state/balance_right/blend_position";
		public void StartGrinding()
		{
			animator.Set(IS_BALANCING_STATE, 1); //Turn on grinding animations
			animator.Set(BALANCE_STATE_PARAMETER, true);
			animator.Set(BALANCE_LEFT_STATE_PARAMETER, 0);
			animator.Set(BALANCE_RIGHT_STATE_PARAMETER, 0);
		}

		//public AnimationNodeStateMachinePlayback GrindingState => _animator.Get("parameters/balance_state/playback") as AnimationNodeStateMachinePlayback;

		public void StopGrinding()
		{
			animator.Set("parameters/balancing/current", 0); //Turn off grinding animations
		}
		#endregion

		#region Normal Animation
		private readonly StringName NORMAL_STATE_PARAMETER = "parameters/normal_state/playback";
		/// <summary> Gets the normal state's StateMachinePlayback </summary>
		private AnimationNodeStateMachinePlayback NormalState => (AnimationNodeStateMachinePlayback)animator.Get(NORMAL_STATE_PARAMETER);

		private readonly StringName FALL_STATE_PARAMETER = "fall";
		private readonly StringName JUMP_CANCEL_TIME_PARAMETER = "parameters/air_state/jump_cancel_time/scale";

		private bool isJumping;
		private readonly StringName JUMP_STATE_PARAMETER = "jump";
		public void Jump()
		{
			isJumping = true;
			NormalState.Start(JUMP_STATE_PARAMETER);
		}

		private readonly StringName BACKFLIP_STATE_PARAMETER = "backflip";
		public void Backflip()
		{
			NormalState.Start(BACKFLIP_STATE_PARAMETER);
		}

		public void JumpAccel()
		{
		}

		public void Stomp()
		{
			animator.Set(JUMP_CANCEL_TIME_PARAMETER, 1.5f);
		}

		private readonly StringName GROUND_STATE_PARAMETER = "ground_tree";
		private readonly StringName AIR_STATE_PARAMETER = "air_tree";
		private const float MOVEMENT_DEADZONE = .2f;
		public void UpdateAnimation()
		{
			//TODO Play landing animation
			if (Character.JustLandedOnGround)
				NormalState.Start(GROUND_STATE_PARAMETER);

			if (Character.IsOnGround)
				GroundAnimations();
			else
				AirAnimations();

			UpdateRotation();
		}

		private readonly StringName JOG_BLEND_PARAMETER = "parameters/normal_state/ground_tree/jog_blend/blend_position";
		private readonly StringName RUN_BLEND_PARAMETER = "parameters/normal_state/ground_tree/run_blend/blend_position";
		private readonly StringName MOVE_TRANSITION_PARAMETER = "parameters/normal_state/ground_tree/move_transition/current";
		private readonly StringName MOVE_SPEED_PARAMETER = "parameters/normal_state/ground_tree/move_speed/scale";

		[Export]
		private Curve movementAnimationSpeedCurve;
		/// <summary> What speedratio should be considered as fully running? </summary>
		private const float RUN_RATIO = .9f;
		/// <summary> How much should the animation speed be smoothed by? </summary>
		private const float SPEED_SMOOTHING = .1f;
		private void GroundAnimations()
		{
			//TODO Speed break animation
			if (Character.Skills.IsSpeedBreakActive)
			{
				return;
			}

			float speedRatio = Mathf.Abs(Character.GroundSettings.GetSpeedRatio(Character.MoveSpeed));
			float animationSpeed = 1f;
			int targetMoveState = 0; //Default to idling

			if (!Mathf.IsZeroApprox(Character.MoveSpeed))
			{
				if (Character.IsMovingBackward) //Backstep
				{
					targetMoveState = 1;
					speedRatio = Mathf.Abs(Character.BackstepSettings.GetSpeedRatio(Character.MoveSpeed));
					animationSpeed = 1.2f + speedRatio * .4f;
				}
				else if (speedRatio >= RUN_RATIO) //Running
				{
					targetMoveState = 3;
					animationSpeed = Mathf.Clamp(animationSpeed, 2f + (speedRatio - RUN_RATIO), 2.5f);
				}
				else //Jogging
				{
					targetMoveState = 2;
					speedRatio = speedRatio / RUN_RATIO; //Normalize speed ratio
					animationSpeed = movementAnimationSpeedCurve.Sample(speedRatio);
					animator.Set(JOG_BLEND_PARAMETER, speedRatio);
				}
			}

			animator.Set(MOVE_TRANSITION_PARAMETER, targetMoveState);
			animator.Set(MOVE_SPEED_PARAMETER, Mathf.Lerp((float)animator.Get(MOVE_SPEED_PARAMETER), animationSpeed, SPEED_SMOOTHING));
		}

		private void AirAnimations()
		{
			bool isJumpDashing = Character.ActionState == CharacterController.ActionStates.JumpDash || Character.ActionState == CharacterController.ActionStates.AccelJump;

			if (isJumping)
			{
				if (Character.ActionState != CharacterController.ActionStates.Jumping || Character.VerticalSpd <= 5f)
				{
					isJumping = false;
					NormalState.Travel(FALL_STATE_PARAMETER);
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
			ApplyRotation();
		}

		/// <summary>
		/// Calculates the target visual rotation and applies it.
		/// </summary>
		private void UpdateRotation()
		{
			//Don't update directions when externally controlled or on launchers
			float targetRotation = Character.MovementAngle;
			float smoothing = MOVEMENT_ROTATION_SMOOTHING;

			if (Character.Lockon.IsHomingAttacking) //Face target
				targetRotation = Character.CalculateForwardAngle(Character.Lockon.HomingAttackDirection);
			else if (Character.Skills.IsSpeedBreakActive || Character.IsMovingBackward) //Speed break, use path's forward direction
				targetRotation = Character.PathFollower.ForwardAngle;
			else if (Character.MovementState == CharacterController.MovementStates.External)
				targetRotation = ExternalAngle;

			VisualAngle = ExtensionMethods.SmoothDampAngle(VisualAngle, targetRotation, ref rotationVelocity, MOVEMENT_ROTATION_SMOOTHING);
			ApplyRotation();
		}

		/// <summary>
		/// Apply currentRotation on Transform.
		/// </summary>
		private void ApplyRotation() => Rotation = Vector3.Up * VisualAngle;
		#endregion
	}
}