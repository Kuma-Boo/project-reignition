using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Gameplay;

public partial class PlayerStateController : Node
{
	private PlayerController Player;
	public void Initialize(PlayerController player) => Player = player;

	public bool CanJumpDash { get; set; }
	public bool AllowSidle { get; set; }
	public bool IsInvincible { get; set; }
	public bool IsDefeated { get; set; }

	public void ProcessPhysics()
	{
		UpdateLockoutTimer();
	}

	[Export]
	private AnimationPlayer hitboxAnimator;
	public void ChangeHitbox(StringName hitboxAnimation)
	{
		hitboxAnimator.Play(hitboxAnimation);
		hitboxAnimator.Advance(0);
		hitboxAnimator.Play(hitboxAnimation);
	}

	// REFACTOR TODO
	[Signal]
	public delegate void KnockbackEventHandler(); // This signal is called anytime a hitbox collides with the player, regardless of invincibilty.
	private KnockbackSettings previousKnockbackSettings;
	public void StartKnockback(KnockbackSettings knockbackSettings = new())
	{
		GD.PrintErr("Knockback hasn't been implemented yet.");
	}

	public void TakeDamage()
	{
		GD.PrintErr("Damage hasn't been implemented yet.");
	}

	public void StartInvincibility(float length = 3f)
	{
		GD.PrintErr("Invincibility hasn't been implemented yet.");
	}

	public void StartRespawn(bool useDebugCheckpoint = false)
	{
		GD.PrintErr("Respawn hasn't been implemented yet.");
	}

	public void Teleport(Triggers.TeleportTrigger tr)
	{
		GD.PrintErr("Teleport hasn't been implemented yet.");
	}

	[Export]
	public LaunchState launcherState;
	public void StartLauncher(LaunchSettings settings)
	{
		if (!launcherState.UpdateSettings(settings)) // Failed to start launcher state
			return;

		Player.StateMachine.ChangeState(launcherState);
	}

	public Node ExternalController { get; set; }
	public Node3D ExternalParent { get; set; }
	public void StartExternal(Node controller, Node3D followObject = null, float smoothing = 0f, bool allowSpeedBreak = false)
	{
		GD.PrintErr("Start External controllers is not inimplemented!");
	}

	public void UpdateExternalControl(bool autoResynce = false)
	{
		GD.PrintErr("Process External controllers is not inimplemented!");
	}

	public void StopExternal()
	{
		GD.PrintErr("Stop External controllers is not inimplemented!");
	}

	[Signal]
	public delegate void AttackStateChangeEventHandler();
	/// <summary> Keeps track of how much attack the player will deal. </summary>
	public AttackStates AttackState
	{
		get => attackState;
		set
		{
			attackState = value;
			EmitSignal(SignalName.AttackStateChange);
		}
	}
	private AttackStates attackState;
	public enum AttackStates
	{
		None, // Player is not attacking
		Weak, // Player will deal a single point of damage 
		Strong, // Double Damage -- Perfect homing attacks
		OneShot, // Destroy enemies immediately (i.e. Speedbreak and Crest of Fire)
	}
	public void ResetAttackState() => attackState = AttackStates.None;

	private float lockoutTimer;
	public bool IsLockoutActive => ActiveLockoutData != null;
	public LockoutResource ActiveLockoutData { get; private set; }
	private readonly List<LockoutResource> lockoutDataList = [];

	/// <summary> Adds a ControlLockoutResource to the list, and switches to it depending on it's priority
	public void AddLockoutData(LockoutResource resource)
	{
		if (!lockoutDataList.Contains(resource))
		{
			lockoutDataList.Add(resource); // Add the new lockout data
			if (lockoutDataList.Count >= 2) // List only needs to be sorted if there are multiple elements on it
				lockoutDataList.Sort(new LockoutResource.Comparer());

			if (ActiveLockoutData?.priority == -1) // Remove current lockout?
				RemoveLockoutData(ActiveLockoutData);

			if (resource.priority == -1) // Exclude from priority, take over immediately
				SetLockoutData(resource);
			else
				ProcessCurrentLockoutData();
		}
		else if (ActiveLockoutData == resource) // Reset lockout timer
		{
			lockoutTimer = 0;
		}
	}

	/// <summary>
	/// Removes a ControlLockoutResource from the list
	/// </summary>
	public void RemoveLockoutData(LockoutResource resource)
	{
		if (!lockoutDataList.Contains(resource)) return;
		lockoutDataList.Remove(resource);
		ProcessCurrentLockoutData();
	}

	/// <summary>
	/// Recalculates what the active lockout data is. Called whenever the lockout list is modified.
	/// </summary>
	private void ProcessCurrentLockoutData()
	{
		if (IsLockoutActive && lockoutDataList.Count == 0) // Disable lockout
			SetLockoutData(null);
		else if (ActiveLockoutData != lockoutDataList[^1]) // Change to current data (Highest priority, last on the list)
			SetLockoutData(lockoutDataList[^1]);
	}

	// REFACTOR TODO
	private bool isRecentered;
	private void SetLockoutData(LockoutResource resource)
	{
		ActiveLockoutData = resource;

		if (resource != null) // Reset flags
		{
			lockoutTimer = 0;
			isRecentered = false;
		}
	}

	private void UpdateLockoutTimer()
	{
		if (!IsLockoutActive || Mathf.IsZeroApprox(ActiveLockoutData.length))
			return;

		lockoutTimer = Mathf.MoveToward(lockoutTimer, ActiveLockoutData.length, PhysicsManager.physicsDelta);
		if (Mathf.IsEqualApprox(lockoutTimer, ActiveLockoutData.length))
			RemoveLockoutData(ActiveLockoutData);
	}
}

public struct KnockbackSettings
{
	/// <summary> Should the player be knocked forward? Default is false. </summary>
	public bool knockForward;
	/// <summary> Knock the player around without bouncing them into the air. </summary>
	public bool stayOnGround;
	/// <summary> Apply knockback even when invincible? </summary>
	public bool ignoreInvincibility;
	/// <summary> Don't damage the player? </summary>
	public bool disableDamage;
	/// <summary> Always apply knockback, regardless of state. </summary>
	public bool ignoreMovementState;

	/// <summary> Override default knockback amount? </summary>
	public bool overrideKnockbackSpeed;
	/// <summary> Speed to assign to player. </summary>
	public float knockbackSpeed;

	/// <summary> Override default knockback height? </summary>
	public bool overrideKnockbackHeight;
	/// <summary> Height to move player by. </summary>
	public float knockbackHeight;
}