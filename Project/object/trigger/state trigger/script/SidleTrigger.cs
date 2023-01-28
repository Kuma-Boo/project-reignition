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

		private float velocity;
		private float currentCyclePosition;

		private bool isActive;
		private bool isInteractingWithPlayer;
		private CharacterController Character => CharacterController.instance;
		private InputManager.Controller Controller => InputManager.controller;

		/// <summary> Maximum amount of cycles in a single second. </summary>
		private const float CYCLE_FREQUENCY = 3.2f;
		/// <summary> Smoothing to apply when accelerating.  </summary>
		private const float TRACTION_SMOOTHING = .1f;
		/// <summary> Smoothing to apply when slowing down.  </summary>
		private const float FRICTION_SMOOTHING = .4f;
		/// <summary> How much to move each cycle.  </summary>
		private const float CYCLE_DISTANCE = 3.2f;

		public override void _PhysicsProcess(double _)
		{
			if (!isInteractingWithPlayer) return;

			if (isActive)
			{
				if (isDamaged)
					UpdateSidleDamage();
				else
					UpdateSidle();
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
			currentCyclePosition = 0;

			Character.IsOnGround = true;
			Character.StartExternal(this, Character.PathFollower, .2f);

			Character.Animator.ExternalAngle = 0;
			Character.Animator.SnapRotation(0);
			Character.Animator.StartSidle(isFacingRight);

			Character.Connect(CharacterController.SignalName.Damaged, new Callable(this, MethodName.OnPlayerDamaged));
			EmitSignal(SignalName.Activated);
		}

		private void UpdateSidle()
		{
			float targetVelocity = (isFacingRight ? Controller.MovementAxis.x : -Controller.MovementAxis.x) * CYCLE_FREQUENCY;
			if (Mathf.IsZeroApprox(velocity) || Mathf.Sign(targetVelocity) == Mathf.Sign(velocity))
				velocity = Mathf.Lerp(velocity, targetVelocity, TRACTION_SMOOTHING);
			else
				velocity = Mathf.Lerp(velocity, targetVelocity, FRICTION_SMOOTHING);

			//Check walls
			Vector3 castVector = Character.PathFollower.Forward() * (Character.CollisionRadius + Mathf.Abs(velocity * PhysicsManager.physicsDelta));
			castVector *= Mathf.Sign(velocity);
			RaycastHit hit = this.CastRay(Character.CenterPosition, castVector, RuntimeConstants.Instance.environmentMask);
			Debug.DrawRay(Character.CenterPosition, castVector, hit ? Colors.Red : Colors.White);
			if (hit) //Kill speed
				velocity = (hit.distance - Character.CollisionRadius) * Mathf.Sign(velocity);

			if (Mathf.IsZeroApprox(velocity))
				return;

			currentCyclePosition += velocity * PhysicsManager.physicsDelta;
			if (currentCyclePosition >= 1)
				currentCyclePosition--;
			else if (currentCyclePosition < 0)
				currentCyclePosition++;

			Character.Animator.UpdateSidle(currentCyclePosition);
			Character.MoveSpeed = Character.Skills.sidleMovementCurve.Sample(currentCyclePosition) * velocity * CYCLE_DISTANCE;
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

			isDamaged = false;
			Character.Disconnect(CharacterController.SignalName.Damaged, new Callable(this, MethodName.OnPlayerDamaged));

			EmitSignal(SignalName.Deactivated);
		}

		#region Damage
		/// <summary> Is the player currently being damaged? </summary>
		private bool isDamaged;
		private const float DAMAGE_HANG_LENGTH = 5f; //How long can the player hang onto the rail?

		/// <summary>
		/// Called when the player hits a hazard.
		/// </summary>
		private void OnPlayerDamaged()
		{
			if (isDamaged) return; //Damage routine has already started

			isDamaged = true;
			currentCyclePosition = 0;
			Character.Animator.SidleDamage(IsOverFoothold);
		}

		/// <summary>
		/// Processes player when being damaged
		/// </summary>
		private void UpdateSidleDamage()
		{
			if (Character.Animator.IsSidleHanging) //Process inputs
			{
				currentCyclePosition += PhysicsManager.physicsDelta;
				if (currentCyclePosition >= DAMAGE_HANG_LENGTH) //Fall
				{

				}

				if (Controller.jumpButton.wasPressed) //Jump back to the ledge
				{
					currentCyclePosition = 0;
					Character.Animator.SidleRecovery();
				}
			}

			if (Character.Animator.IsSidleMoving) //Finished
				isDamaged = false;
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
		}

		public void OnExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			Instance = null;
			isInteractingWithPlayer = false;
			StopSidle();
		}
	}
}
