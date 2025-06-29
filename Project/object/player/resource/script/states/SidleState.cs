using Godot;
using Project.Core;
using Project.Gameplay.Triggers;

namespace Project.Gameplay;

public partial class SidleState : PlayerState
{
	public SidleTrigger Trigger { get; set; }
	/// <summary> Should the player grab a foot hold when taking damage? </summary>
	public Node ActiveFoothold { get; set; }
	private bool IsOverFoothold => ActiveFoothold != null;

	[Export]
	private PlayerState runState;
	[Export]
	private PlayerState backstepState;

	private float velocity;
	private float cycleTimer;
	/// <summary> Maximum amount of cycles in a single second. </summary>
	private const float CycleFrequency = 3.4f;
	/// <summary> How much to move each cycle.  </summary>
	private const float CycleDistance = 3.8f;
	/// <summary> Smoothing to apply when accelerating.  </summary>
	private const float TractionSmoothing = .1f;
	/// <summary> Smoothing to apply when slowing down.  </summary>
	private const float FrictionSmoothing = .4f;

	/// <summary> Is the player currently being damaged? </summary>
	private DamageStates damageState;
	private enum DamageStates
	{
		Disabled, // Normal sidle movement
		Stagger, // Playing stagger animation
		Falling, // Falling to rail
		Hanging, // Jump recovery allowed
		Recovery, // Recovering back to the ledge
		Respawning
	}

	/// <summary> How long does the stagger animation last? </summary>
	private const float DamageStaggerLength = .8f;
	/// <summary> How long can the player hang onto the rail? </summary>
	private const float DamageHangLength = 5f;
	/// <summary> How long is the transition from staggering to hanging? </summary>
	private const float DamageTransitionLength = 1f;
	/// <summary> How long does the recovery take? </summary>
	private const float RecoveryLength = .84f;

	private readonly StringName JumpAction = "action_jump";

	public override void EnterState()
	{
		velocity = 0;
		cycleTimer = 0;
		damageState = DamageStates.Disabled;

		Player.IsOnGround = true;
		Player.StartExternal(this, Player.PathFollower, .2f);
		Player.Skills.IsSpeedBreakEnabled = false;
		Player.Animator.ExternalAngle = 0; // Rotate to follow pathfollower
		Player.Animator.SnapRotation(Player.Animator.ExternalAngle);
		Player.Animator.StartSidle(Trigger.IsFacingRight);
		Player.Animator.UpdateSidle(cycleTimer);

		Player.Knockback += OnPlayerDamaged;
	}

	public override void ExitState()
	{
		Player.StopSidle();
		if (Player.ExternalController == this)
		{
			Player.StopExternal();
			Player.Animator.SnapRotation(Player.PathFollower.ForwardAngle);
		}

		if (Player.IsDefeated)
			return; // Don't reset animations when respawning

		damageState = DamageStates.Disabled;

		Player.MovementAngle = Player.MoveSpeed > 0 ? Player.PathFollower.ForwardAngle : Player.PathFollower.BackAngle;
		Player.MoveSpeed = Mathf.Abs(Player.MoveSpeed);
		Player.Skills.IsSpeedBreakEnabled = true;

		Player.Animator.ResetState(Player.IsTeleporting ? 0f : .1f);
		Player.Knockback -= OnPlayerDamaged;
	}

	public override PlayerState ProcessPhysics()
	{
		if (!StageSettings.Instance.IsLevelIngame || Player.IsDefeated)
			return null;

		if (Trigger == null || Player.ExternalController != this)
			return Player.MoveSpeed >= 0 ? runState : backstepState;

		if (damageState != DamageStates.Disabled)
		{
			UpdateSidleDamage();
			return null;
		}

		CheckGround();
		UpdateMoveSpeed();
		Player.UpdateExternalControl();
		return null;
	}

	private void CheckGround()
	{
		Vector3 castVector = Vector3.Down * Player.CollisionSize.X * 2.0f;
		RaycastHit hit = Player.CastRay(Player.CenterPosition, castVector, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(Player.CenterPosition, castVector, hit ? Colors.Red : Colors.White);
		if (hit)
			return;

		StartRespawn();
		Player.Animator.SidleFall();
	}

	private void UpdateMoveSpeed()
	{
		// Update velocity
		float targetInput = Player.Controller.InputHorizontal * (Trigger.IsFacingRight ? 1 : -1) * CycleFrequency;
		if (Mathf.IsZeroApprox(velocity) && !Mathf.IsZeroApprox(targetInput)) // Ensure sfx plays when starting to move
			Player.Effect.PlayActionSFX(Player.Effect.SidleSfx);

		if (Mathf.IsZeroApprox(velocity) || Mathf.Sign(targetInput) == Mathf.Sign(velocity))
			velocity = Mathf.Lerp(velocity, targetInput, TractionSmoothing);
		else
			velocity = Mathf.Lerp(velocity, targetInput, FrictionSmoothing);

		// Check walls
		Vector3 castVector = Player.PathFollower.ForwardAxis * Mathf.Sign(velocity) * (Player.CollisionSize.X + Mathf.Abs(velocity * PhysicsManager.physicsDelta));
		RaycastHit hit = Player.CastRay(Player.CenterPosition, castVector, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(Player.CenterPosition, castVector, hit ? Colors.Red : Colors.White);
		if (hit && hit.collidedObject.IsInGroup("sidle wall")) // Kill speed
			velocity = (hit.distance - Player.CollisionSize.X) * Mathf.Sign(velocity);

		if (Mathf.IsZeroApprox(velocity))
		{
			Player.MoveSpeed = 0;
			return;
		}

		cycleTimer += velocity * PhysicsManager.physicsDelta;
		if (Mathf.Abs(cycleTimer - .5f) >= .5f) // Starting a new cycle
		{
			cycleTimer -= Mathf.Sign(cycleTimer);
			Player.Effect.PlayActionSFX(Player.Effect.SidleSfx);
		}

		Player.Animator.UpdateSidle(cycleTimer);
		Player.MoveSpeed = Player.Stats.sidleMovementCurve.Sample(cycleTimer) * velocity * CycleDistance;
		Player.PathFollower.Progress += Player.MoveSpeed * PhysicsManager.physicsDelta;
	}

	/// <summary> Called when the player hits a hazard. </summary>
	private void OnPlayerDamaged()
	{
		// Invincible/Damage routine has already started
		if (Player.IsDefeated || Player.IsInvincible || damageState != DamageStates.Disabled) return;

		velocity = 0;
		cycleTimer = 0;

		if (StageSettings.Instance.CurrentRingCount == 0)
		{
			Player.Knockback -= OnPlayerDamaged;
			Player.StartKnockback(new()
			{
				ignoreMovementState = true
			});
			return;
		}

		damageState = DamageStates.Stagger;

		Player.TakeDamage();
		Player.StartInvincibility();

		Player.Effect.PlayVoice("sidle hurt");
		Player.Animator.SidleDamage();
	}

	/// <summary> Processes player when being damaged. </summary>
	private void UpdateSidleDamage()
	{
		cycleTimer += PhysicsManager.physicsDelta;
		switch (damageState)
		{
			case DamageStates.Stagger:
				ProcessDamageStagger();
				break;
			case DamageStates.Falling:
				ProcessDamageFalling();
				break;
			case DamageStates.Hanging:
				ProcessDamageHanging();
				break;
			case DamageStates.Recovery:
				ProcessDamageRecovery();
				break;
			case DamageStates.Respawning:
				ProcessDamageRespawn();
				break;
		}
	}

	private void ProcessDamageStagger()
	{
		if (cycleTimer >= DamageStaggerLength)
			return;

		cycleTimer = 0;
		if (IsOverFoothold)
		{
			damageState = DamageStates.Falling;
			Player.IsOnGround = false;
			Player.Animator.SidleHang();
		}
		else
		{
			StartRespawn();
			Player.Animator.SidleFall();
		}
	}

	private void ProcessDamageFalling()
	{
		if (cycleTimer < DamageTransitionLength)
			return;

		cycleTimer = 0;
		damageState = DamageStates.Hanging;
		HeadsUpDisplay.Instance.SetPrompt(null, 0);
		HeadsUpDisplay.Instance.SetPrompt(JumpAction, 1);
		HeadsUpDisplay.Instance.ShowPrompts();
	}

	private void ProcessDamageHanging()
	{
		if (Input.IsActionJustPressed("button_jump")) // Process inputs
		{
			// Jump back to the ledge
			cycleTimer = 0;
			damageState = DamageStates.Recovery;
			Player.Effect.PlayActionSFX(Player.Effect.JumpSfx);
			Player.Effect.PlayVoice("grunt");
			Player.Animator.SidleRecovery();
			HeadsUpDisplay.Instance.HidePrompts();
			return;
		}

		if (cycleTimer < DamageHangLength) // Fall
			return;

		StartRespawn();
		Player.Animator.SidleHangFall();
		HeadsUpDisplay.Instance.HidePrompts();
	}

	private void ProcessDamageRecovery()
	{
		if (!Player.IsOnGround)
		{
			if (cycleTimer < RecoveryLength)
				return;

			Player.IsOnGround = true;
			Player.Effect.PlayLandingFX();
		}

		if (!Player.Animator.IsSidleMoving)
			return;

		// Finished
		cycleTimer = 0;
		damageState = DamageStates.Disabled;
		Player.Animator.UpdateSidle(cycleTimer);
	}

	private void ProcessDamageRespawn()
	{
		if (cycleTimer <= .5f)
			return;

		damageState = DamageStates.Disabled;
		Player.StartRespawn();
	}

	/// <summary> Tells the player to start respawning. </summary>
	private void StartRespawn()
	{
		cycleTimer = 0;
		damageState = DamageStates.Respawning;
	}
}
