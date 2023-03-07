using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Handles sidle behaviour.
	/// </summary>
	public partial class SidleTrigger : Area3D
	{
		[Signal]
		public delegate void ActivatedEventHandler();
		[Signal]
		public delegate void DeactivatedEventHandler();

		/// <summary> Reference to the active sidle trigger. </summary>
		public static SidleTrigger Instance { get; private set; }
		/// <summary> Reference to the active foothold. </summary>
		public static FootholdTrigger CurrentFoothold { get; set; }
		/// <summary> Should the player grab a foot hold when taking damage? </summary>
		private bool IsOverFoothold => CurrentFoothold != null;

		/// <summary> Which way to sidle? </summary>
		[Export]
		private bool isFacingRight = true;
		[Export]
		private LockoutResource lockout;

		private bool isActive;
		private bool isInteractingWithPlayer;
		private CharacterController Character => CharacterController.instance;
		private InputManager.Controller Controller => InputManager.controller;

		private float velocity;
		private float cycleTimer;
		/// <summary> Maximum amount of cycles in a single second. </summary>
		private const float CYCLE_FREQUENCY = 3.4f;
		/// <summary> How much to move each cycle.  </summary>
		private const float CYCLE_DISTANCE = 3.8f;
		/// <summary> Smoothing to apply when accelerating.  </summary>
		private const float TRACTION_SMOOTHING = .1f;
		/// <summary> Smoothing to apply when slowing down.  </summary>
		private const float FRICTION_SMOOTHING = .4f;

		public override void _Ready() => LevelSettings.instance.ConnectRespawnSignal(this);

		public override void _PhysicsProcess(double _)
		{
			if (!isInteractingWithPlayer ||
			(Character.IsRespawning && Character.ActionState == CharacterController.ActionStates.Damaged)) return;

			if (isActive)
			{
				if (damageState == DamageStates.Disabled)
					UpdateSidle();
				else
					UpdateSidleDamage();
			}
			else if (Character.IsOnGround)
			{
				if (Character.ActionState == CharacterController.ActionStates.Normal)
					StartSidle(); //Allows player to slide through sidle section if they know what they're doing
				else if (Character.ActionState == CharacterController.ActionStates.Crouching && Mathf.IsZeroApprox(Character.MoveSpeed))
					Character.ResetActionState();
			}
		}

		private void StartSidle()
		{
			isActive = true;
			velocity = 0;
			cycleTimer = 0;
			damageState = DamageStates.Disabled;

			Character.IsOnGround = true;
			Character.StartExternal(this, Character.PathFollower, .2f);

			Character.Animator.ExternalAngle = 0;
			Character.Animator.SnapRotation(0);
			Character.Animator.StartSidle(isFacingRight);

			if (!Character.IsConnected(CharacterController.SignalName.Knockback, new Callable(this, MethodName.OnPlayerDamaged)))
				Character.Connect(CharacterController.SignalName.Knockback, new Callable(this, MethodName.OnPlayerDamaged));
		}

		private void UpdateSidle()
		{
			float targetVelocity = (isFacingRight ? Controller.MovementAxis.X : -Controller.MovementAxis.X) * CYCLE_FREQUENCY;
			if (Mathf.IsZeroApprox(velocity) || Mathf.Sign(targetVelocity) == Mathf.Sign(velocity))
				velocity = Mathf.Lerp(velocity, targetVelocity, TRACTION_SMOOTHING);
			else
				velocity = Mathf.Lerp(velocity, targetVelocity, FRICTION_SMOOTHING);

			//Check walls
			Vector3 castVector = Character.PathFollower.Forward() * (Character.CollisionRadius + Mathf.Abs(velocity * PhysicsManager.physicsDelta));
			castVector *= Mathf.Sign(velocity);
			RaycastHit hit = this.CastRay(Character.CenterPosition, castVector, Runtime.Instance.environmentMask);
			Debug.DrawRay(Character.CenterPosition, castVector, hit ? Colors.Red : Colors.White);
			if (hit) //Kill speed
				velocity = (hit.distance - Character.CollisionRadius) * Mathf.Sign(velocity);

			if (Mathf.IsZeroApprox(velocity)) return;

			cycleTimer += velocity * PhysicsManager.physicsDelta;
			if (cycleTimer >= 1)
				cycleTimer--;
			else if (cycleTimer < 0)
				cycleTimer++;

			Character.Animator.UpdateSidle(cycleTimer);
			Character.MoveSpeed = Character.Skills.sidleMovementCurve.Sample(cycleTimer) * velocity * CYCLE_DISTANCE;
			Character.PathFollower.Progress += Character.MoveSpeed * PhysicsManager.physicsDelta;
			Character.UpdateExternalControl();
		}

		private void StopSidle()
		{
			if (!isActive) return; //Already deactivated

			isActive = false;
			Character.RemoveLockoutData(lockout);

			Character.MovementAngle = Character.MoveSpeed < 0 ? Character.PathFollower.BackAngle : Character.PathFollower.ForwardAngle;
			Character.MoveSpeed = Mathf.Abs(Character.MoveSpeed);

			if (Character.ExternalController == this)
				Character.ResetMovementState();

			Character.Animator.ResetState(.1f);
			Character.Animator.SnapRotation(Character.PathFollower.ForwardAngle);

			damageState = DamageStates.Disabled;
			Character.Disconnect(CharacterController.SignalName.Knockback, new Callable(this, MethodName.OnPlayerDamaged));
		}

		#region Damage
		/// <summary> Is the player currently being damaged? </summary>
		private DamageStates damageState;
		private enum DamageStates
		{
			Disabled, //Normal sidle movement
			Stagger, //Playing stagger animation
			Falling, //Falling to rail
			Hanging, //Jump recovery allowed
			Recovery, //Recovering back to the ledge
			Respawning
		}

		private const float DAMAGE_STAGGER_LENGTH = .8f; //How long does the stagger animation last?
		private const float DAMAGE_HANG_LENGTH = 5f; //How long can the player hang onto the rail?
		private const float DAMAGE_TRANSITION_LENGTH = 1f; //How long is the transition from staggering to hanging?
		private const float RECOVERY_LENGTH = .84f; //How long does the recovery take?

		/// <summary>
		/// Called when the player hits a hazard.
		/// </summary>
		private void OnPlayerDamaged()
		{
			//Invincible/Damage routine has already started
			if (Character.IsInvincible || damageState != DamageStates.Disabled) return;

			if (LevelSettings.instance.CurrentRingCount == 0)
			{
				StopSidle();
				Character.StartKnockback(new CharacterController.KnockbackSettings());
				return;
			}

			Character.TakeDamage();
			Character.StartInvincibility();

			damageState = DamageStates.Stagger;
			velocity = 0;
			cycleTimer = 0;

			Character.Effect.PlayVoice("sidle hurt");
			Character.Animator.SidleDamage();
		}

		/// <summary>
		/// Processes player when being damaged.
		/// </summary>
		private void UpdateSidleDamage()
		{
			cycleTimer += PhysicsManager.physicsDelta;
			switch (damageState)
			{
				case DamageStates.Hanging:
					if (cycleTimer >= DAMAGE_HANG_LENGTH) //Fall
					{
						StartRespawn();
						Character.Animator.SidleHangFall();
					}
					else if (Controller.jumpButton.wasPressed) //Process inputs
					{
						//Jump back to the ledge
						cycleTimer = 0;
						damageState = DamageStates.Recovery;

						Character.Effect.PlayVoice("grunt");
						Character.Animator.SidleRecovery();
					}
					break;

				case DamageStates.Stagger:
					if (cycleTimer >= DAMAGE_STAGGER_LENGTH) //Fall
					{
						cycleTimer = 0;
						if (IsOverFoothold)
						{
							damageState = DamageStates.Falling;
							Character.IsOnGround = false;
							Character.Animator.SidleHang();
						}
						else
						{
							StartRespawn();
							Character.Animator.SidleFall();
						}
					}
					break;

				case DamageStates.Falling:
					if (cycleTimer >= DAMAGE_TRANSITION_LENGTH)
					{
						cycleTimer = 0;
						damageState = DamageStates.Hanging;
					}
					break;

				case DamageStates.Recovery:
					if (!Character.IsOnGround)
					{
						if (cycleTimer < RECOVERY_LENGTH)
							return;

						Character.IsOnGround = true;
						Character.Effect.PlayLandingFX();
					}

					cycleTimer = 0;
					if (Character.Animator.IsSidleMoving) //Finished
					{
						damageState = DamageStates.Disabled;
						Character.Animator.UpdateSidle(cycleTimer);
					}
					break;

				case DamageStates.Respawning:
					if (cycleTimer > .5f)
						Character.StartRespawn();
					break;
			}
		}

		/// <summary>
		/// Tells the player to start respawning.
		/// </summary>
		private void StartRespawn()
		{
			cycleTimer = 0;
			damageState = DamageStates.Respawning;
		}

		public void Respawn()
		{
			if (!isActive) return;

			StartSidle();
			Character.Animator.UpdateSidle(cycleTimer);
		}
		#endregion

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			Instance = this;
			isInteractingWithPlayer = true;

			//Apply state
			Character.Skills.IsSpeedBreakEnabled = false; //Disable speed break
			Character.AddLockoutData(lockout);

			float dot = ExtensionMethods.DotAngle(Character.MovementAngle, Character.PathFollower.ForwardAngle);
			if (dot < 0)
			{
				Character.MoveSpeed = -Mathf.Abs(Character.MoveSpeed);
				Character.MovementAngle = Character.PathFollower.ForwardAngle;
				Character.PathFollower.Resync();
			}

			EmitSignal(SignalName.Activated);
		}

		public void OnExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			Instance = null;
			isInteractingWithPlayer = false;

			StopSidle();
			EmitSignal(SignalName.Deactivated);
		}
	}
}
