using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers;

/// <summary>
/// Handles sidle behaviour.
/// </summary>
public partial class SidleTrigger : Area3D
{
	[Signal]
	public delegate void ActivatedEventHandler();
	[Signal]
	public delegate void DeactivatedEventHandler();

	/// <summary> Reference to the active foothold. </summary>
	public static FootholdTrigger CurrentFoothold { get; set; }
	/// <summary> Should the player grab a foot hold when taking damage? </summary>
	private bool IsOverFoothold => CurrentFoothold != null;

	/// <summary> Which way to sidle? </summary>
	[Export]
	private bool isFacingRight = true;
	[Export]
	private LockoutResource lockout;

	private bool isActive;
	private bool isInteractingWithPlayer;
	private PlayerController Player => StageSettings.Player;

	private float velocity;
	private float cycleTimer;
	/// <summary> Maximum amount of cycles in a single second. </summary>
	private const float CYCLE_FREQUENCY = 3.4f;
	/// <summary> How much to move each cycle.  </summary>
	private const float CYCLE_DISTANCE = 3.8f;
	/// <summary> Smoothing to apply when accelerating.  </summary>
	private const float TRACTION_SMOOTHING = .1f;
	/// <summary> Smoothing to apply when slowing down.  </summary>
	private const float FRICTION_SMOOTHING = .4f;

	public override void _Ready() => StageSettings.instance.ConnectRespawnSignal(this);

	public override void _PhysicsProcess(double _)
	{
		if (!isInteractingWithPlayer)
			return;

		if (!Player.State.AllowSidle)
			return;

		if (isActive)
		{
			if (Player.State.ExternalController != this) // Overridden
			{
				StopSidle();
				isInteractingWithPlayer = false;
				return;
			}

			if (damageState == DamageStates.Disabled)
				UpdateSidle();
			else
				UpdateSidleDamage();
		}
		/* RERFACTOR TODO
		else if (Player.IsOnGround && Player.MovementState == PlayerController.MovementStates.Normal)
		{
			if (Player.ActionState == PlayerController.ActionStates.Normal)
				StartSidle(); // Allows player to slide through sidle section if they know what they're doing
			else if (Player.ActionState == PlayerController.ActionStates.Crouching && Mathf.IsZeroApprox(Player.MoveSpeed))
				Player.ResetActionState();
		}
		*/
	}

	private void StartSidle()
	{
		isActive = true;
		velocity = 0;
		cycleTimer = 0;
		damageState = DamageStates.Disabled;

		Player.IsOnGround = true;
		Player.State.StartExternal(this, Player.PathFollower, .2f);
		Player.Animator.ExternalAngle = 0; // Rotate to follow pathfollower
		Player.Animator.SnapRotation(Player.Animator.ExternalAngle);
		Player.Animator.StartSidle(isFacingRight);
		Player.Animator.UpdateSidle(cycleTimer);

		if (!Player.IsConnected(PlayerController.SignalName.Knockback, new Callable(this, MethodName.OnPlayerDamaged)))
			Player.Connect(PlayerController.SignalName.Knockback, new Callable(this, MethodName.OnPlayerDamaged));
	}

	private void UpdateSidle()
	{
		if (!StageSettings.instance.IsLevelIngame || Player.State.IsDefeated)
			return;

		// Check ground
		Vector3 castVector = Vector3.Down * Player.CollisionSize.X * 2.0f;
		RaycastHit hit = this.CastRay(Player.CenterPosition, castVector, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(Player.CenterPosition, castVector, hit ? Colors.Red : Colors.White);
		if (!hit) // No ground - Fall and respawn
		{
			GD.Print("Ground not found!!!");
			StartRespawn();
			Player.Animator.SidleFall();
			return;
		}

		// Update velocity
		float targetVelocity = Input.GetAxis("move_left", "move_right") * (isFacingRight ? 1 : -1) * CYCLE_FREQUENCY;
		if (Mathf.IsZeroApprox(velocity) && !Mathf.IsZeroApprox(targetVelocity)) // Ensure sfx plays when starting to move
			Player.Effect.PlayActionSFX(Player.Effect.SidleSfx);

		if (Mathf.IsZeroApprox(velocity) || Mathf.Sign(targetVelocity) == Mathf.Sign(velocity))
			velocity = Mathf.Lerp(velocity, targetVelocity, TRACTION_SMOOTHING);
		else
			velocity = Mathf.Lerp(velocity, targetVelocity, FRICTION_SMOOTHING);

		// Check walls
		castVector = Player.PathFollower.Forward() * Mathf.Sign(velocity) * (Player.CollisionSize.X + Mathf.Abs(velocity * PhysicsManager.physicsDelta));
		hit = this.CastRay(Player.CenterPosition, castVector, Runtime.Instance.environmentMask);
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
			Player.MoveSpeed = Player.Stats.sidleMovementCurve.Sample(cycleTimer) * velocity * CYCLE_DISTANCE;
			Player.PathFollower.Progress += Player.MoveSpeed * PhysicsManager.physicsDelta;
		}
		else
		{
			Player.MoveSpeed = 0;
		}

		Player.State.UpdateExternalControl();
	}

	private void StopSidle()
	{
		if (!isActive)
			return; // Already deactivated

		isActive = false;

		if (Player.State.IsDefeated)
			return; // Don't reset animations when respawning

		damageState = DamageStates.Disabled;

		if (Player.State.ExternalController == this)
		{
			Player.State.StopExternal();
			Player.Animator.SnapRotation(Player.PathFollower.ForwardAngle);
		}

		// TODO Clean up this workaround when refactoring PlayerController.cs
		// TEMP WORKAROUND: Use the "wrong" angle so PlayerController will flip it when correcting the "negative speed"
		// Use negative speed to force PlayerController to snap direction
		Player.MovementAngle = Player.MoveSpeed < 0 ? Player.PathFollower.ForwardAngle : Player.PathFollower.BackAngle;
		Player.MoveSpeed = -Mathf.Abs(Player.MoveSpeed);

		// REFACTOR TODO Player.Animator.ResetState(Player.ActionState == PlayerController.ActionStates.Teleport ? 0f : .1f);
		Player.Disconnect(PlayerController.SignalName.Knockback, new Callable(this, MethodName.OnPlayerDamaged));
	}

	#region Damage
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
		if (Player.State.IsInvincible || damageState != DamageStates.Disabled) return;

		if (StageSettings.instance.CurrentRingCount == 0)
		{
			StopSidle();
			Player.State.StartKnockback();
			return;
		}

		Player.State.TakeDamage();
		Player.State.StartInvincibility();
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
					Player.State.StartRespawn();
				break;
		}
	}

	/// <summary> Tells the player to start respawning. </summary>
	private void StartRespawn()
	{
		cycleTimer = 0;
		damageState = DamageStates.Respawning;
	}

	public void Respawn()
	{
		if (!isActive) return;

		StartSidle();
		Player.Animator.UpdateSidle(cycleTimer);
	}
	#endregion

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;

		isInteractingWithPlayer = true;

		Player.Skills.IsSpeedBreakEnabled = false; // Disable speed break
		Player.State.AddLockoutData(lockout); // Apply lockout
		EmitSignal(SignalName.Activated); // Immediately emit signals to allow path changes, etc.
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;

		isInteractingWithPlayer = false;
		Player.State.RemoveLockoutData(lockout);

		/*
		REFACTOR TODO
		if (Player.MovementState == PlayerController.MovementStates.Normal)
			Player.Skills.IsSpeedBreakEnabled = true; // Re-enable speed break
		*/

		StopSidle();
		EmitSignal(SignalName.Deactivated); // Deactivate signals
	}
}
