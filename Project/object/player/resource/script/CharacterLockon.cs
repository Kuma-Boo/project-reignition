using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay;

/// <summary>
/// Responsible for figuring out which target to lock onto.
/// Also contains the code for bouncing off stuff when using the homing attack.
/// </summary>
public partial class CharacterLockon : Node3D
{
	[Export]
	private Area3D areaTrigger;
	private CharacterController Character => CharacterController.instance;

	#region Homing Attack Reticle
	/// <summary> Active lockon target shown on the HUD. </summary>
	public Node3D Target
	{
		get => target;
		private set
		{
			target = value;
			wasTargetChanged = true;
		}
	}
	private Node3D target;
	/// <summary> Was lockonTarget changed this frame? </summary>
	private bool wasTargetChanged;
	/// <summary> can the current target be attacked? </summary>
	public bool IsTargetAttackable { get; set; }
	private enum TargetState
	{
		Valid,
		NotInList,
		PlayerBusy,
		PlayerIgnored,
		Invisible,
		HitObstacle,
	}
	/// <summary> Targets whose squared distance is within this range will prioritize height instead of distance. </summary>
	private readonly float DistanceFudgeAmount = 1f;
	private readonly Array<Node3D> activeTargets = []; // List of targetable objects

	/// <summary> Enables detection of new lockonTargets. </summary>
	public bool IsMonitoring { get; set; }

	public bool IsHomingAttacking { get; set; }
	public bool IsPerfectHomingAttack { get; private set; }
	private bool monitoringPerfectHomingAttack;
	[Export]
	private AudioStreamPlayer perfectSFX;
	public void EnablePerfectHomingAttack() => monitoringPerfectHomingAttack = true;
	public void DisablePerfectHomingAttack() => monitoringPerfectHomingAttack = false;
	public Vector3 HomingAttackDirection => Target != null ? (Target.GlobalPosition - GlobalPosition).Normalized() : this.Forward();

	public void StartHomingAttack()
	{
		IsHomingAttacking = true;
		IsPerfectHomingAttack = monitoringPerfectHomingAttack;
		Character.AttackState = CharacterController.AttackStates.Weak;

		if (IsPerfectHomingAttack)
		{
			perfectSFX.Play();
			lockonAnimator.Play("perfect-strike");
			Character.AttackState = CharacterController.AttackStates.Strong;
		}
	}

	public void StopHomingAttack()
	{
		Character.AttackState = CharacterController.AttackStates.None;
		IsHomingAttacking = false;
		IsPerfectHomingAttack = false;
		Character.ResetActionState();
		ResetLockonTarget();
	}

	public void UpdateLockonTargets()
	{
		wasTargetChanged = false;
		GlobalRotation = Vector3.Up * Character.PathFollower.ForwardAngle;

		if (IsMonitoring && (!IsBounceLockoutActive || CanInterruptBounce))
		{
			float closestDistance = Mathf.Inf; // Current closest target
			Node3D currentTarget = Target;
			if (currentTarget != null)
				closestDistance = currentTarget.GlobalPosition.Flatten().DistanceSquaredTo(Character.GlobalPosition.Flatten());

			// Check whether to pick a new target
			for (int i = 0; i < activeTargets.Count; i++)
			{
				if (currentTarget == activeTargets[i])
					continue;

				TargetState state = IsTargetValid(activeTargets[i]);
				if (state != TargetState.Valid)
					continue;

				float dst = activeTargets[i].GlobalPosition.Flatten().DistanceSquaredTo(Character.GlobalPosition.Flatten());

				if (currentTarget != null)
				{
					if (dst > closestDistance + DistanceFudgeAmount)
						continue; // Check whether the object is close enough to be considered
					else if (dst > closestDistance - DistanceFudgeAmount && activeTargets[i].GlobalPosition.Y <= currentTarget.GlobalPosition.Y)
						continue; // Within fudge range, decide priority based on height
				}

				// Update data
				currentTarget = activeTargets[i];
				closestDistance = dst;
			}

			if (currentTarget != null && currentTarget != Target) // Target has changed
				Target = currentTarget;
		}

		if (Target != null) // Validate current lockon target
		{
			TargetState targetState = IsTargetValid(Target); // Validate homing attack target

			if ((IsHomingAttacking && targetState == TargetState.NotInList) ||
				(!IsHomingAttacking && targetState != TargetState.Valid))
			{
				Target = null;
			}
			else if (!IsHomingAttacking &&
				Target.GlobalPosition.Flatten().DistanceSquaredTo(Character.GlobalPosition.Flatten()) < DistanceFudgeAmount &&
				Character.IsHoldingDirection(Character.PathFollower.ForwardAngle))
			{
				Target = null;
			}
			else
			{
				// Check Height
				bool isTargetAttackable = IsHomingAttacking ||
					(Target.GlobalPosition.Y <= Character.CenterPosition.Y + (Character.CollisionSize.Y * 2.0f) &&
					Character.ActionState != CharacterController.ActionStates.JumpDash);
				Vector2 screenPos = Character.Camera.ConvertToScreenSpace(Target.GlobalPosition);
				UpdateLockonReticle(screenPos, isTargetAttackable);
			}
		}

		if (Target == null && wasTargetChanged) // Disable UI
			DisableLockonReticle();
	}

	private TargetState IsTargetValid(Node3D t)
	{
		if (!activeTargets.Contains(t)) // Not in target list anymore (target hitbox may have been disabled)
			return TargetState.NotInList;

		if (Character.ActionState == CharacterController.ActionStates.Damaged ||
			!StageSettings.instance.IsLevelIngame) // Character is busy
		{
			return TargetState.PlayerBusy;
		}

		if (!t.IsVisibleInTree() || !Character.Camera.IsOnScreen(t.GlobalPosition)) // Not visible
			return TargetState.Invisible;

		// Raycast for obstacles
		Vector3 castPosition = Character.CollisionPosition;
		if (Character.VerticalSpeed < 0)
			castPosition += Character.UpDirection * Character.VerticalSpeed * PhysicsManager.physicsDelta;
		Vector3 castVector = t.GlobalPosition - castPosition;
		RaycastHit h = this.CastRay(castPosition, castVector, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(castPosition, castVector, Colors.Magenta);

		if (h && h.collidedObject != t)
		{
			if (!h.collidedObject.IsInGroup("level wall") ||
				h.normal.AngleTo(Vector3.Up) > Mathf.Pi * .4f)
			{
				// Hit an obstacle
				return TargetState.HitObstacle;
			}

			if (h.collidedObject.IsInGroup("level wall")) // Cast a new ray from the collision point
			{
				castPosition = h.point + (h.direction.Normalized() * .1f);
				castVector = t.GlobalPosition - castPosition;
				h = this.CastRay(castPosition, castVector, Runtime.Instance.environmentMask);
				DebugManager.DrawRay(castPosition, castVector, Colors.Red);

				if (h && h.collidedObject != t)
					return TargetState.HitObstacle;
			}
		}

		// Check from the floor if nothing was hit
		castPosition = Character.GlobalPosition;
		if (Character.VerticalSpeed < 0)
			castPosition += Character.UpDirection * Character.VerticalSpeed * PhysicsManager.physicsDelta;
		castVector = t.GlobalPosition - (Vector3.Up * Character.CollisionSize.Y) - castPosition;
		h = this.CastRay(castPosition, castVector, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(castPosition, castVector, Colors.Magenta);
		if (h && h.collidedObject != t)
		{
			if (!h.collidedObject.IsInGroup("level wall") &&
				h.normal.AngleTo(Vector3.Up) > Mathf.Pi * .4f)
			{
				return TargetState.HitObstacle;
			}
		}

		float distance = t.GlobalPosition.Flatten().DistanceSquaredTo(Character.GlobalPosition.Flatten());
		if (distance < DistanceFudgeAmount &&
			Character.IsHoldingDirection(Character.PathFollower.ForwardAngle) &&
			!IsBounceLockoutActive)
		{
			return TargetState.PlayerIgnored;
		}

		return TargetState.Valid;
	}

	public void ResetLockonTarget()
	{
		Character.Camera.LockonTarget = null;

		if (Target != null) // Reset Active Target
		{
			Target = null;
			DisableLockonReticle();
		}
	}

	[Export]
	private Node2D lockonReticle;
	[Export]
	private AnimationPlayer lockonAnimator;

	public void DisableLockonReticle() => lockonAnimator.Play("disable");
	public void UpdateLockonReticle(Vector2 screenPosition, bool isTargetAttackable)
	{
		lockonReticle.SetDeferred("position", screenPosition);
		if (!wasTargetChanged && isTargetAttackable == IsTargetAttackable)
			return;

		IsTargetAttackable = isTargetAttackable;
		lockonAnimator.Play("RESET");
		lockonAnimator.Advance(0);
		if (!IsTargetAttackable)
			lockonAnimator.Play("preview");
		else if (Character.Skills.IsSkillEquipped(SkillKey.PerfectHomingAttack))
			lockonAnimator.Play("perfect-enable");
		else
			lockonAnimator.Play("enable");
	}
	#endregion

	#region Bouncing
	[Export]
	public LockoutResource bounceLockoutSettings;
	[Export]
	public float bounceSpeed;
	[Export]
	public float bounceHeight;

	/// <summary> Used to determine whether targeting is enabled or not. </summary>
	private float bounceInterruptTimer;
	/// <summary> Used to determine whether character's lockout is active. </summary>
	public bool IsBounceLockoutActive => Character.ActiveLockoutData == bounceLockoutSettings;
	public bool CanInterruptBounce { get; private set; }

	public void UpdateBounce()
	{
		bounceInterruptTimer = Mathf.MoveToward(bounceInterruptTimer, 0, PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(bounceInterruptTimer))
		{
			CanInterruptBounce = true;
			UpdateLockonTargets();
		}

		Character.MoveSpeed = Mathf.MoveToward(Character.MoveSpeed, 0f, Character.GroundSettings.Friction * PhysicsManager.physicsDelta);
		Character.VerticalSpeed -= Runtime.Gravity * PhysicsManager.physicsDelta;
	}

	public void StartBounce(bool bounceUpward = true) // Bounce the player
	{
		IsHomingAttacking = false;
		CanInterruptBounce = false;
		bounceInterruptTimer = bounceLockoutSettings.length - .5f;

		if (bounceUpward && Target != null) // Snap the player to the target
		{
			Character.MoveSpeed = 0; // Reset speed

			bool applySnapping = false;
			if (!IsBounceLockoutActive)
			{
				if (Target is Area3D)
					applySnapping = areaTrigger.GetOverlappingAreas().Contains(Target as Area3D);
				else if (Target is PhysicsBody3D)
					applySnapping = areaTrigger.GetOverlappingBodies().Contains(Target as PhysicsBody3D);
			}

			// Only snap when target being hit is correct
			if (applySnapping)
				Character.GlobalPosition = Target.GlobalPosition;
		}
		else // Only bounce the player backwards if bounceUpward is false
		{
			Character.MoveSpeed = -bounceSpeed;
		}

		if (IsBounceLockoutActive) return;

		Character.CanJumpDash = true;
		Character.VerticalSpeed = Runtime.CalculateJumpPower(bounceHeight);
		Character.MovementAngle = Character.PathFollower.ForwardAngle;
		Character.AddLockoutData(bounceLockoutSettings);
		Character.ResetActionState();

		Character.Animator.ResetState(0.1f);
		Character.Animator.BounceTrick();
		Character.Effect.PlayActionSFX(Character.Effect.JumpSfx);
	}
	#endregion

	// Targeting areas on the lockon layer
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

	// Allow targeting physics bodies as well...
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
