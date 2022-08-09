using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Responsible for figuring out which target to lock onto.
	/// Also contains the code for bouncing off stuff when using the homing attack.
	/// </summary>
	public class CharacterLockon : Spatial
	{
		private CharacterController Character => CharacterController.instance;

		public Spatial LockonTarget { get; private set; } //Active lockon target
		private readonly Array<Spatial> activeTargets = new Array<Spatial>(); //List of targetable objects

		public bool IsMonitoring { get; set; }

		[Export]
		public float homingAttackSpeed;
		public bool IsHomingAttacking { get; set; }

		public void HomingAttack()
		{
			IsHomingAttacking = true;

			//TODO Check whether it's actually a perfect homing attack
			HeadsUpDisplay.instance.PerfectHomingAttack();
		}

		public void ProcessLockonTargets()
		{
			bool isLockedOn = LockonTarget != null;
			//Validate current lockon target
			if (isLockedOn && IsTargetInvalid(LockonTarget))
				LockonTarget = null;

			//Update homing attack
			if (LockonTarget == null && IsMonitoring)
			{
				float closestDistance = Mathf.Inf;
				//Pick new target
				for (int i = 0; i < activeTargets.Count; i++)
				{
					if (IsTargetInvalid(activeTargets[i]))
						continue;

					float dst = activeTargets[i].GlobalTranslation.RemoveVertical().DistanceSquaredTo(Character.GlobalTranslation.RemoveVertical());
					if (dst > closestDistance)
						continue;

					//Raycast

					closestDistance = dst;
					LockonTarget = activeTargets[i];
				}
			}

			//Disable Homing Attack
			if (LockonTarget == null && isLockedOn)
				HeadsUpDisplay.instance.DisableLockonReticle();
			else if (LockonTarget != null)
			{
				Vector2 screenPos = Character.Camera.ConvertToScreenSpace(LockonTarget.GlobalTranslation);
				HeadsUpDisplay.instance.UpdateLockonReticle(screenPos, !isLockedOn);
			}
		}

		private bool IsTargetInvalid(Spatial t)
		{
			if (!activeTargets.Contains(t) || !t.IsVisibleInTree() || Character.ActionState == CharacterController.ActionStates.Damaged || IsBouncing)
				return true;

			Vector3 castVector = t.GlobalTranslation - Character.GlobalTranslation;
			RaycastHit h = this.CastRay(Character.GlobalTranslation, castVector, Character.environmentMask);
			Debug.DrawRay(Character.GlobalTranslation, castVector, Colors.Magenta);

			if (h && h.collidedObject != t)
				return true;

			return false;
		}

		public void ResetLockonTarget()
		{
			IsHomingAttacking = false;

			if (LockonTarget != null) //Reset Active Target
			{
				LockonTarget = null;
				HeadsUpDisplay.instance.DisableLockonReticle();
			}
		}

		#region Bouncing
		[Export]
		public ControlLockoutResource bounceLockoutSettings;
		[Export]
		public float bouncePower;

		private float bounceTimer;
		public bool IsBouncing => bounceTimer != 0;
		private const float BOUNCE_LOCKOUT_TIME = .15f;

		public void UpdateBounce()
		{
			bounceTimer = Mathf.MoveToward(bounceTimer, 0, PhysicsManager.physicsDelta);

			Character.MoveSpeed = Mathf.MoveToward(Character.MoveSpeed, 0f, Character.moveSettings.friction * PhysicsManager.physicsDelta);
			Character.StrafeSpeed = 0;
			Character.VerticalSpeed -= CharacterController.GRAVITY * PhysicsManager.physicsDelta;
		}

		public void StartBounce() //Bounce the character up and back (So they can target the same enemy again)
		{
			IsHomingAttacking = false;

			bounceTimer = BOUNCE_LOCKOUT_TIME;
			ResetLockonTarget();

			Character.CanJumpDash = true;
			Character.MoveSpeed = -bouncePower;
			Character.VerticalSpeed = bouncePower;
			Character.SetControlLockout(bounceLockoutSettings);
			Character.ResetActionState();
		}
		#endregion


		//Targeting areas on the lockon layer
		public void OnTargetTriggerEnter(Area area)
		{
			if (!activeTargets.Contains(area))
				activeTargets.Add(area);
		}

		public void OnTargetTriggerExit(Area area)
		{
			if (activeTargets.Contains(area))
				activeTargets.Remove(area);
		}

		//Allow targeting physics bodies as well...
		public void OnTargetBodyEnter(PhysicsBody body)
		{
			if (!activeTargets.Contains(body))
				activeTargets.Add(body);
		}

		public void OnTargetBodyExit(PhysicsBody body)
		{
			if (activeTargets.Contains(body))
				activeTargets.Remove(body);
		}
	}
}
