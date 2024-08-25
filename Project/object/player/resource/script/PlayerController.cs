using Godot;

namespace Project.Gameplay;

public partial class PlayerController : CharacterBody3D
{
	[Export]
	private NodePath inputController;
	private PlayerInputController _inputController;

	[Export]
	private NodePath stateMachine;
	private PlayerStateMachine _stateMachine;

	public override void _Ready()
	{
		_inputController = GetNode<PlayerInputController>(inputController);

		_stateMachine = GetNode<PlayerStateMachine>(stateMachine);
		_stateMachine.Initialize(this, _inputController);
	}

	public override void _PhysicsProcess(double _)
	{
		_inputController.ProcessInputs();
		_stateMachine.ProcessPhysics();
	}

	/// <summary> Player's true movespeed, ignoring slopes. </summary>
	public float MoveSpeed { get; set; }

	/*
	[Export]
	public CameraController Camera { get; private set; }
	[Export]
	public CharacterPathFollower PathFollower { get; private set; }
	[Export]
	public CharacterAnimator Animator { get; private set; }
	[Export]
	public CharacterEffect Effect { get; private set; }
	[Export]
	public CharacterSkillManager Skills { get; private set; }
	[Export]
	public CharacterLockon Lockon { get; private set; }
	*/
}
