using Godot;
using Project.Core;
using Project.Gameplay.Triggers;

namespace Project.Gameplay;

public partial class SidleState : PlayerState
{
	[Export]
	private PlayerState runState;
	[Export]
	private PlayerState backstepState;
	[Export]
	private PlayerState knockbackState;
	
	public SidleTrigger Trigger { get; set; }
	/// <summary> Should the player grab a foot hold when taking damage? </summary>
	public Node ActiveFoothold { get; set; }
	private bool IsOverFoothold => ActiveFoothold != null;

	[Export]
	private LockoutResource lockout;

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

	/*
	public bool ActivateSidle()
	{
		if (!isInteractingWithPlayer)
			return false;

		if (!Player.DisableSidle)
			return false;

		if (isActive)
		{
			if (Player.ExternalController != this) // Overridden
			{
				StopSidle();
				isInteractingWithPlayer = false;
				return false;
			}

			if (damageState == DamageStates.Disabled)
				UpdateSidle();
			else
				UpdateSidleDamage();
		}
		else if (Player.IsOnGround && Player.MovementState == PlayerController.MovementStates.Normal)
		{
			if (Player.ActionState == PlayerController.ActionStates.Normal)
				StartSidle(); // Allows player to slide through sidle section if they know what they're doing
			else if (Player.ActionState == PlayerController.ActionStates.Crouching && Mathf.IsZeroApprox(Player.MoveSpeed))
				Player.ResetActionState();
		}
		return true;
	}
	*/

    public override void EnterState()
    {
		velocity = 0;
		cycleTimer = 0;
		damageState = DamageStates.Disabled;

		Player.Skills.IsSpeedBreakEnabled = false; // Disable speed break
		
		Player.IsSidling = true;
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
		Player.IsSidling = false;

		if (Player.IsDefeated)
			return; // Don't reset animations when respawning

		damageState = DamageStates.Disabled;

		if (Player.ExternalController == this)
		{
			Player.StopExternal();
			Player.Skills.IsSpeedBreakEnabled = true;
			Player.Animator.SnapRotation(Player.PathFollower.ForwardAngle);
		}

		// TODO Clean up this workaround when refactoring PlayerController.cs
		// TEMP WORKAROUND: Use the "wrong" angle so PlayerController will flip it when correcting the "negative speed"
		// Use negative speed to force PlayerController to snap direction
		Player.MovementAngle = Player.MoveSpeed < 0 ? Player.PathFollower.ForwardAngle : Player.PathFollower.BackAngle;
		Player.MoveSpeed = -Mathf.Abs(Player.MoveSpeed);

		// REFACTOR TODO Player.Animator.ResetState(Player.ActionState == PlayerController.ActionStates.Teleport ? 0f : .1f);
		Player.Knockback -= OnPlayerDamaged;
		Trigger = null;
	}

    public override PlayerState ProcessPhysics()
	{
		if (!StageSettings.Instance.IsLevelIngame || Player.IsDefeated)
			return null;

		// Check ground
		Vector3 castVector = Vector3.Down * Player.CollisionSize.X * 2.0f;
		RaycastHit hit = Player.CastRay(Player.CenterPosition, castVector, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(Player.CenterPosition, castVector, hit ? Colors.Red : Colors.White);
		if (!hit) // No ground - Fall and respawn
		{
			GD.Print("Ground not found!!!");
			StartRespawn();
			Player.Animator.SidleFall();
			return null;
		}

		// Update velocity
		float targetVelocity = Input.GetAxis("move_left", "move_right") * (Trigger.IsFacingRight ? 1 : -1) * CycleFrequency;
		if (Mathf.IsZeroApprox(velocity) && !Mathf.IsZeroApprox(targetVelocity)) // Ensure sfx plays when starting to move
			Player.Effect.PlayActionSFX(Player.Effect.SidleSfx);

		if (Mathf.IsZeroApprox(velocity) || Mathf.Sign(targetVelocity) == Mathf.Sign(velocity))
			velocity = Mathf.Lerp(velocity, targetVelocity, TractionSmoothing);
		else
			velocity = Mathf.Lerp(velocity, targetVelocity, FrictionSmoothing);

		// Check walls
		castVector = Player.PathFollower.Forward() * Mathf.Sign(velocity) * (Player.CollisionSize.X + Mathf.Abs(velocity * PhysicsManager.physicsDelta));
		hit = Player.CastRay(Player.CenterPosition, castVector, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(Player.CenterPosition, castVector, hit ? Colors.Red : Colors.White);
		if (hit && hit.collidedObject.IsInGroup("sidle wall")) // Kill speed
			velocity = (hit.distance - Player.CollisionSize.X) * Mathf.Sign(velocity);

		if (!Mathf.IsZeroApprox(velocity))
		{
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
		else
		{
			Player.MoveSpeed = 0;
		}

		Player.UpdateExternalControl();
		return null;
	}

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

	private const float DAMAGE_STAGGER_LENGTH = .8f; // How long does the stagger animation last?
	private const float DAMAGE_HANG_LENGTH = 5f; // How long can the player hang onto the rail?
	private const float DAMAGE_TRANSITION_LENGTH = 1f; // How long is the transition from staggering to hanging?
	private const float RECOVERY_LENGTH = .84f; // How long does the recovery take?

	/// <summary> Called when the player hits a hazard. </summary>
	private void OnPlayerDamaged()
	{
		// Invincible/Damage routine has already started
		if (Player.IsInvincible || damageState != DamageStates.Disabled) return;

		if (StageSettings.Instance.CurrentRingCount == 0)
		{
			Player.StartKnockback();
			return;
		}

		Player.TakeDamage();
		Player.StartInvincibility();
		Player.Effect.PlayVoice("sidle hurt");

		damageState = DamageStates.Stagger;
		velocity = 0;
		cycleTimer = 0;

		Player.Animator.SidleDamage();
	}

	/// <summary> Processes player when being damaged. </summary>
	private void UpdateSidleDamage()
	{
		cycleTimer += PhysicsManager.physicsDelta;
		switch (damageState)
		{
			case DamageStates.Hanging:
				if (cycleTimer >= DAMAGE_HANG_LENGTH) // Fall
				{
					StartRespawn();
					Player.Animator.SidleHangFall();
				}
				else if (Input.IsActionJustPressed("button_jump")) // Process inputs
				{
					// Jump back to the ledge
					cycleTimer = 0;
					damageState = DamageStates.Recovery;
					Player.Effect.PlayActionSFX(Player.Effect.JumpSfx);
					Player.Effect.PlayVoice("grunt");
					Player.Animator.SidleRecovery();
				}
				break;

			case DamageStates.Stagger:
				if (cycleTimer >= DAMAGE_STAGGER_LENGTH) // Fall
				{
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
				break;

			case DamageStates.Falling:
				if (cycleTimer >= DAMAGE_TRANSITION_LENGTH)
				{
					cycleTimer = 0;
					damageState = DamageStates.Hanging;
				}
				break;

			case DamageStates.Recovery:
				if (!Player.IsOnGround)
				{
					if (cycleTimer < RECOVERY_LENGTH)
						return;

					Player.Effect.PlayLandingFX();
				}

				cycleTimer = 0;
				if (Player.Animator.IsSidleMoving) // Finished
				{
					damageState = DamageStates.Disabled;
					Player.Animator.UpdateSidle(cycleTimer);
				}
				break;

			case DamageStates.Respawning:
				if (cycleTimer > .5f)
					Player.StartRespawn();
				break;
		}
	}

	/// <summary> Tells the player to start respawning. </summary>
	private void StartRespawn()
	{
		cycleTimer = 0;
		damageState = DamageStates.Respawning;
	}
}
