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
		if (Player.State.IsRailActivationValid(this))
			Player.State.StartGrinding(this);
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
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;
		IsInteractingWithPlayer = false;

		Deactivate();
	}
}