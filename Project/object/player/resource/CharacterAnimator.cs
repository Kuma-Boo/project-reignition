using Godot;
using Project.Core;

namespace Project.Gameplay
{
	public class CharacterAnimator : Spatial
	{
		[Export]
		public NodePath animator;
		private AnimationTree _animator;
		[Export]
		public NodePath root;
		private Spatial _root;
		private CharacterController _character;

		public override void _Ready()
		{
			_animator = GetNode<AnimationTree>(animator);
			_animator.Active = true;
			_root = GetNode<Spatial>(root);

			_character = GetParent() as CharacterController;
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


		private const string GROUND_PARAMETER = "parameters/IsGrounded/current";
		public void UpdateAnimation()
		{
			if (ringParticleTimer != 0)
			{
				ringParticleTimer = Mathf.MoveToward(ringParticleTimer, 0, PhysicsManager.physicsDelta);

				if (ringParticleTimer == 0)
					_ringParticleEffect.Emitting = false;
			}

			if (_character.MovementState != CharacterController.MovementStates.Automation)
			{
				Transform t = _root.GlobalTransform;
				t.basis.z = _character.ForwardDirection;
				t.basis.y = _character.worldDirection;
				t.basis.x = -t.basis.z.Cross(t.basis.y);
				t.basis = t.basis.Orthonormalized();
				_root.GlobalTransform = t;
			}

			_animator.Set(GROUND_PARAMETER, _character.IsOnGround ? 0 : 1);
			if (_character.IsOnGround)
				GroundAnimations();
			else
				AirAnimations();

			if (!_character.IsIdling)
			{
				float targetRotation = 0;
				if (_character.SpeedRatio <= .8f)
					targetRotation = _character.MoveSpeed >= 0 ? new Vector2(_character.StrafeSpeed, -_character.MoveSpeed).Normalized().AngleTo(Vector2.Up) * .8f : 0;
				Rotation = Rotation.LinearInterpolate(Vector3.Up * targetRotation, .2f);
			}
		}

		private float strafeVelocity;
		private const string MOVEMENT_STATE_PARAMETER = "parameters/ground_state/MoveState/current";
		private void GroundAnimations()
		{
			int transition = 1; //Idle
			if (_character.SpeedRatio < 0) //Backstep
				transition = 0;
			else if (_character.SpeedRatio >= 1) //Running
				transition = 3;
			else if(!_character.IsIdling) //Walk -> Jog
				transition = 2;

			_animator.Set(MOVEMENT_STATE_PARAMETER, transition);
			_animator.Set(CROUCH_PARAMETER, _character.IsCrouching ? 1 : 0);

			float targetStrafeTilt = 0;

			if (!_character.IsIdling && _character.ControlLockoutData == null)
			{
				if (InputManager.controller.MovementAxis != Vector2.Zero)
				{
					float targetDirection = new Vector2(_character.GetStrafeInputValue(), -Mathf.Abs(_character.GetMovementInputValue())).AngleTo(Vector2.Up);
					targetStrafeTilt = -Mathf.Clamp((targetDirection - Rotation.y) / Mathf.Pi * .5f, -1, 1);
				}
			}
			strafeTilt = ExtensionMethods.SmoothDamp(strafeTilt, targetStrafeTilt, ref strafeVelocity, .2f);

			float moveAnimationSpeed = 1f;
			
			if(_character.SpeedRatio < 0)
				moveAnimationSpeed = .8f * Mathf.Abs(_character.backstepSettings.GetSpeedRatio(_character.MoveSpeed));
			else if(!_character.IsIdling)
				moveAnimationSpeed = Mathf.Max(_character.SpeedRatio, Mathf.Abs(_character.runningStrafeSettings.GetSpeedRatioClamped(_character.StrafeSpeed)));

			_animator.Set("parameters/ground_state/Jog/blend_position", new Vector2(strafeTilt, moveAnimationSpeed));
			_animator.Set("parameters/ground_state/Run/blend_position", strafeTilt);

			_animator.Set("parameters/ground_state/MoveSpeed/scale", Mathf.Lerp(1f, 2.5f, moveAnimationSpeed));
		}

		private void AirAnimations()
		{
			_animator.Set(JUMPDASH_PARAMETER, _character.IsJumpDashing || _character.IsAccelerationJumping ? 1 : 0);

			if ((int)_animator.Get(JUMPING_PARAMETER) == 1)
			{
				if (_character.ActionState != CharacterController.ActionStates.Jumping || _character.VerticalSpeed <= 5f)
					FallAnimation();
			}
		}

		#region VFX
		[Export]
		public NodePath ringParticleEffect;
		private Particles _ringParticleEffect;
		private float ringParticleTimer;

		public void PlayRingParticleEffect()
		{
			if (_ringParticleEffect == null)
				_ringParticleEffect = GetNode<Particles>(ringParticleEffect);

			ringParticleTimer = .2f;
			_ringParticleEffect.Emitting = true;
		}
		#endregion
		#endregion
	}
}