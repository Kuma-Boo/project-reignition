using Godot;

namespace Project.Gameplay;

/// <summary> Controls the behavior of the flying scrap used in the Ifrit Golem boss fight. </summary>
public partial class TraversalScrap : Area3D
{
	[Export] private NodePath animator;
	private AnimationPlayer _animator;
	private bool isFalling;
	private bool isInteractingWithPlayer;
	private SpawnData spawnData;
	private PlayerController Player => StageSettings.Player;

	public override void _Ready()
	{
		_animator = GetNode<AnimationPlayer>(animator);
		spawnData = new SpawnData(GetParent(), Transform);
		_animator.Play("init");
	}

	private void Respawn()
	{
		isFalling = false;
		_animator.Play("show");
		spawnData.Respawn(this);
	}

	public override void _PhysicsProcess(double _)
	{
		if (!isInteractingWithPlayer)
			return;

		if (!Player.IsJumpDashOrHomingAttack)
			return;

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
