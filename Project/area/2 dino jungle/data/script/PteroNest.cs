using Godot;

namespace Project.Gameplay.Objects;

// / <summary> For that one act in Dinosaur Jungle. </summary>
public partial class PteroNest : Node3D
{
	[Export(PropertyHint.NodePathValidTypes, "AnimationPlayer")]
	private NodePath animator;
	private AnimationPlayer Animator { get; set; }
	public PteroEgg AssignedEgg { get; set; }
	private CharacterController Character => CharacterController.instance;

	public override void _Ready()
	{
		Animator = GetNodeOrNull<AnimationPlayer>(animator);
		StageSettings.instance.ConnectRespawnSignal(this);
	}

	public void Respawn()
	{
		if (AssignedEgg.IgnoreRespawn)
			return;

		Animator.Play("RESET");
	}

	public void SetType(Node3D model) // Adds the sign model as a child
	{
		AddChild(model);
		model.Transform = Transform3D.Identity;
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;
		if (Character.Lockon.IsHomingAttacking) // Bounce the player
			Character.Lockon.StartBounce();

		if (AssignedEgg.IsReturningToNest || AssignedEgg.IsReturnedToNest)
			return;

		// Check if the target egg is held
		if (AssignedEgg.IsHeld)
		{
			AssignedEgg.ReturnToNest(this);
			Animator.Play("returned");
		}
	}
}