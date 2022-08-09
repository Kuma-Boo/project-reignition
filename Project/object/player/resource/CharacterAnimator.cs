using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Responsible for playing the player's animations and visual effects.
	/// </summary>
	public class CharacterAnimator : Spatial
	{
		[Export]
		public NodePath animator;
		private AnimationTree _animator;
		[Export]
		public NodePath root;
		private Spatial _root;
		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			_animator = GetNode<AnimationTree>(animator);
			_animator.Active = true;
			_root = GetNode<Spatial>(root);
		}

		private const string COUNTDOWN_PARAMETER = "parameters/countdown/active";
		public void Countdown()
		{
			_animator.Set(COUNTDOWN_PARAMETER, true);
		}

		public void PlayAnimation(string anim) //Play a specific animation
		{
			
		}

		#region Grinding Animation
		private const string IS_BALANCING_STATE = "parameters/balancing/current";
		private const string BALANCE_STATE_PARAMETER = "parameters/balance_state/active";
		private const string BALANCE_LEFT_STATE_PARAMETER = "parameters/balance_state/balance_left/blend_position";
		private const string BALANCE_RIGHT_STATE_PARAMETER = "parameters/balance_state/balance_right/blend_position";
		public void StartGrinding()
		{
			_animator.Set(IS_BALANCING_STATE, 1); //Turn on grinding animations
			_animator.Set(BALANCE_STATE_PARAMETER, true);
			_animator.Set(BALANCE_LEFT_STATE_PARAMETER, 0);
			_animator.Set(BALANCE_RIGHT_STATE_PARAMETER, 0);
		}

		public AnimationNodeStateMachinePlayback GrindingState => _animator.Get("parameters/balance_state/playback") as AnimationNodeStateMachinePlayback;

		public void StopGrinding()
		{
			_animator.Set("parameters/balancing/current", 0); //Turn off grinding animations
		}
		#endregion

		#region Normal Animation
		private float strafeTilt;

		private const string CROUCH_PARAMETER = "parameters/ground_state/IsCrouching/current";

		private const string JUMP_TRIGGER_PARAMETER = "parameters/air_state/jump/active";
		private const string JUMPING_PARAMETER = "parameters/air_state/IsJumping/current";
		private const string JUMPDASH_PARAMETER = "parameters/air_state/IsJumpDashing/current";
		private const string FALL_TRIGGER_PARAMETER = "parameters/air_state/fall/active";
		private const string FALL_RESET_PARAMETER = "parameters/air_state/fall_reset/seek_position";
		private const string JUMP_CANCEL_TIME_PARAMETER = "parameters/air_state/jump_cancel_time/scale";

		public void Jump()
		{
			_animator.Set(JUMPING_PARAMETER, 1);
			_animator.Set(JUMP_TRIGGER_PARAMETER, true);
		}

		private const string BACKFLIP_TRIGGER_PARAMETER = "parameters/air_state/backflip/active";
		public void Backflip()
		{
			_animator.Set(BACKFLIP_TRIGGER_PARAMETER, true);
			_animator.Set(FALL_TRIGGER_PARAMETER, false);
			_animator.Set(FALL_RESET_PARAMETER, 0);
			Rotation = Vector3.Zero; //Reset Rotation
		}

		public void JumpAccel()
		{

		}

		public void Stomp()
		{
			FallAnimation();
			_animator.Set(JUMP_CANCEL_TIME_PARAMETER, 1.5f);
		}

		public void FallAnimation()
		{
			_animator.Set(JUMPING_PARAMETER, 0);
			_animator.Set(JUMP_CANCEL_TIME_PARAMETER, 1f);
			_animator.Set(FALL_TRIGGER_PARAMETER, true);
			_animator.Set(FALL_RESET_PARAMETER, 0);
		}


		public void ResetLocalRotation()
		{
			Rotation = Vector3.Zero;
		}

		public void SetForwardDirection(Vector3 direction)
		{
			Transform t = _root.GlobalTransform;
			t.basis.z = direction;
			t.basis.y = Character.worldDirection;
			t.basis.x = -t.basis.z.Cross(t.basis.y);
			t.basis = t.basis.Orthonormalized();
			_root.GlobalTransform = t;
		}

		private const string GROUND_PARAMETER = "parameters/IsGrounded/current";
		private const float MOVEMENT_DEADZONE = .2f;
		public void UpdateAnimation()
		{
			//Don't update directions when externally controlled or on launchers
			if (Character.MovementState != CharacterController.MovementStates.External && Character.MovementState != CharacterController.MovementStates.Launcher)
			{
				SetForwardDirection(Character.PathFollower.MovementDirection);

				if (Character.Controller.MovementAxis != Vector2.Zero || Character.Soul.IsSpeedBreakActive)
				{
					float targetRotation = 0;
					if (!Character.Soul.IsSpeedBreakActive && Character.SpeedRatio <= .8f)
					{
						if (Character.MoveSpeed > MOVEMENT_DEADZONE || Mathf.Abs(Character.StrafeSpeed) > MOVEMENT_DEADZONE)
							targetRotation = new Vector2(Character.StrafeSpeed, -Character.MoveSpeed).Normalized().AngleTo(Vector2.Up);
						else
							targetRotation = -Character.Controller.MovementAxis.Normalized().AngleTo(Vector2.Down);

						targetRotation = Mathf.Clamp(targetRotation, -Mathf.Pi * .5f, Mathf.Pi * .5f);
					}

					Rotation = Rotation.LinearInterpolate(Vector3.Up * targetRotation, .2f);
				}
			}

			_animator.Set(GROUND_PARAMETER, Character.IsOnGround ? 0 : 1);
			if (Character.IsOnGround)
				GroundAnimations();
			else
				AirAnimations();
		}

		private float strafeVelocity;
		private const string MOVEMENT_STATE_PARAMETER = "parameters/ground_state/MoveState/current";
		private void GroundAnimations()
		{
			int transition = 1; //Idle
			if (Character.SpeedRatio < 0) //Backstep
				transition = 0;
			else if (Character.SpeedRatio >= 1) //Running
				transition = 3;
			else if(!Character.IsIdling) //Walk -> Jog
				transition = 2;

			_animator.Set(MOVEMENT_STATE_PARAMETER, transition);
			_animator.Set(CROUCH_PARAMETER, Character.IsCrouching ? 1 : 0);

			float targetStrafeTilt = 0;

			if (Character.ControlLockoutData == null && Character.Controller.MovementAxis != Vector2.Zero)
			{
				float targetDirection = new Vector2(Character.GetStrafeInputValue(), -Mathf.Abs(Character.GetMovementInputValue())).AngleTo(Vector2.Up);
				targetStrafeTilt = -Mathf.Clamp((targetDirection - Rotation.y) / Mathf.Pi * .5f, -1, 1);
			}
			strafeTilt = ExtensionMethods.SmoothDamp(strafeTilt, targetStrafeTilt, ref strafeVelocity, .2f);

			float moveAnimationSpeed = 1f;
			
			if(Character.SpeedRatio < 0)
				moveAnimationSpeed = .8f * Mathf.Abs(Character.backstepSettings.GetSpeedRatio(Character.MoveSpeed));
			else if(!Character.IsIdling)
				moveAnimationSpeed = Mathf.Max(Character.SpeedRatio, Mathf.Abs(Character.runningStrafeSettings.GetSpeedRatioClamped(Character.StrafeSpeed)));

			_animator.Set("parameters/ground_state/Jog/blend_position", new Vector2(strafeTilt, moveAnimationSpeed));
			_animator.Set("parameters/ground_state/Run/blend_position", strafeTilt);

			_animator.Set("parameters/ground_state/MoveSpeed/scale", Mathf.Lerp(1f, 2.5f, moveAnimationSpeed));
		}

		private void AirAnimations()
		{
			bool isJumpDashing = Character.ActionState == CharacterController.ActionStates.JumpDashing || Character.ActionState == CharacterController.ActionStates.AccelJump;
			_animator.Set(JUMPDASH_PARAMETER, isJumpDashing ? 1 : 0);

			if ((int)_animator.Get(JUMPING_PARAMETER) == 1)
			{
				if (Character.ActionState != CharacterController.ActionStates.Jumping || Character.VerticalSpeed <= 5f)
					FallAnimation();
			}
		}
		#endregion
	}
}