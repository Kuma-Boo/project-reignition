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
	private PlayerController Player;
	public void Initialize(PlayerController player) => Player = player;

	[Export]
	private Area3D areaTrigger;
	public Array<Area3D> GetOverlappingAreas() => areaTrigger.GetOverlappingAreas();
	public Array<Node3D> GetOverlappingBodies() => areaTrigger.GetOverlappingBodies();

	/// <summary> Active lockon target shown on the HUD. </summary>
	public Node3D Target { get; private set; }
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

			/* REFACTOR TODO Does this need to be here?
			if (currentTarget != null)
			{
			*/
			// Ignore targets that are further from the current target
			if (dst > closestDistance + DistanceFudgeAmount)
				continue;

			// Within fudge range, decide priority based on height
			if (dst > closestDistance - DistanceFudgeAmount &&
				activeTargets[i].GlobalPosition.Y <= currentTarget.GlobalPosition.Y)
			{
				continue;
			}
			//}

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
		if (Target == null)
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
			Player.Controller.IsHoldingDirection(Player.Controller.GetTargetMovementAngle(true), Player.PathFollower.ForwardAngle))
		{
			ResetLockonTarget();
			return;
		}

		// REFACTOR TODO
		// Check Height
		bool isTargetAttackable = IsHomingAttacking ||
			(Target.GlobalPosition.Y <= Player.CenterPosition.Y + (Player.CollisionSize.Y * 2.0f));
		// && Player.ActionState != PlayerController.ActionStates.JumpDash);
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
		if (Player.ActionState == PlayerController.ActionStates.Damaged ||
			!StageSettings.instance.IsLevelIngame) // Character is busy
		{
			return TargetState.PlayerBusy;
		}
		*/

		if (!target.IsVisibleInTree() || !Player.Camera.IsOnScreen(target.GlobalPosition)) // Not visible
			return TargetState.Invisible;

		if (HitObstacle(target))
			return TargetState.HitObstacle;

		float distance = target.GlobalPosition.Flatten().DistanceSquaredTo(Player.GlobalPosition.Flatten());
		if (distance < DistanceFudgeAmount &&
			Player.Controller.IsHoldingDirection(Player.Controller.GetTargetMovementAngle(true), Player.PathFollower.ForwardAngle))
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
		Player.Camera.LockonTarget = null;

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
		else if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.PerfectHomingAttack))
			lockonAnimator.Play("perfect-enable");
		else
			lockonAnimator.Play("enable");
	}

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
