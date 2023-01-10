using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Handles sidle behaviour.
	/// </summary>
	public partial class SidleTrigger : Area3D
	{
		/// <summary> Reference to the active sidle trigger. </summary>
		public static SidleTrigger Instance { get; private set; }
		/// <summary> Reference to the active foothold. </summary>
		public static FootholdTrigger CurrentFoothold { get; set; }

		/// <summary> Which way to sidle? </summary>
		[Export]
		private bool isFacingRight = true;
		[Export]
		private LockoutResource lockout;

		/// <summary> Reference to the current rail. </summary>
		private Node3D currentRailing;
		private float velocity;
		private float currentCyclePosition;

		private bool isActive;
		private bool isInteractingWithPlayer;
		/// <summary> Should the player grab a foot hold when taking damage? </summary>
		private bool IsOverFoothold => CurrentFoothold != null;
		private CharacterController Character => CharacterController.instance;
		private InputManager.Controller Controller => InputManager.controller;

		/// <summary> Maximum amount of cycles in a single second. </summary>
		private const float CYCLE_FREQUENCY = 3.4f;
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
				UpdateSidle();
			else if (Character.IsOnGround)
			{
				if (Character.ActionState == CharacterController.ActionStates.Normal)
					StartSidle(); //Allows player to skip section if they know what they're doing
				else if (Character.ActionState == CharacterController.ActionStates.Crouching && Mathf.IsZeroApprox(Character.MoveSpeed))
					Character.ResetActionState();
			}
		}

		private void StartSidle()
		{
			isActive = true;
			Character.StartExternal(this, Character.PathFollower, .2f);
			Character.Animator.ExternalAngle = 0;
			Character.Animator.SnapRotation(0);
			Character.Animator.StartSidle(isFacingRight);
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

		private void UpdateSidleDamage()
		{
		}

		private void UpdateSidleHang()
		{

		}

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			Instance = this;
			isInteractingWithPlayer = true;

			//Apply state
			Character.Skills.IsSpeedBreakEnabled = false;
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
			isActive = false;
			isInteractingWithPlayer = false;
			Character.RemoveLockoutData(lockout);

			if (Character.ExternalController != this) return; //Overridden by a different controller

			Character.MovementAngle = Character.MoveSpeed < 0 ? Character.PathFollower.BackAngle : Character.PathFollower.ForwardAngle;
			Character.MoveSpeed = Mathf.Abs(Character.MoveSpeed);
			Character.ResetMovementState();

			Character.Animator.ResetState(.1f);
			Character.Animator.SnapRotation(Character.PathFollower.ForwardAngle);
		}
	}
}
