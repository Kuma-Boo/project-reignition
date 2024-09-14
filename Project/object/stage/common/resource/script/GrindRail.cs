using Godot;

namespace Project.Gameplay;

/// <summary> Handles grindrails. Backwards grinding isn't supported. </summary>
[Tool]
public partial class GrindRail : Area3D
{
	[Signal]
	public delegate void GrindStartedEventHandler();
	[Signal]
	public delegate void GrindCompletedEventHandler();

	private PlayerController Player => StageSettings.Player;
	/// <summary> Reference to the grindrail's pathfollower. </summary>
	public PathFollow3D PathFollower { get; private set; }

	[ExportGroup("Components")]
	[Export(PropertyHint.NodeType, "Path3D")]
	private NodePath rail;
	public Path3D Rail { get; private set; }

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

		Rail.Curve = new();
		Rail.Curve.AddPoint(Vector3.Zero, null, Vector3.Forward);
		Rail.Curve.AddPoint(Vector3.Forward * railLength, Vector3.Back);
	}

	/// <summary> Updates invisible rails to sync with the player's position. </summary>
	public void UpdateInvisibleRailPosition()
	{
		if (!isInvisibleRail)
			return;

		_railModel.GlobalPosition = Player.GlobalPosition;
		_railModel.Position = new Vector3(0, _railModel.Position.Y, _railModel.Position.Z); // Ignore player's x-offset
		railMaterial.SetShaderParameter("uv_offset", _railModel.Position.Z);
	}

	/// <summary> Returns the rail's baked length. </summary>
	public float RailLength => Rail.Curve.GetBakedLength();
	/// <summary> Process collisions? </summary>
	public bool IsInteractingWithPlayer { get; private set; }
	/// <summary> Can the player obtain bonuses on this rail? </summary>
	public bool IsBonusDisabled { get; set; }

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			return;

		// Create a path follower
		PathFollower = new()
		{
			UseModelFront = true,
			Loop = false,
		};

		Rail = GetNodeOrNull<Path3D>(rail);
		Rail.CallDeferred("add_child", PathFollower);

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

		if (IsInteractingWithPlayer)
			CheckRailActivation();
	}

	private void CheckRailActivation()
	{
		if (Player.IsRailActivationValid(this))
			Player.StartGrinding(this);
	}

	public void Activate()
	{
		if (isInvisibleRail)
		{
			// Show invisible grindrail
			_railModel.Visible = true;
			UpdateInvisibleRailPosition();
		}

		EmitSignal(SignalName.GrindStarted);
	}

	public void Deactivate()
	{
		IsBonusDisabled = true;

		if (isInvisibleRail) // Hide rail model
			_railModel.Visible = false;

		EmitSignal(SignalName.GrindCompleted);
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;

		IsInteractingWithPlayer = true;
		CheckRailActivation();
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;
		IsInteractingWithPlayer = false;

		Deactivate();
	}
}

/*
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
	private CharacterController Character => CharacterController.instance;
	private CharacterSkillManager Skills => Character.Skills;

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
		_railModel.GlobalPosition = Character.GlobalPosition;
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
	private float RailFudgeFactor => Skills.GrindSettings.Speed * PhysicsManager.physicsDelta;
	private const float JumpbufferLength = .2f;
	/// <summary> How "magnetic" the rail is. Early 3D Sonic games had a habit of putting this too low. </summary>
	private const float GrindrailSnapping = 1.0f;
	/// <summary> Rail snapping is more generous when performing a grind step. </summary>
	private const float GrindstepRailSnapping = 1.4f;
	private Vector3 closestPoint;
	private void CheckRailActivation()
	{
		if (Character.IsOnGround && !Character.JustLandedOnGround)
			return; // Can't start grinding from the ground
		if (Character.MovementState != CharacterController.MovementStates.Normal)
			return; // Character is busy
		if (Character.VerticalSpeed > 0f)
			return; // Player must be falling to start grinding!

		// Sync rail pathfollower
		Vector3 delta = _rail.GlobalTransform.Basis.Inverse() * (Character.GlobalPosition - _rail.GlobalPosition);
		pathFollower.Progress = _rail.Curve.GetClosestOffset(delta);
		pathFollower.Progress = Mathf.Clamp(pathFollower.Progress, 0, _rail.Curve.GetBakedLength() - RailFudgeFactor);
		if (pathFollower.Progress >= _rail.Curve.GetBakedLength() - RailFudgeFactor) // Too close to the end of the rail; skip smoothing
			return;

		// Ignore grinds that would immediately put the player into a wall
		if (CheckWall(Skills.GrindSettings.Speed * PhysicsManager.physicsDelta)) return;

		delta = pathFollower.GlobalTransform.Basis.Inverse() * (Character.GlobalPosition - pathFollower.GlobalPosition);
		delta.Y -= Character.VerticalSpeed * PhysicsManager.physicsDelta;
		if (!Character.JustLandedOnGround && delta.Y < -0.01f)
			return;

		// Horizontal validation
		if (Mathf.Abs(delta.X) > GrindrailSnapping &&
			!(Character.ActionState == CharacterController.ActionStates.Grindstep &&
			Mathf.Abs(delta.X) > GrindstepRailSnapping))
		{
			return;
		}

		if (Character.Lockon.IsHomingAttacking) // Cancel any active homing attack
			Character.Lockon.StopHomingAttack();

		Activate(); // Start grinding
	}

	private void Activate()
	{
		isActive = true;

		if (allowBonuses && Character.IsGrindstepBonusActive)
			BonusManager.instance.QueueBonus(new(BonusType.Grindstep));

		isGrindstepping = false;
		Character.IsGrindstepBonusActive = false;

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
		Character.ResetActionState(); // Reset grind step, cancel stomps, jumps, etc
		Character.StartExternal(this, pathFollower, positionSmoothing);

		Character.IsMovingBackward = false;
		Character.LandOnGround(); // Rail counts as being on the ground
		Character.VerticalSpeed = 0f;
		Character.MoveSpeed = Skills.GrindSettings.Speed * Skills.CalculateGrindSpeedRatio(); // Start at the correct speed
		if (Skills.IsSkillEquipped(SkillKey.GrindUp) && Skills.GetAugmentIndex(SkillKey.GrindUp) == 3)
			StageSettings.instance.UpdateRingCount(5, StageSettings.MathModeEnum.Subtract, true);

		Character.Animator.ExternalAngle = 0; // Reset rotation
		Character.Animator.StartBalancing();
		Character.Animator.SnapRotation(Character.Animator.ExternalAngle);

		// Reset FX
		Character.Effect.StartGrindFX(true);

		Character.Connect(CharacterController.SignalName.Knockback, new Callable(this, MethodName.Deactivate));
		Character.Connect(CharacterController.SignalName.ExternalControlCompleted, new Callable(this, MethodName.Deactivate));

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

		Character.Effect.StopGrindFX();
		if (isInvisibleRail) // Hide rail model
			_railModel.Visible = false;

		Character.IsOnGround = false; // Disconnect from the ground
		Character.ResetMovementState();

		// Preserve speed
		float launchAngle = pathFollower.Up().AngleTo(Vector3.Up) * Mathf.Sign(pathFollower.Up().Y);
		Character.VerticalSpeed = Mathf.Sin(launchAngle) * -Character.MoveSpeed;
		Character.MoveSpeed = Mathf.Cos(launchAngle) * Character.MoveSpeed;

		if (!isGrindstepping)
			Character.Animator.ResetState(.2f);
		Character.Animator.SnapRotation(Character.MovementAngle);
		Character.Animator.IsFallTransitionEnabled = true;

		// Disconnect signals
		Character.Disconnect(CharacterController.SignalName.Knockback, new Callable(this, MethodName.Deactivate));
		Character.Disconnect(CharacterController.SignalName.ExternalControlCompleted, new Callable(this, MethodName.Deactivate));

		EmitSignal(SignalName.GrindCompleted);
	}

	private void UpdateRail()
	{
		if (Character.MovementState != CharacterController.MovementStates.External ||
			Character.ExternalController != this) // Player disconnected from the rail already
		{
			Deactivate();
			return;
		}

		Character.UpDirection = pathFollower.Up();
		Character.MovementAngle = ExtensionMethods.CalculateForwardAngle(pathFollower.Forward(), Character.PathFollower.Up());

		UpdateCharge();

		if (Input.IsActionJustPressed("button_jump"))
			jumpBufferTimer = JumpbufferLength;

		jumpBufferTimer = Mathf.MoveToward(jumpBufferTimer, 0, PhysicsManager.physicsDelta);

		if (!Character.Animator.IsBalanceShuffleActive && jumpBufferTimer > 0) //Jumping off rail can only happen when not shuffling
		{
			ProcessJump();
			return;
		}

		if (isInvisibleRail)
			UpdateInvisibleRailPosition();

		// Check wall
		float movementDelta = Character.MoveSpeed * PhysicsManager.physicsDelta;
		RaycastHit hit = CheckWall(movementDelta);
		if (hit && hit.collidedObject is StaticBody3D) // Stop player when colliding with a static body
		{
			movementDelta = 0; // Limit movement distance
			Character.MoveSpeed = 0f;
		}
		else // No walls, Check for crushers
		{
			Character.CheckCeiling();
		}

		pathFollower.Progress += movementDelta;
		pathFollower.ProgressRatio = Mathf.Clamp(pathFollower.ProgressRatio, 0.0f, 1.0f);
		Character.UpdateExternalControl(true);
		Character.Animator.UpdateBalanceSpeed(Character.Skills.GrindSettings.GetSpeedRatioClamped(Character.MoveSpeed));
		if (Mathf.IsEqualApprox(pathFollower.ProgressRatio, 1) || Mathf.IsZeroApprox(Character.MoveSpeed)) // Disconnect from the rail
			Deactivate();
	}

	private void ProcessJump()
	{
		jumpBufferTimer = 0;

		// Check if the player is holding a direction parallel to rail and start a grindstep
		isGrindstepping = Character.IsHoldingDirection(Character.MovementAngle + (Mathf.Pi * .5f), true, false) ||
			Character.IsHoldingDirection(Character.MovementAngle - (Mathf.Pi * .5f), true, false);

		Deactivate();
		if (isGrindstepping) // Grindstepping
			Character.StartGrindstep();
		else // Jump normally
			Character.Jump(true);
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
				if (Character.Animator.IsBalanceShuffleActive)
				{
					// Prevent fully charging during a grind shuffle
					currentCharge -= 0.0001f;
				}
				else if (!isCharged) // Fully charged
				{
					perfectChargeTimer = PerfectChargeInputWindow;
					Character.Effect.FullGrindChargeFX();
				}
			}
			else
			{
				Character.Effect.StartChargeFX();
			}
		}
		else if (!Mathf.IsZeroApprox(currentCharge))
		{
			// Update shuffling
			if (!Character.Animator.IsBalanceShuffleActive && isCharged)
			{
				StartShuffle();
				currentCharge = 0;
			}
			else if (Mathf.IsZeroApprox(currentCharge))
			{
				allowBonuses = false;
			}

			currentCharge = Mathf.MoveToward(currentCharge, 0f, ChargeSpeed * PhysicsManager.physicsDelta);
			Character.Effect.StopChargeFX();
		}

		Character.Animator.UpdateBalanceCrouch(isCharging && !Character.Animator.IsBalanceShuffleActive);
		if (!Character.Animator.IsBalanceShuffleActive) // Only slow down when not shuffling
		{
			float speedRatio = Character.Skills.GrindSettings.GetSpeedRatioClamped(Character.MoveSpeed);
			Character.Effect.UpdateGrindFX(speedRatio);

			if (Mathf.IsZeroApprox(perfectChargeTimer))
				Character.MoveSpeed = Skills.GrindSettings.UpdateInterpolate(Character.MoveSpeed, isCharging ? 0f : -1f);
		}

		Character.Animator.CallDeferred(CharacterAnimator.MethodName.UpdateBalancing, isCharging ? 0.0f : Character.Animator.CalculateTurnRatio());
	}

	private void StartShuffle()
	{
		float targetSpeed = Skills.perfectShuffleSpeed;
		if (Mathf.IsZeroApprox(perfectChargeTimer))
		{
			targetSpeed = Skills.GrindSettings.Speed;
			allowBonuses = false;
		}
		else
		{
			Character.Effect.PerfectGrindShuffleFX();
		}

		Character.MoveSpeed = targetSpeed;
		Character.Effect.StartGrindFX(false);
		Character.Animator.StartGrindShuffle();

		if (allowBonuses)
			BonusManager.instance.QueueBonus(new(BonusType.GrindShuffle));
	}

	private RaycastHit CheckWall(float movementDelta)
	{
		float castLength = movementDelta + Character.CollisionSize.X;
		RaycastHit hit = this.CastRay(pathFollower.GlobalPosition, pathFollower.Forward() * castLength, Character.CollisionMask);
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
*/