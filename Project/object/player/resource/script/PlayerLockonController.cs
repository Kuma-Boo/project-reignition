using System.Collections.Generic;
using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary>
/// Responsible for figuring out which target to lock onto.
/// Also contains the code for bouncing off stuff when using the homing attack.
/// </summary>
public partial class PlayerLockonController : Area3D
{
	private PlayerController Player;
	public void Initialize(PlayerController player)
	{
		Player = player;
		IsMonitoring = SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.GroundedHomingAttack);

		Player.Skills.TimeBreakStarted += UpdateLockonAnimationSpeedScale;
		Player.Skills.TimeBreakStopped += UpdateLockonAnimationSpeedScale;
	}

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
	private readonly float PriorityDistance = 1f;
	/// <summary> How close a target needs to be to auto-lockon after bouncing. </summary>
	private readonly float AutotargetDistanceAmount = 4f;
	/// <summary> How far ahead the player must be to ignore the active lockon target. </summary>
	private readonly float IgnoreTargetDistance = 2f;
	private readonly string LevelWallGroup = "level wall";
	private readonly string IgnoreLockonCastGroup = "ignore lockon cast";
	/// <summary> List of all possible targets. </summary>
	private readonly List<Node3D> potentialTargets = [];

	private bool isMonitoring;
	/// <summary> Should the controller check for new lockonTargets? </summary>
	public new bool IsMonitoring
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
	private bool isSideScrolling;
	public bool IsMonitoringPerfectHomingAttack { get; private set; }
	public void EnablePerfectHomingAttack() => IsMonitoringPerfectHomingAttack = true;
	public void DisablePerfectHomingAttack() => IsMonitoringPerfectHomingAttack = false;
	public void PlayPerfectStrike() => lockonAnimator.Play("perfect-strike");
	public Vector3 HomingAttackDirection => Target != null ? (Target.GlobalPosition - GlobalPosition).Normalized() : this.Forward();

	public override void _Ready() => IsReticleVisible = !DebugManager.Instance.DisableReticle;

	public void ProcessPhysics()
	{
		bool wasTargetChanged = false;

		if (SaveManager.ActiveSkillRing.IsAutorunActive)
			GlobalRotation = Vector3.Up * Player.PathFollower.ForwardAngle;

		isSideScrolling = Player.IsLockoutOverridingMovementAngle &&
			Player.ActiveLockoutData.recenterPlayer &&
			Mathf.Abs(Player.PathFollower.Forward().Dot(Player.Camera.Camera.Forward())) < 0.6f;

		if (IsMonitoring)
			wasTargetChanged = ProcessMonitoring();

		ValidateTarget(wasTargetChanged);
		ValidateCameraLockonTarget();
	}

	private void UpdateLockonAnimationSpeedScale() => lockonAnimator.SpeedScale = 1f / (float)Engine.TimeScale;

	private void ValidateCameraLockonTarget()
	{
		if (Player.Camera.LockonTarget == null)
			return;

		if (Player.IsOnGround)
		{
			Player.Camera.SetLockonTarget(null);
			return;
		}

		TargetState targetState = IsTargetValid(Player.Camera.LockonTarget);
		if (targetState != TargetState.NotInList && targetState != TargetState.Invisible)
			return;

		Player.Camera.SetLockonTarget(null);
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
		for (int i = 0; i < potentialTargets.Count; i++)
		{
			if (activeTarget == potentialTargets[i])
				continue;

			TargetState potentialState = IsTargetValid(potentialTargets[i]);
			if (potentialState != TargetState.Valid && potentialState != TargetState.LowPriority)
				continue;

			float potentialDistance = potentialTargets[i].GlobalPosition.DistanceSquaredTo(Player.GlobalPosition);
			if (activeTarget != null)
			{
				bool prioritizeActiveTarget = activeState == TargetState.Valid || potentialState == TargetState.LowPriority;
				// Ignore low-priority targets that are further from the current target
				if (potentialDistance > closestDistance + PriorityDistance && prioritizeActiveTarget)
					continue;

				// Ignore lower targets when within priority distance
				if (activeState == TargetState.Valid &&
					Mathf.Abs(closestDistance - potentialDistance) < PriorityDistance &&
					(potentialTargets[i].GlobalPosition.Y <= activeTarget.GlobalPosition.Y ||
					potentialState == TargetState.LowPriority))
				{
					continue;
				}
			}

			// Update data
			activeTarget = potentialTargets[i];
			activeState = potentialState;
			closestDistance = potentialDistance;
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

		if (targetState == TargetState.PlayerIgnored)
		{
			ResetLockonTarget();
			Player.Camera.SetLockonTarget(null);
			return;
		}

		Vector2 screenPos = Player.Camera.ConvertToScreenSpace(Target.GlobalPosition);
		screenPos.X = Mathf.Clamp(screenPos.X, LockonReticleRadius, GetTree().Root.GetViewport().GetVisibleRect().Size.X - LockonReticleRadius);
		screenPos.Y = Mathf.Clamp(screenPos.Y, LockonReticleRadius, GetTree().Root.GetViewport().GetVisibleRect().Size.Y - LockonReticleRadius);
		UpdateLockonReticle(screenPos, Player.IsHomingAttacking || targetState == TargetState.Valid, wasTargetChanged);
	}

	private TargetState IsTargetValid(Node3D target)
	{
		if (target == null || !potentialTargets.Contains(target)) // Not in target list anymore (target hitbox may have been disabled)
			return TargetState.NotInList;

		if (target is Area3D && !(target as Area3D).Monitorable) // Treat deactivated hitboxes as not in the list
			return TargetState.NotInList;

		if (Player.IsKnockback || !StageSettings.Instance.IsLevelIngame) // Character is busy
			return TargetState.PlayerBusy;

		if (HitObstacle(target))
			return TargetState.HitObstacle;

		if (IsIgnoringTarget(target))
			return TargetState.PlayerIgnored;

		// Ignore height check if player is already homing attacking the target (or is moving upwards)
		if (IsTargetAttackable && target == Target && (Player.IsHomingAttacking || Player.VerticalSpeed > 0))
			return TargetState.Valid;

		if (!IsTargetVisible(target))
			return TargetState.Invisible;

		// Check Height
		float maxTargetHeight = Player.CenterPosition.Y + (Player.CollisionSize.Y * 2.0f);
		if (Player.IsOnGround) // Capture more lockon targets when on the ground 
			maxTargetHeight += Player.Stats.JumpHeight;
		bool isTargetAttackable = target.GlobalPosition.Y <= maxTargetHeight;
		if (Player.IsBouncing && !Player.IsBounceInteruptable)
		{
			isTargetAttackable = false;

			if (Target == null)
			{
				// Allow camera to lockon to close objects
				float targetDistance = Player.PathFollower.GetProgress(target.GlobalPosition) - Player.PathFollower.Progress;
				if (targetDistance <= AutotargetDistanceAmount)
					Player.Camera.SetLockonTarget(target);
			}
		}
		else if (Player.IsOnGround && isTargetAttackable)
			isTargetAttackable = SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.GroundedHomingAttack);

		return isTargetAttackable ? TargetState.Valid : TargetState.LowPriority;
	}

	/// <summary> Determines whether an object should be prematurely locked onto (e.g. stacked gas tanks in EF). </summary>
	private bool IsTargetVisible(Node3D target)
	{
		if (!target.IsVisibleInTree()) // Not visible
			return false;

		if (isSideScrolling) // Ignore vertical targeting when sidescrolling
		{
			if (target.GlobalPosition.Y > Player.CenterPosition.Y + (Player.CollisionSize.Y * 2.0f)) // To high
				return false;

			if (target.GlobalPosition.Y <= Player.CenterPosition.Y - 8f) // Too low
				return false;

			float dot = (target.GlobalPosition - Player.CenterPosition).Dot(Vector3.Up);
			if (dot > .5f)
				return false;
		}

		if (Player.Camera.IsOnScreen(target.GlobalPosition)) // Always allow targeting on-screen objects
			return true;

		if (Player.Camera.IsBehindCamera(target.GlobalPosition)) // Don't allow targeting behind the camera
			return false;

		if (Target != null && Target != target) // Already targeting something
			return false;

		return true;
	}

	private bool IsIgnoringTarget(Node3D target)
	{
		if (Target == target && Player.IsHomingAttacking)
			return false;

		if (Player.Camera.LockonTarget == target) // Always focus on camera lockon targets
			return false;

		Vector3 direction = (target.GlobalPosition - Player.GlobalPosition).RemoveVertical();
		float angle = ExtensionMethods.CalculateForwardAngle(direction);
		if (!direction.IsZeroApprox() && ExtensionMethods.DotAngle(angle, Player.PathFollower.ForwardAngle) > 0) // Player is moving towards lockon-don't ignore it!
			return false;

		float inputStrength = Player.Controller.GetInputStrength();
		float distance = direction.Flatten().Length();
		bool isHoldingForward = inputStrength > 0.8f && Player.Controller.IsHoldingDirection(Player.Controller.GetTargetInputAngle(), Player.PathFollower.ForwardAngle);
		bool isIgnoringTarget = isHoldingForward || (SaveManager.ActiveSkillRing.IsAutorunActive && !Player.Controller.IsBrakeHeld());
		return distance <= IgnoreTargetDistance && isIgnoringTarget;
	}

	private bool HitObstacle(Node3D target)
	{
		// Raycast for obstacles
		Vector3 castPosition = Player.CollisionPosition;
		if (Player.VerticalSpeed < 0)
			castPosition += Player.UpDirection * Player.VerticalSpeed * PhysicsManager.physicsDelta;
		Vector3 castVector = target.GlobalPosition - castPosition;

		RaycastHit h = this.CastRay(castPosition, castVector, Runtime.Instance.lockonObstructionMask);
		DebugManager.DrawRay(castPosition, castVector, Colors.Magenta);

		if (h && h.collidedObject != target)
		{
			if (h.collidedObject.IsInGroup(IgnoreLockonCastGroup))
				return false;

			if (!h.collidedObject.IsInGroup(LevelWallGroup)) // Hit an obstacle
				return true;

			if (h.collidedObject.IsInGroup(LevelWallGroup)) // Cast a new ray from the collision point
			{
				castPosition = h.point + (h.direction.Normalized() * .1f);
				castVector = target.GlobalPosition - castPosition;
				h = this.CastRay(castPosition, castVector, Runtime.Instance.lockonObstructionMask);
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
		IsTargetAttackable = false;
		DisableLockonReticle();
	}

	[Export]
	private Node2D lockonReticle;
	[Export]
	private AnimationPlayer lockonAnimator;
	private readonly float LockonReticleRadius = 100f;
	public void DisableLockonReticle() => lockonAnimator.Play("disable");
	public void UpdateLockonReticle(Vector2 screenPosition, bool isTargetAttackable, bool wasTargetChanged)
	{
		lockonReticle.SetDeferred("position", screenPosition);
		if (!wasTargetChanged && isTargetAttackable == IsTargetAttackable)
			return;

		IsTargetAttackable = isTargetAttackable;
		lockonAnimator.Play("RESET");
		lockonAnimator.Advance(0);
		DisablePerfectHomingAttack();
		if (!IsTargetAttackable)
			lockonAnimator.Play("preview");
		else if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.PerfectHomingAttack))
			lockonAnimator.Play("perfect-enable");
		else
			lockonAnimator.Play("enable");
	}

	/// <summary> Checks whether the player is currently colliding with the Lockon Target. </summary>
	public bool IsCollidingWithTarget()
	{
		if (Target == null)
			return false;

		List<Node3D> collidingObjects = [];
		collidingObjects.AddRange(GetOverlappingAreas());
		collidingObjects.AddRange(GetOverlappingBodies());
		return collidingObjects.Contains(Target);
	}

	// Targeting areas on the lockon layer
	public void OnTargetTriggerEnter(Area3D area)
	{
		if (!potentialTargets.Contains(area))
			potentialTargets.Add(area);
	}

	public void OnTargetTriggerExit(Area3D area)
	{
		if (potentialTargets.Contains(area))
			potentialTargets.Remove(area);
	}

	// Allow targeting physics bodies as well...
	public void OnTargetBodyEnter(PhysicsBody3D body)
	{
		if (!potentialTargets.Contains(body))
			potentialTargets.Add(body);
	}

	public void OnTargetBodyExit(PhysicsBody3D body)
	{
		if (potentialTargets.Contains(body))
			potentialTargets.Remove(body);
	}
}
