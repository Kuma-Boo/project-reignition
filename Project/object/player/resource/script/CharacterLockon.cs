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
		public bool IsPerfectHomingAttack { get; private set; }
		private bool monitoringPerfectHomingAttack;
		public void EnablePerfectHomingAttack() => monitoringPerfectHomingAttack = true;
		public void DisablePerfectHomingAttack() => monitoringPerfectHomingAttack = false;
		public Vector3 HomingAttackDirection => LockonTarget != null ? (LockonTarget.GlobalTranslation - GlobalTranslation).Normalized() : this.Forward();

		public override void _Ready()
		{
			_lockonReticle = GetNode<Node2D>(lockonReticle);
			_lockonAnimator = GetNode<AnimationPlayer>(lockonAnimator);
		}

		public void HomingAttack()
		{
			IsHomingAttacking = true;
			IsPerfectHomingAttack = monitoringPerfectHomingAttack;
			if(IsPerfectHomingAttack)
				StageSettings.instance.AddBonus(StageSettings.BonusType.PerfectHomingAttack);
		}

		public void ProcessLockonTargets()
		{
			bool isLockedOn = LockonTarget != null;
			//Validate current lockon target
			if (isLockedOn && IsTargetInvalid(LockonTarget))
				LockonTarget = null;

			//Update homing attack
			if (IsMonitoring)
			{
				float closestDistance = Mathf.Inf;
				if(LockonTarget != null)
					closestDistance = LockonTarget.GlobalTranslation.Flatten().DistanceSquaredTo(Character.GlobalTranslation.Flatten());

				//Pick new target
				for (int i = 0; i < activeTargets.Count; i++)
				{
					if (IsTargetInvalid(activeTargets[i]))
						continue;

					float dst = activeTargets[i].GlobalTranslation.Flatten().DistanceSquaredTo(Character.GlobalTranslation.Flatten());
					if (dst > closestDistance)
						continue;

					//Raycast
					closestDistance = dst;
					LockonTarget = activeTargets[i];
				}
			}

			//Disable Homing Attack
			if (LockonTarget == null && isLockedOn)
				DisableLockonReticle();
			else if (LockonTarget != null)
			{
				Vector2 screenPos = Character.Camera.ConvertToScreenSpace(LockonTarget.GlobalTranslation);
				UpdateLockonReticle(screenPos, !isLockedOn);
			}
		}

		private bool IsTargetInvalid(Spatial t)
		{
			if (!t.IsVisibleInTree() || Character.ActionState == CharacterController.ActionStates.Damaged || IsBouncing)
				return true;

			if (!Character.Camera.IsOnScreen(t.GlobalTranslation))
				return true;

			if (!activeTargets.Contains(t) && t != LockonTarget)
				return true;

			Vector3 castPosition = Character.GlobalTranslation;
			if (Character.VerticalSpeed < 0)
				castPosition += Character.worldDirection * Character.VerticalSpeed * PhysicsManager.physicsDelta;
			Vector3 castVector = t.GlobalTranslation - castPosition;
			RaycastHit h = this.CastRay(castPosition, castVector, Character.environmentMask);
			Debug.DrawRay(castPosition, castVector, Colors.Magenta);

			if (h && h.collidedObject != t)
				return true;

			return false;
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
		public ControlLockoutResource bounceLockoutSettings;
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
			Character.MoveSpeed = bounceSpeed;
			Character.VerticalSpeed = bouncePower;
			Character.StartControlLockout(bounceLockoutSettings);
			Character.ResetActionState();
		}
		#endregion

		#region Homing Attack Reticle
		[Export]
		public NodePath lockonReticle;
		private Node2D _lockonReticle;
		[Export]
		public NodePath lockonAnimator;
		private AnimationPlayer _lockonAnimator;

		public void DisableLockonReticle() => _lockonAnimator.Play("disable");
		public void UpdateLockonReticle(Vector2 screenPosition, bool newTarget)
		{
			_lockonReticle.SetDeferred("position", screenPosition);
			if (newTarget)
				_lockonAnimator.Play("enable");
		}

		public void PerfectHomingAttack()
		{
			//TODO Play animation
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
