using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary> Handles grindrails. Backwards grinding isn't supported. </summary>
[Tool]
public partial class GrindRail : Area3D
{
	[Signal]
	public delegate void GrindStartedEventHandler();
	[Signal]
	public delegate void GrindCompletedEventHandler();

	[ExportGroup("Components")]
	[Export(PropertyHint.NodeType, "Path3D")]
	private NodePath rail;
	private Path3D _rail;
	/// <summary> Reference to the grindrail's pathfollower. </summary>
	private PathFollow3D pathFollower;
	private PlayerController Player => StageSettings.Player;

	[ExportGroup("Invisible Rail Settings")]
	[Export]
	private bool isInvisibleRail;
	[Export(PropertyHint.NodePathValidTypes, "Node3D")]
	private NodePath railModel;
	private Node3D _railModel;
	[Export]
	private ShaderMaterial railMaterial;
	[Export(PropertyHint.NodePathValidTypes, "Node3D")]
	private NodePath startCap;
	private Node3D _startCap;
	[Export(PropertyHint.NodePathValidTypes, "Node3D")]
	private NodePath endCap;
	private Node3D _endCap;
	[Export(PropertyHint.NodePathValidTypes, "CollisionShape3D")]
	private NodePath collider;
	private CollisionShape3D _collider;
	[Export(PropertyHint.Range, "5, 120")]
	/// <summary> Only used for invisible rails. </summary>
	private int railLength = 5;
	/// <summary> Updates rail's visual length. </summary>
	private void UpdateInvisibleRailLength()
	{
		_railModel = GetNodeOrNull<Node3D>(railModel);
		_startCap = GetNodeOrNull<Node3D>(startCap);
		_endCap = GetNodeOrNull<Node3D>(endCap);

		if (_startCap != null)
			_startCap.Position = Vector3.Forward;

		if (_endCap != null)
			_endCap.Position = Vector3.Forward * (railLength - 1);
	}

	/// <summary> Generates rail's collision and curve. </summary>
	private void InitializeInvisibleRail()
	{
		UpdateInvisibleRailLength();
		_railModel.Visible = false;

		// Generate collision and curve
		_collider = GetNodeOrNull<CollisionShape3D>(collider);
		_collider.Shape = new BoxShape3D()
		{
			Size = new Vector3(2f, .5f, railLength)
		};
		_collider.Position = (Vector3.Forward * railLength * .5f) + (Vector3.Down * .05f);

		_rail.Curve = new();
		_rail.Curve.AddPoint(Vector3.Zero, null, Vector3.Forward);
		_rail.Curve.AddPoint(Vector3.Forward * railLength, Vector3.Back);
	}

	/// <summary> Updates invisible rails to sync with the player's position. </summary>
	private void UpdateInvisibleRailPosition()
	{
		_railModel.GlobalPosition = Player.GlobalPosition;
		_railModel.Position = new Vector3(0, _railModel.Position.Y, _railModel.Position.Z); // Ignore player's x-offset
		railMaterial.SetShaderParameter("uv_offset", _railModel.Position.Z);
	}

	/// <summary> Is the rail active? </summary>
	private bool isActive;
	/// <summary> Process collisions? </summary>
	private bool isInteractingWithPlayer;
	/// <summary> Is the player leaving via grindstep? </summary>
	private bool isGrindstepping;
	/// <summary> Can the player obtain bonuses on this rail? </summary>
	private bool allowBonuses = true;

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			return;

		// Create a path follower
		pathFollower = new()
		{
			UseModelFront = true,
			Loop = false,
		};

		_rail = GetNodeOrNull<Path3D>(rail);
		_rail.CallDeferred("add_child", pathFollower);

		// For Secret Rings' hidden rails
		if (isInvisibleRail)
			InitializeInvisibleRail();
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint())
		{
			UpdateInvisibleRailLength();
			return;
		}
		if (isActive)
			UpdateRail();
		else if (isInteractingWithPlayer)
			CheckRailActivation();
	}

	/// <summary> Is a jump currently being buffered? </summary>
	private float jumpBufferTimer;
	/// <summary> Basic measure for attaching at the end of the rail. </summary>
	private float RailFudgeFactor => Player.Stats.GrindSettings.Speed * PhysicsManager.physicsDelta;
	private const float JumpbufferLength = .2f;
	/// <summary> How "magnetic" the rail is. Early 3D Sonic games had a habit of putting this too low. </summary>
	private const float GrindrailSnapping = 1.0f;
	/// <summary> Rail snapping is more generous when performing a grind step. </summary>
	private const float GrindstepRailSnapping = 1.4f;
	private Vector3 closestPoint;
	private void CheckRailActivation()
	{
		/*
		REFACTOR TODO
		if (Player.IsOnGround && !Player.JustLandedOnGround)
			return; // Can't start grinding from the ground
		if (Player.MovementState != PlayerController.MovementStates.Normal)
			return; // Character is busy
		*/
		if (Player.VerticalSpeed > 0f)
			return; // Player must be falling to start grinding!

		// Sync rail pathfollower
		Vector3 delta = _rail.GlobalTransform.Basis.Inverse() * (Player.GlobalPosition - _rail.GlobalPosition);
		pathFollower.Progress = _rail.Curve.GetClosestOffset(delta);
		pathFollower.Progress = Mathf.Clamp(pathFollower.Progress, 0, _rail.Curve.GetBakedLength() - RailFudgeFactor);
		if (pathFollower.Progress >= _rail.Curve.GetBakedLength() - RailFudgeFactor) // Too close to the end of the rail; skip smoothing
			return;

		// Ignore grinds that would immediately put the player into a wall
		if (CheckWall(Player.Stats.GrindSettings.Speed * PhysicsManager.physicsDelta)) return;

		delta = pathFollower.GlobalTransform.Basis.Inverse() * (Player.GlobalPosition - pathFollower.GlobalPosition);
		delta.Y -= Player.VerticalSpeed * PhysicsManager.physicsDelta;
		/*
		REFACTOR TODO
		if (!Player.JustLandedOnGround && delta.Y < -0.01f)
			return;

		// Horizontal validation
		if (Mathf.Abs(delta.X) > GrindrailSnapping &&
			!(Player.ActionState == PlayerController.ActionStates.Grindstep &&
			Mathf.Abs(delta.X) > GrindstepRailSnapping))
		{
			return;
		}

		if (Player.Lockon.IsHomingAttacking) // Cancel any active homing attack
			Player.Lockon.StopHomingAttack();
		*/

		Activate(); // Start grinding
	}

	private void Activate()
	{
		isActive = true;

		/*
		REFACTOR TODO
		if (allowBonuses && Player.IsGrindstepBonusActive)
			BonusManager.instance.QueueBonus(new(BonusType.Grindstep));

		isGrindstepping = false;
		Player.IsGrindstepBonusActive = false;
		*/

		// Reset buffer timers
		jumpBufferTimer = 0;
		perfectChargeTimer = 0;
		currentCharge = 0;

		// Show invisible grindrail
		if (isInvisibleRail)
		{
			_railModel.Visible = true;
			UpdateInvisibleRailPosition();
		}

		float positionSmoothing = .2f;
		float smoothFactor = RailFudgeFactor * 5f;
		if (pathFollower.Progress >= _rail.Curve.GetBakedLength() - smoothFactor) // Calculate smoothing when activating at the end of the rail
		{
			float progressFactor = Mathf.Abs(pathFollower.Progress - _rail.Curve.GetBakedLength());
			positionSmoothing = Mathf.SmoothStep(0f, positionSmoothing, Mathf.Clamp(progressFactor / smoothFactor, 0f, 1f));
		}
		// REFACTOR TODO Player.ResetActionState(); // Reset grind step, cancel stomps, jumps, etc
		Player.State.StartExternal(this, pathFollower, positionSmoothing);

		Player.IsMovingBackward = false;
		// REFACTOR TODO Player.LandOnGround(); // Rail counts as being on the ground
		Player.IsOnGround = true;
		Player.VerticalSpeed = 0f;
		Player.MoveSpeed = Player.Stats.GrindSettings.Speed * Player.Stats.CalculateGrindSpeedRatio(); // Start at the correct speed
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.GrindUp) &&
			SaveManager.ActiveSkillRing.GetAugmentIndex(SkillKey.GrindUp) == 3)
		{
			StageSettings.instance.UpdateRingCount(5, StageSettings.MathModeEnum.Subtract, true);
		}

		Player.Animator.ExternalAngle = 0; // Reset rotation
		Player.Animator.StartBalancing();
		Player.Animator.SnapRotation(Player.Animator.ExternalAngle);

		// Reset FX
		Player.Effect.StartGrindFX(true);

		Player.Connect(PlayerController.SignalName.Knockback, new Callable(this, MethodName.Deactivate));
		Player.Connect(PlayerController.SignalName.ExternalControlCompleted, new Callable(this, MethodName.Deactivate));

		UpdateRail();
		EmitSignal(SignalName.GrindStarted);
	}

	private void Deactivate()
	{
		if (!isActive) return;

		if (jumpBufferTimer > 0) // Player buffered a jump; allow cyote time
		{
			ProcessJump(); // Process Jump calls Deactivate() so we can return early
			return;
		}

		isActive = false;
		isInteractingWithPlayer = false;
		allowBonuses = false;

		Player.Effect.StopGrindFX();
		if (isInvisibleRail) // Hide rail model
			_railModel.Visible = false;

		Player.IsOnGround = false; // Disconnect from the ground
		Player.State.StopExternal();

		// Preserve speed
		float launchAngle = pathFollower.Up().AngleTo(Vector3.Up) * Mathf.Sign(pathFollower.Up().Y);
		Player.VerticalSpeed = Mathf.Sin(launchAngle) * -Player.MoveSpeed;
		Player.MoveSpeed = Mathf.Cos(launchAngle) * Player.MoveSpeed;

		if (!isGrindstepping)
			Player.Animator.ResetState(.2f);
		Player.Animator.SnapRotation(Player.MovementAngle);
		Player.Animator.IsFallTransitionEnabled = true;

		// Disconnect signals
		Player.Disconnect(PlayerController.SignalName.Knockback, new Callable(this, MethodName.Deactivate));
		Player.Disconnect(PlayerController.SignalName.ExternalControlCompleted, new Callable(this, MethodName.Deactivate));

		EmitSignal(SignalName.GrindCompleted);
	}

	private void UpdateRail()
	{
		/*
		REFACTOR TODO
		if (Player.MovementState != PlayerController.MovementStates.External ||
			Player.State.ExternalController != this) // Player disconnected from the rail already
		{
			Deactivate();
			return;
		}
		*/

		Player.UpDirection = pathFollower.Up();
		Player.MovementAngle = ExtensionMethods.CalculateForwardAngle(pathFollower.Forward(), Player.PathFollower.Up());

		UpdateCharge();

		if (Input.IsActionJustPressed("button_jump"))
			jumpBufferTimer = JumpbufferLength;

		jumpBufferTimer = Mathf.MoveToward(jumpBufferTimer, 0, PhysicsManager.physicsDelta);

		if (!Player.Animator.IsBalanceShuffleActive && jumpBufferTimer > 0) //Jumping off rail can only happen when not shuffling
		{
			ProcessJump();
			return;
		}

		if (isInvisibleRail)
			UpdateInvisibleRailPosition();

		// Check wall
		float movementDelta = Player.MoveSpeed * PhysicsManager.physicsDelta;
		RaycastHit hit = CheckWall(movementDelta);
		if (hit && hit.collidedObject is StaticBody3D) // Stop player when colliding with a static body
		{
			movementDelta = 0; // Limit movement distance
			Player.MoveSpeed = 0f;
		}
		else // No walls, Check for crushers
		{
			// REFACTOR TODO Player.CheckCeiling();
		}

		pathFollower.Progress += movementDelta;
		pathFollower.ProgressRatio = Mathf.Clamp(pathFollower.ProgressRatio, 0.0f, 1.0f);
		Player.State.UpdateExternalControl(true);
		Player.Animator.UpdateBalanceSpeed(Player.Stats.GrindSettings.GetSpeedRatioClamped(Player.MoveSpeed));
		if (Mathf.IsEqualApprox(pathFollower.ProgressRatio, 1) || Mathf.IsZeroApprox(Player.MoveSpeed)) // Disconnect from the rail
			Deactivate();
	}

	private void ProcessJump()
	{
		jumpBufferTimer = 0;

		/*
		// Check if the player is holding a direction parallel to rail and start a grindstep
		isGrindstepping = Player.Controller.IsHoldingDirection(Player.MovementAngle + (Mathf.Pi * .5f), true, false) ||
			Player.Controller.IsHoldingDirection(Player.MovementAngle - (Mathf.Pi * .5f), true, false);

		Deactivate();
		REFACTOR TODO
		if (isGrindstepping) // Grindstepping
			Player.StartGrindstep();
		else // Jump normally
			Player.Jump(true);
		*/
	}

	private float currentCharge;
	private float perfectChargeTimer;
	private readonly float PerfectChargeInputWindow = .3f;
	private readonly float ChargeSpeed = 3.0f;
	private void UpdateCharge()
	{
		bool isCharging = Input.IsActionPressed("button_action");
		bool isCharged = Mathf.IsEqualApprox(currentCharge, 1.0f);

		perfectChargeTimer = Mathf.MoveToward(perfectChargeTimer, 0, PhysicsManager.physicsDelta);

		if (isCharging)
		{
			currentCharge = Mathf.MoveToward(currentCharge, 1.0f, ChargeSpeed * PhysicsManager.physicsDelta);
			if (Mathf.IsEqualApprox(currentCharge, 1.0f))
			{
				if (Player.Animator.IsBalanceShuffleActive)
				{
					// Prevent fully charging during a grind shuffle
					currentCharge -= 0.0001f;
				}
				else if (!isCharged) // Fully charged
				{
					perfectChargeTimer = PerfectChargeInputWindow;
					Player.Effect.FullGrindChargeFX();
				}
			}
			else
			{
				Player.Effect.StartChargeFX();
			}
		}
		else if (!Mathf.IsZeroApprox(currentCharge))
		{
			// Update shuffling
			if (!Player.Animator.IsBalanceShuffleActive && isCharged)
			{
				StartShuffle();
				currentCharge = 0;
			}
			else if (Mathf.IsZeroApprox(currentCharge))
			{
				allowBonuses = false;
			}

			currentCharge = Mathf.MoveToward(currentCharge, 0f, ChargeSpeed * PhysicsManager.physicsDelta);
			Player.Effect.StopChargeFX();
		}

		Player.Animator.UpdateBalanceCrouch(isCharging && !Player.Animator.IsBalanceShuffleActive);
		if (!Player.Animator.IsBalanceShuffleActive) // Only slow down when not shuffling
		{
			float speedRatio = Player.Stats.GrindSettings.GetSpeedRatioClamped(Player.MoveSpeed);
			Player.Effect.UpdateGrindFX(speedRatio);

			if (Mathf.IsZeroApprox(perfectChargeTimer))
				Player.MoveSpeed = Player.Stats.GrindSettings.UpdateInterpolate(Player.MoveSpeed, isCharging ? 0f : -1f);
		}

		Player.Animator.UpdateBalancing(isCharging ? 0.0f : Player.Animator.CalculateTurnRatio());
	}

	private void StartShuffle()
	{
		float targetSpeed = Player.Stats.perfectShuffleSpeed;
		if (Mathf.IsZeroApprox(perfectChargeTimer))
		{
			targetSpeed = Player.Stats.GrindSettings.Speed;
			allowBonuses = false;
		}
		else
		{
			Player.Effect.PerfectGrindShuffleFX();
		}

		Player.MoveSpeed = targetSpeed;
		Player.Effect.StartGrindFX(false);
		Player.Animator.StartGrindShuffle();

		if (allowBonuses)
			BonusManager.instance.QueueBonus(new(BonusType.GrindShuffle));
	}

	private RaycastHit CheckWall(float movementDelta)
	{
		float castLength = movementDelta + Player.CollisionSize.X;
		RaycastHit hit = this.CastRay(pathFollower.GlobalPosition, pathFollower.Forward() * castLength, Player.CollisionMask);
		DebugManager.DrawRay(pathFollower.GlobalPosition, pathFollower.Forward() * castLength, hit ? Colors.Red : Colors.White);

		// Block grinding through objects in the given group
		if (hit && hit.collidedObject.IsInGroup("grind wall"))
			return hit;

		return new RaycastHit();
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;

		isInteractingWithPlayer = true;
		CheckRailActivation();
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;
		isInteractingWithPlayer = false;

		Deactivate();
	}
}