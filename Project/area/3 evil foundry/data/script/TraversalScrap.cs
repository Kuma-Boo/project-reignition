using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary> Controls the behavior of the flying scrap used in the Ifrit Golem boss fight. </summary>
public partial class TraversalScrap : Area3D
{
	[Export(PropertyHint.NodePathValidTypes, "AnimationPlayer")] private NodePath animator;
	[Export(PropertyHint.NodePathValidTypes, "Node3D")] private NodePath root;
	private Node3D _root;
	private AnimationPlayer _animator;
	private bool isFalling;
	private bool isInteractingWithPlayer;
	private SpawnData spawnData;
	private PlayerController Player => StageSettings.Player;

	private float rotationSpeed;
	private Vector3 rotationVector = Vector3.Up;
	private readonly float MinRotationSpeed = 5.0f;
	private readonly float MaxRotationSpeed = 10.0f;

	public override void _Ready()
	{
		spawnData = new SpawnData(GetParent(), Transform);

		_animator = GetNode<AnimationPlayer>(animator);
		_animator.Play("init");

		_root = GetNode<Node3D>(root);
	}

	private void Respawn()
	{
		isFalling = false;
		_animator.Play("show");
		spawnData.Respawn(this);

		_root.Rotation = Vector3.Zero;
		rotationVector = Vector3.Up.Rotated(Vector3.Forward, Runtime.randomNumberGenerator.Randf() * Mathf.Pi);
		rotationVector = rotationVector.Rotated(Vector3.Up, Runtime.randomNumberGenerator.Randf() * Mathf.Pi);
		rotationVector = rotationVector.Normalized();
		rotationSpeed = Mathf.Lerp(MinRotationSpeed, MaxRotationSpeed, Runtime.randomNumberGenerator.Randf());
		if (Runtime.randomNumberGenerator.Randf() < .5f)
			rotationSpeed *= -1;
	}

	public override void _PhysicsProcess(double _)
	{
		if (isFalling)
			return;

		_root.GlobalRotate(rotationVector, rotationSpeed * PhysicsManager.physicsDelta);

		if (!isInteractingWithPlayer)
			return;

		if (!Player.IsJumpDashOrHomingAttack)
			return;

		isFalling = true;
		Player.StartBounce();
		isInteractingWithPlayer = false;
		_animator.Play("fall");
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		isInteractingWithPlayer = true;
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		isInteractingWithPlayer = false;
	}
}
