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
	private readonly float DISTANCE_FUDGE_AMOUNT = 1f;
	private readonly Array<Node3D> activeTargets = new(); //List of targetable objects

	/// <summary> Enables detection of new lockonTargets. </summary>
	public bool IsMonitoring { get; set; }

	public bool IsHomingAttacking { get; set; }
	public bool IsPerfectHomingAttack { get; private set; }
	private bool monitoringPerfectHomingAttack;
	public void EnablePerfectHomingAttack() => monitoringPerfectHomingAttack = true;
	public void DisablePerfectHomingAttack() => monitoringPerfectHomingAttack = false;
	public Vector3 HomingAttackDirection => Target != null ? (Target.GlobalPosition - GlobalPosition).Normalized() : this.Forward();

	public void StartHomingAttack()
	{
		IsHomingAttacking = true;
		IsPerfectHomingAttack = Character.Skills.IsSkillEnabled(SkillKeys.PerfectHomingAttack) && monitoringPerfectHomingAttack;
	}


	public void StopHomingAttack()
	{
		IsHomingAttacking = false;
		IsPerfectHomingAttack = false;
		Character.ResetActionState();
		ResetLockonTarget();
	}


	public void UpdateLockonTargets()
	{
		wasTargetChanged = false;
		GlobalRotation = Vector3.Up * Character.PathFollower.ForwardAngle;

		if (IsMonitoring)
		{
			Node3D currentTarget = Target;
			float closestDistance = Mathf.Inf; // Current closest target

			// Current lockon target starts as the closest target
			if (Target?.IsInsideTree() == true)
			{
				closestDistance = Target.GlobalPosition.Flatten().DistanceSquaredTo(Character.GlobalPosition.Flatten());

				if (closestDistance < DISTANCE_FUDGE_AMOUNT && Character.IsHoldingDirection(Character.PathFollower.ForwardAngle))
				{
					// Allow the player to stop targeting objects directly beneath them
					currentTarget = null;
					closestDistance = Mathf.Inf;
					ResetLockonTarget();
				}
			}

			// Check whether to pick a new target
			for (int i = 0; i < activeTargets.Count; i++)
			{
				if (currentTarget == activeTargets[i])
					continue;

				if (IsTargetValid(activeTargets[i]) != TargetState.Valid)
					continue;

				float dst = activeTargets[i].GlobalPosition.Flatten().DistanceSquaredTo(Character.GlobalPosition.Flatten());

				if (currentTarget != null)
				{
					if (dst > closestDistance + DISTANCE_FUDGE_AMOUNT)
						continue; // Check whether the object is close enough to be considered
					else if (dst > closestDistance - DISTANCE_FUDGE_AMOUNT && activeTargets[i].GlobalPosition.Y <= currentTarget.GlobalPosition.Y)
						continue; // Within fudge range, decide priority based on height
				}

				// Update data
				currentTarget = activeTargets[i];
				closestDistance = dst;
			}

			if (currentTarget != null && currentTarget != Target) // Target has changed
				Target = currentTarget;
			else if (Target != null && IsTargetValid(Target) != TargetState.Valid) // Validate current lockon target
				Target = null;
		}
		else if (IsHomingAttacking) // Validate homing attack target
		{
			TargetState state = IsTargetValid(Target);
			if (state == TargetState.NotInList)
				Target = null;
		}

		if (Target != null)
		{
			Vector2 screenPos = Character.Camera.ConvertToScreenSpace(Target.GlobalPosition);
			UpdateLockonReticle(screenPos, wasTargetChanged);
		}
		else if (wasTargetChanged) // Disable UI
		{
			DisableLockonReticle();
		}
	}

	private TargetState IsTargetValid(Node3D t)
	{
		if (!activeTargets.Contains(t)) // Not in target list anymore (target hitbox may have been disabled)
			return TargetState.NotInList;

		if (Character.ActionState == CharacterController.ActionStates.Damaged) // Character is busy
			return TargetState.PlayerBusy;

		if (!t.IsVisibleInTree() || !Character.Camera.IsOnScreen(t.GlobalPosition)) // Not visible
			return TargetState.Invisible;

		//Raycast for obstacles
		Vector3 castPosition = Character.GlobalPosition;
		if (Character.VerticalSpeed < 0)
			castPosition += Character.UpDirection * Character.VerticalSpeed * PhysicsManager.physicsDelta;
		Vector3 castVector = t.GlobalPosition - castPosition;
		RaycastHit h = this.CastRay(castPosition, castVector, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(castPosition, castVector, Colors.Magenta);

		if (h && h.collidedObject != t)
			return TargetState.HitObstacle;

		float distance = t.GlobalPosition.Flatten().DistanceSquaredTo(Character.GlobalPosition.Flatten());
		if (distance < DISTANCE_FUDGE_AMOUNT && Character.IsHoldingDirection(Character.PathFollower.ForwardAngle))
			return TargetState.PlayerIgnored;

		return TargetState.Valid;
	}


	public void ResetLockonTarget()
	{
		Character.Camera.LockonTarget = null;

		if (Target != null) //Reset Active Target
		{
			Target = null;
			DisableLockonReticle();
		}
	}

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
	public bool IsBouncingLockoutActive => Character.ActiveLockoutData == bounceLockoutSettings;
	public bool CanInterruptBounce { get; private set; }


	public void UpdateBounce()
	{
		bounceInterruptTimer = Mathf.MoveToward(bounceInterruptTimer, 0, PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(bounceInterruptTimer))
			CanInterruptBounce = true;

		Character.MoveSpeed = Mathf.MoveToward(Character.MoveSpeed, 0f, Character.GroundSettings.friction * PhysicsManager.physicsDelta);
		Character.VerticalSpeed -= Runtime.GRAVITY * PhysicsManager.physicsDelta;
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
			if (!IsBouncingLockoutActive)
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
			Character.MoveSpeed = -bounceSpeed;

		if (IsBouncingLockoutActive) return;

		Character.CanJumpDash = true;
		Character.VerticalSpeed = Runtime.CalculateJumpPower(bounceHeight);
		Character.MovementAngle = Character.PathFollower.ForwardAngle;
		Character.AddLockoutData(bounceLockoutSettings);
		Character.ResetActionState();

		Character.Animator.ResetState(0.1f);
		Character.Animator.BounceTrick();
		Character.Effect.PlayActionSFX(Character.Effect.JUMP_SFX);
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
