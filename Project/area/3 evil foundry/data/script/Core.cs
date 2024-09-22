using Godot;

namespace Project.Gameplay.Bosses;

public partial class Core : Area3D
{
	[Signal] public delegate void CoreDestroyedEventHandler(bool isRightHand);

	[Export(PropertyHint.NodeType, "AnimationPlayer")] private NodePath animator;
	private AnimationPlayer Animator { get; set; }
	[Export] private bool isRightHand;

	public bool IsDamaged { get; set; }
	private bool isInteractingWithPlayer;
	private PlayerController Player => StageSettings.Player;

	public override void _Ready()
	{
		Animator = GetNode<AnimationPlayer>(animator);
	}

	public override void _PhysicsProcess(double _)
	{
		if (!isInteractingWithPlayer)
			return;

		ProcessInteraction();
	}

	public void Respawn() => IsDamaged = false;

	public void ShowCore() => Animator.Play("show");
	public void HideCore() => Animator.Play("hide");

	private void ProcessInteraction()
	{
		if (Player.AttackState == PlayerController.AttackStates.None)
			return;

		if (Player.IsHomingAttacking)
			Player.StartBounce(true);

		IsDamaged = true;
		Animator.Play("damage");
		isInteractingWithPlayer = false;
		EmitSignal(SignalName.CoreDestroyed, isRightHand);
	}

	private void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player"))
			return;

		isInteractingWithPlayer = true;
	}

	private void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player"))
			return;

		isInteractingWithPlayer = false;
	}
}
