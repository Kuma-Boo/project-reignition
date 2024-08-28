using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay;

/// <summary>
/// Responsible for figuring out which target to lock onto.
/// Also contains the code for bouncing off stuff when using the homing attack.
/// </summary>
public partial class PlayerLockonController : Node3D
{
	[Export]
	private Area3D areaTrigger;

	private PlayerController Player;
	public void Initialize(PlayerController player) => Player = player;

	/// <summary> Active lockon target shown on the HUD. </summary>
	public Node3D Target {get; private set;}
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
	private readonly string LevelWallGroup = "level wall";
	private readonly Array<Node3D> activeTargets = []; // List of targetable objects

	/// <summary> Should the controller check for new lockonTargets? </summary>
	public bool IsMonitoring { get; set; }
	public bool IsHomingAttacking { get; set; }

	public bool IsMonitoringPerfectHomingAttack { get; private set; }
	public void EnablePerfectHomingAttack() => IsMonitoringPerfectHomingAttack = true;
	public void DisablePerfectHomingAttack() => IsMonitoringPerfectHomingAttack = false;
	public void PlayPerfectStrike() => lockonAnimator.Play("perfect-strike");
	public Vector3 HomingAttackDirection => Target != null ? (Target.GlobalPosition - GlobalPosition).Normalized() : this.Forward();

	public void ProcessPhysics()
	{
		bool wasTargetChanged = false;
		GlobalRotation = Vector3.Up * Player.PathFollower.ForwardAngle;

		if (IsMonitoring)
			wasTargetChanged = ProcessMonitoring();

		ValidateTarget(wasTargetChanged);
	}

	private bool ProcessMonitoring()
	{
		Node3D currentTarget = Target;
		float closestDistance = Mathf.Inf;
		if (currentTarget != null)
			closestDistance = currentTarget.GlobalPosition.Flatten().DistanceSquaredTo(Player.GlobalPosition.Flatten());

		// Check whether to pick a new target
		for (int i = 0; i < activeTargets.Count; i++)
		{
			if (currentTarget == activeTargets[i])
				continue;

			TargetState state = IsTargetValid(activeTargets[i]);
			if (state != TargetState.Valid)
				continue;

			float dst = activeTargets[i].GlobalPosition.Flatten().DistanceSquaredTo(Player.GlobalPosition.Flatten());

			if (currentTarget != null)
			{
				// Ignore targets that are further from the current target
				if (dst > closestDistance + DistanceFudgeAmount)
					continue;
				
				// Within fudge range, decide priority based on height
				if (dst > closestDistance - DistanceFudgeAmount &&
					activeTargets[i].GlobalPosition.Y <= currentTarget.GlobalPosition.Y)
				{
					continue;
				}
			}

			// Update data
			currentTarget = activeTargets[i];
			closestDistance = dst;
		}

		if (currentTarget != null && currentTarget != Target) // Target has changed
		{
			Target = currentTarget;
			return true;
		}

		return false;
	}

	private void ValidateTarget(bool wasTargetChanged)
	{
		if(Target == null)
			return;

		TargetState targetState = IsTargetValid(Target); // Validate homing attack target
		if ((IsHomingAttacking && targetState == TargetState.NotInList) ||
			(!IsHomingAttacking && targetState != TargetState.Valid))
		{
			ResetLockonTarget();
			return;
		}

		if (!IsHomingAttacking &&
			Target.GlobalPosition.Flatten().DistanceSquaredTo(Player.GlobalPosition.Flatten()) < DistanceFudgeAmount &&
			Player.Controller.IsHoldingDirection(Player.Controller.GetTargetMovementAngle(), Player.PathFollower.ForwardAngle))
		{
			ResetLockonTarget();
			return;
		}

		// REFACTOR TODO
		// Check Height
		bool isTargetAttackable = IsHomingAttacking ||
			(Target.GlobalPosition.Y <= Player.CenterPosition.Y + (Player.CollisionSize.Y * 2.0f));
		// && Player.State.ActionState != CharacterController.ActionStates.JumpDash);
		/*
		if (IsBounceLockoutActive && !CanInterruptBounce)
			isTargetAttackable = false;
		*/

		Vector2 screenPos = Vector2.One * .5f; // REFACTOR TODO Player.Camera.ConvertToScreenSpace(Target.GlobalPosition);
		UpdateLockonReticle(screenPos, isTargetAttackable, wasTargetChanged);
	}

	private TargetState IsTargetValid(Node3D target)
	{
		if (!activeTargets.Contains(target)) // Not in target list anymore (target hitbox may have been disabled)
			return TargetState.NotInList;

		/*
		REFACTOR TODO
		if (Player.ActionState == CharacterController.ActionStates.Damaged ||
			!StageSettings.instance.IsLevelIngame) // Character is busy
		{
			return TargetState.PlayerBusy;
		}

		if (!t.IsVisibleInTree() || !Player.Camera.IsOnScreen(t.GlobalPosition)) // Not visible
			return TargetState.Invisible;
		*/

		if(HitObstacle(target))
			return TargetState.HitObstacle;

		float distance = target.GlobalPosition.Flatten().DistanceSquaredTo(Player.GlobalPosition.Flatten());
		if (distance < DistanceFudgeAmount &&
			Player.Controller.IsHoldingDirection(Player.Controller.GetTargetMovementAngle(), Player.PathFollower.ForwardAngle))
		{
			return TargetState.PlayerIgnored;
		}

		return TargetState.Valid;
	}

	private bool HitObstacle(Node3D target)
	{
		// Raycast for obstacles
		Vector3 castPosition = Player.CollisionPosition;
		if (Player.VerticalSpeed < 0)
			castPosition += Player.UpDirection * Player.VerticalSpeed * PhysicsManager.physicsDelta;
		Vector3 castVector = target.GlobalPosition - castPosition;

		RaycastHit h = this.CastRay(castPosition, castVector, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(castPosition, castVector, Colors.Magenta);
		if (h && h.collidedObject != target)
		{
			if (!h.collidedObject.IsInGroup(LevelWallGroup) ||
				h.normal.AngleTo(Vector3.Up) > Mathf.Pi * .4f)
			{
				// Hit an obstacle
				return true;
			}

			if (h.collidedObject.IsInGroup(LevelWallGroup)) // Cast a new ray from the collision point
			{
				castPosition = h.point + (h.direction.Normalized() * .1f);
				castVector = target.GlobalPosition - castPosition;
				h = this.CastRay(castPosition, castVector, Runtime.Instance.environmentMask);
				DebugManager.DrawRay(castPosition, castVector, Colors.Red);

				if (h && h.collidedObject != target)
					return true;
			}
		}

		/* REFACTOR TODO
			Remove this check and have the player simply transition
		 	bounce state when colliding with something instead.
		*/
		// Check from the player's feet if nothing was hit
		Vector3 castOffset = Vector3.Up * Player.CollisionSize.Y * .5f;
		castPosition = Player.GlobalPosition + castOffset;
		if (Player.VerticalSpeed < 0)
			castPosition += Player.UpDirection * Player.VerticalSpeed * PhysicsManager.physicsDelta;
		castVector = target.GlobalPosition - castOffset - castPosition;
		h = this.CastRay(castPosition, castVector, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(castPosition, castVector, Colors.Magenta);
		if (h && h.collidedObject != target &&
			!h.collidedObject.IsInGroup(LevelWallGroup) &&
			h.normal.AngleTo(Vector3.Up) > Mathf.Pi * .4f)
		{
			return true;
		}

		return false;
	}

	public void ResetLockonTarget()
	{
		/* REFACTOR TODO
		Player.Camera.LockonTarget = null;
		*/

		if (Target == null)
			return;

		// Reset Active Target
		Target = null;
		DisableLockonReticle();
	}

	[Export]
	private Node2D lockonReticle;
	[Export]
	private AnimationPlayer lockonAnimator;
	public void DisableLockonReticle() => lockonAnimator.Play("disable");
	public void UpdateLockonReticle(Vector2 screenPosition, bool isTargetAttackable, bool wasTargetChanged)
	{
		lockonReticle.SetDeferred("position", screenPosition);
		if (!wasTargetChanged && isTargetAttackable == IsTargetAttackable)
			return;

		IsTargetAttackable = isTargetAttackable;
		lockonAnimator.Play("RESET");
		lockonAnimator.Advance(0);
		if (!IsTargetAttackable)
			lockonAnimator.Play("preview");
		else if (Player.Skills.IsSkillEquipped(SkillKey.PerfectHomingAttack))
			lockonAnimator.Play("perfect-enable");
		else
			lockonAnimator.Play("enable");
	}

	/* REFACTOR TODO
	Move to separate movement state.
	[Export]
	public LockoutResource bounceLockoutSettings;
	[Export]
	public float bounceSpeed;
	[Export]
	public float bounceHeight;

	/// <summary> Used to determine whether targeting is enabled or not. </summary>
	private float bounceInterruptTimer;
	/// <summary> Used to determine whether character's lockout is active. </summary>
	public bool IsBounceLockoutActive => Player.ActiveLockoutData == bounceLockoutSettings;
	public bool CanInterruptBounce { get; private set; }

	public void UpdateBounce()
	{
		bounceInterruptTimer = Mathf.MoveToward(bounceInterruptTimer, 0, PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(bounceInterruptTimer))
		{
			CanInterruptBounce = true;
			UpdateLockonTargets();
		}

		Player.MoveSpeed = Mathf.MoveToward(Player.MoveSpeed, 0f, Player.GroundSettings.Friction * PhysicsManager.physicsDelta);
		Player.VerticalSpeed -= Runtime.Gravity * PhysicsManager.physicsDelta;
	}

	public void StartBounce(bool bounceUpward = true) // Bounce the player
	{
		IsHomingAttacking = false;
		CanInterruptBounce = false;
		bounceInterruptTimer = bounceLockoutSettings.length - .5f;

		if (bounceUpward && Target != null) // Snap the player to the target
		{
			Player.MoveSpeed = 0; // Reset speed

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
				Player.GlobalPosition = Target.GlobalPosition;
		}
		else // Only bounce the player backwards if bounceUpward is false
		{
			Player.MoveSpeed = -bounceSpeed;
		}

		if (IsBounceLockoutActive) return;

		Player.CanJumpDash = true;
		Player.VerticalSpeed = Runtime.CalculateJumpPower(bounceHeight);
		Player.MovementAngle = Player.PathFollower.ForwardAngle;
		Player.AddLockoutData(bounceLockoutSettings);
		Player.ResetActionState();

		Player.Animator.ResetState(0.1f);
		Player.Animator.BounceTrick();
		Player.Effect.PlayActionSFX(Player.Effect.JumpSfx);
	}
	*/

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
