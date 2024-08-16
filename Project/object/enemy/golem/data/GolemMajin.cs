using Godot;
using Project.Core;
using Project.Gameplay.Objects;

namespace Project.Gameplay;

public partial class GolemMajin : Enemy
{
	[Export]
	private PathFollow3D pathFollower;
	/// <summary> Optional reference to a gas tank that can be thrown at the player. </summary>
	[Export]
	private GasTank gasTank;
	[Export(PropertyHint.NodePathValidTypes, "Node3D")]
	private NodePath gasTankParent;
	private Node3D _gasTankParent;

	private float startingProgress;

	private Vector3 velocity;
	private const float RotationResetSpeed = 5f;
	private const float WalkSpeed = 2f;

	private readonly StringName ThrowTrigger = "parameters/throw_trigger/request";
	private readonly StringName StateTransition = "parameters/state_transition/transition_request";
	private readonly StringName DefeatTransition = "parameters/defeat_transition/transition_request";
	private readonly StringName EnabledConstant = "enabled";
	private readonly StringName DisabledConstant = "disabled";

	protected override void SetUp()
	{
		if (pathFollower != null)
			startingProgress = pathFollower.Progress;
		_gasTankParent = GetNodeOrNull<Node3D>(gasTankParent);
		AnimationTree.Active = true;

		base.SetUp();
	}

	public override void Respawn()
	{
		if (pathFollower != null)
			pathFollower.Progress = startingProgress;
		AnimationTree.Set(StateTransition, "idle");
		base.Respawn();

		if (gasTank != null)
		{
			gasTank.GetParent().RemoveChild(gasTank);
			_gasTankParent.AddChild(gasTank);

			gasTank.SetDeferred("transform", Transform3D.Identity);
		}
	}

	private void CheckGasTank()
	{
		if (!IsActive || gasTank == null)
			return;

		GD.Print(Character.PathFollower.ForwardAxis.Dot(this.Back()));
		if (Character.PathFollower.ForwardAxis.Dot(this.Back()) < 0.5f) // Not facing the player
			return;

		AnimationTree.Set(ThrowTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private void LaunchGasTank()
	{
		if (gasTank.IsTraveling || gasTank.IsDetonated) // Gas tank has already been launched
			return;

		gasTank.Launch();
	}

	protected override void EnterRange()
	{
		IsActive = true;

		if (!IsDefeated)
			AnimationTree.Set(StateTransition, "walk");
	}

	protected override void Defeat()
	{
		AnimationTree.Set(StateTransition, "defeat");
		base.Defeat();
	}

	protected override void UpdateEnemy()
	{
		if (!IsActive) return;
		if (pathFollower == null) return;

		if (IsDefeated)
		{
			pathFollower.Rotation = pathFollower.Rotation.Lerp(Vector3.Zero, RotationResetSpeed * PhysicsManager.physicsDelta);
			return;
		}

		pathFollower.Progress += WalkSpeed * PhysicsManager.physicsDelta;
	}
}