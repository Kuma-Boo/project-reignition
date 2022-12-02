using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Responsible for figuring out which target to lock onto.
	/// Also contains the code for bouncing off stuff when using the homing attack.
	/// </summary>
	public partial class CharacterLockon : Node3D
	{
		private CharacterController Character => CharacterController.instance;

		public Node3D LockonTarget { get; private set; } //Active lockon target
		private readonly Array<Node3D> activeTargets = new Array<Node3D>(); //List of targetable objects

		public bool IsMonitoring { get; set; }

		[Export]
		public float homingAttackSpeed;
		public bool IsHomingAttacking { get; set; }
		public bool IsPerfectHomingAttack { get; private set; }
		private bool monitoringPerfectHomingAttack;
		public void EnablePerfectHomingAttack() => monitoringPerfectHomingAttack = true;
		public void DisablePerfectHomingAttack() => monitoringPerfectHomingAttack = false;
		public Vector3 HomingAttackDirection => LockonTarget != null ? (LockonTarget.GlobalPosition - GlobalPosition).Normalized() : this.Back();

		public void HomingAttack()
		{
			IsHomingAttacking = true;
			IsPerfectHomingAttack = monitoringPerfectHomingAttack;
			if (IsPerfectHomingAttack)
				StageSettings.instance.AddBonus(StageSettings.BonusType.PerfectHomingAttack);
		}

		public void UpdateLockonTargets()
		{
			GlobalRotation = Vector3.Up * Character.PathFollower.ForwardAngle;
			bool isTargetChanged = false;

			//Update homing attack
			if (IsMonitoring)
			{
				int currentTarget = -1; //Index of the current target
				float closestDistance = Mathf.Inf; //Current closest target
				if (LockonTarget != null) //Current lockon target starts as the closest target
					closestDistance = LockonTarget.GlobalPosition.Flatten().DistanceSquaredTo(Character.GlobalPosition.Flatten());

				//Check whether to pick a new target
				for (int i = 0; i < activeTargets.Count; i++)
				{
					if (IsTargetValid(activeTargets[i]) != TargetState.Valid)
						continue;

					float dst = activeTargets[i].GlobalPosition.Flatten().DistanceSquaredTo(Character.GlobalPosition.Flatten());
					if (dst > closestDistance)
						continue;

					//Update data
					closestDistance = dst;
					currentTarget = i;
				}

				if (currentTarget != -1 && activeTargets[currentTarget] != LockonTarget) //Target has changed
				{
					LockonTarget = activeTargets[currentTarget];
					isTargetChanged = true;
				}
				else if (LockonTarget != null && IsTargetValid(LockonTarget) != TargetState.Valid) //Validate current lockon target
				{
					LockonTarget = null;
					isTargetChanged = true;
				}
			}

			//Disable Homing Attack
			if (LockonTarget == null && isTargetChanged)
				DisableLockonReticle();
			else if (LockonTarget != null)
			{
				Vector2 screenPos = Character.Camera.ConvertToScreenSpace(LockonTarget.GlobalPosition);
				UpdateLockonReticle(screenPos, isTargetChanged);
			}
		}

		private enum TargetState
		{
			Valid,
			NotInList,
			PlayerBusy,
			Invisible,
			HitObstacle,
		}

		private TargetState IsTargetValid(Node3D t)
		{
			if (!activeTargets.Contains(t)) //Not in target list anymore (target hitbox may have been disabled)
				return TargetState.NotInList;

			if (Character.ActionState == CharacterController.ActionStates.Damaged || IsBouncing) //Character is busy
				return TargetState.PlayerBusy;

			if (!t.IsVisibleInTree() || !Character.Camera.IsOnScreen(t.GlobalPosition)) //Not visible
				return TargetState.Invisible;

			//Raycast for obstacles
			Vector3 castPosition = Character.GlobalPosition;
			if (Character.VerticalSpd < 0)
				castPosition += Character.UpDirection * Character.VerticalSpd * PhysicsManager.physicsDelta;
			Vector3 castVector = t.GlobalPosition - castPosition;
			RaycastHit h = this.CastRay(castPosition, castVector, RuntimeConstants.Instance.environmentMask);
			Debug.DrawRay(castPosition, castVector, Colors.Magenta);

			if (h && h.collidedObject != t)
				return TargetState.HitObstacle;

			return TargetState.Valid;
		}

		public void ResetLockonTarget()
		{
			IsHomingAttacking = false;
			IsPerfectHomingAttack = false;

			if (LockonTarget != null) //Reset Active Target
			{
				LockonTarget = null;
				DisableLockonReticle();
			}
		}

		#region Bouncing
		[Export]
		public LockoutResource bounceLockoutSettings;
		[Export]
		public float bounceSpeed;
		[Export]
		public float bouncePower;

		private float bounceTimer;
		public bool IsBouncing => bounceTimer != 0;
		private const float BOUNCE_LOCKOUT_TIME = .15f;

		public void UpdateBounce()
		{
			bounceTimer = Mathf.MoveToward(bounceTimer, 0, PhysicsManager.physicsDelta);

			Character.MoveSpeed = Mathf.MoveToward(Character.MoveSpeed, 0f, Character.GroundSettings.friction * PhysicsManager.physicsDelta);
			Character.VerticalSpd -= RuntimeConstants.GRAVITY * PhysicsManager.physicsDelta;
		}

		public void StartBounce() //Bounce the character up and back (So they can target the same enemy again)
		{
			IsHomingAttacking = false;

			bounceTimer = BOUNCE_LOCKOUT_TIME;
			ResetLockonTarget();

			Character.CanJumpDash = true;
			Character.MoveSpeed = bounceSpeed;
			Character.VerticalSpd = bouncePower;
			Character.AddLockoutData(bounceLockoutSettings);
			Character.ResetActionState();
		}
		#endregion

		#region Homing Attack Reticle
		[Export]
		private Node2D lockonReticle;
		[Export]
		private AnimationPlayer lockonAnimator;

		public void DisableLockonReticle() => lockonAnimator.Play("disable");
		public void UpdateLockonReticle(Vector2 screenPosition, bool newTarget)
		{
			lockonReticle.SetDeferred("position", screenPosition);
			if (newTarget)
			{
				lockonAnimator.Play("RESET");
				lockonAnimator.Advance(0);
				lockonAnimator.Play("enable");
			}
		}

		public void PerfectHomingAttack()
		{
			//TODO Play animation
		}
		#endregion


		//Targeting areas on the lockon layer
		public void OnTargetTriggerEnter(Area3D area)
		{
			if (!activeTargets.Contains(area))
				activeTargets.Add(area);
		}

		public void OnTargetTriggerExit(Area3D area)
		{
			if (activeTargets.Contains(area))
				activeTargets.Remove(area);
		}

		//Allow targeting physics bodies as well...
		public void OnTargetBodyEnter(PhysicsBody3D body)
		{
			if (!activeTargets.Contains(body))
				activeTargets.Add(body);
		}

		public void OnTargetBodyExit(PhysicsBody3D body)
		{
			if (activeTargets.Contains(body))
				activeTargets.Remove(body);
		}
	}
}
