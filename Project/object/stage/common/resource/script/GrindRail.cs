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

		Character.Animator.UpdateBalancing(isCharging ? 0.0f : Character.Animator.CalculateTurnRatio());
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