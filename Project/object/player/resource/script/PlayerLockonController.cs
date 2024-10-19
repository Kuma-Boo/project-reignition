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
		LowPriority,
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

	private bool isMonitoring;
	/// <summary> Should the controller check for new lockonTargets? </summary>
	public bool IsMonitoring
	{
		get => isMonitoring;
		set
		{
			isMonitoring = value;
			if (!isMonitoring)
				Player.Lockon.ResetLockonTarget();
		}
	}

	public bool IsReticleVisible
	{
		get => lockonReticle.Visible;
		set => lockonReticle.Visible = value;
	}

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
		Node3D activeTarget = Target;
		TargetState activeState = IsTargetValid(Target);
		float closestDistance = Mathf.Inf;
		if (activeTarget != null)
		{
			closestDistance = activeTarget.GlobalPosition.Flatten().DistanceSquaredTo(Player.GlobalPosition.Flatten());

			if (Player.IsHomingAttacking) // Don't allow lockons to switch during a homing attack
				return false;
		}

		// Check whether to pick a new target
		for (int i = 0; i < activeTargets.Count; i++)
		{
			if (activeTarget == activeTargets[i])
				continue;

			TargetState state = IsTargetValid(activeTargets[i]);
			if (state != TargetState.Valid && state != TargetState.LowPriority)
				continue;

			float dst = activeTargets[i].GlobalPosition.Flatten().DistanceSquaredTo(Player.GlobalPosition.Flatten());
			if (activeTarget != null)
			{
				bool prioritizeActiveTarget = activeState == TargetState.Valid || state == TargetState.LowPriority;
				// Ignore low-priority targets that are further from the current target
				if (dst > closestDistance + DistanceFudgeAmount && prioritizeActiveTarget)
					continue;

				// Ignore lower targets when within fudge range
				if (dst < closestDistance + DistanceFudgeAmount &&
					dst > closestDistance - DistanceFudgeAmount &&
					activeTargets[i].GlobalPosition.Y <= activeTarget.GlobalPosition.Y &&
					prioritizeActiveTarget)
				{
					continue;
				}
			}

			// Update data
			activeTarget = activeTargets[i];
			activeState = state;
			closestDistance = dst;
		}

		if (activeTarget != null && activeTarget != Target) // Target has changed
		{
			Target = activeTarget;
			return true;
		}

		return false;
	}

	private void ValidateTarget(bool wasTargetChanged)
	{
		if (Target == null)
			return;

		TargetState targetState = IsTargetValid(Target); // Validate homing attack target
		if ((Player.IsHomingAttacking && targetState == TargetState.NotInList) ||
			(!Player.IsHomingAttacking && targetState != TargetState.Valid && targetState != TargetState.LowPriority))
		{
			ResetLockonTarget();
			return;
		}

		if (IsIgnoringTarget(Target))
		{
			ResetLockonTarget();
			Player.Camera.LockonTarget = null;
			return;
		}

		Vector2 screenPos = Player.Camera.ConvertToScreenSpace(Target.GlobalPosition);
		UpdateLockonReticle(screenPos, Player.IsHomingAttacking || targetState == TargetState.Valid, wasTargetChanged);
	}

	private TargetState IsTargetValid(Node3D target)
	{
		if (target == null || !activeTargets.Contains(target)) // Not in target list anymore (target hitbox may have been disabled)
			return TargetState.NotInList;

		if (Player.IsKnockback || !StageSettings.Instance.IsLevelIngame) // Character is busy
			return TargetState.PlayerBusy;

		if (!target.IsVisibleInTree() || !Player.Camera.IsOnScreen(target.GlobalPosition)) // Not visible
			return TargetState.Invisible;

		if (HitObstacle(target))
			return TargetState.HitObstacle;

		if (IsIgnoringTarget(target))
			return TargetState.PlayerIgnored;

		// Ignore height check if player is already homing attacking the target
		if (IsTargetAttackable && target == Target && Player.IsHomingAttacking)
			return TargetState.Valid;

		// Check Height
		bool isTargetAttackable = target.GlobalPosition.Y <= Player.CenterPosition.Y + (Player.CollisionSize.Y * 2.0f);
		if (Player.IsBouncing && !IsMonitoring)
			isTargetAttackable = false;

		return isTargetAttackable ? TargetState.Valid : TargetState.LowPriority;
	}

	private bool IsIgnoringTarget(Node3D target)
	{
		if (Target == target && Player.IsHomingAttacking)
			return false;

		float inputStrength = Player.Controller.GetInputStrength();
		if (Mathf.IsZeroApprox(inputStrength))
			return false;

		float distance = target.GlobalPosition.Flatten().DistanceSquaredTo(Player.GlobalPosition.Flatten());
		bool holdingForward = Player.Controller.IsHoldingDirection(Player.Controller.GetTargetInputAngle(), Player.PathFollower.ForwardAngle);
		return distance < DistanceFudgeAmount && holdingForward;
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
			if (!h.collidedObject.IsInGroup(LevelWallGroup)) // Hit an obstacle
				return true;

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

		return false;
	}

	public void ResetLockonTarget()
	{
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
