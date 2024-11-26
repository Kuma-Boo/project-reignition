using Godot;
using Project.Core;
using Project.Gameplay.Objects;

namespace Project.Gameplay;

[Tool]
public partial class GolemMajin : Enemy
{
	[Signal]
	public delegate void FallenEventHandler();
	/// <summary> Optional reference to a gas tank that can be thrown at the player. </summary>
	[Export] private GasTank gasTank;
	[Export(PropertyHint.NodePathValidTypes, "Node3D")] private NodePath gasTankParent;
	private Node3D _gasTankParent;
	private bool canThrowGasTank;

	private PathFollow3D pathFollower;
	private float startingProgress;

	private Vector3 velocity;
	private const float RotationResetSpeed = 5f;
	private const float WalkSpeed = 2f;
	private const float DefaultCameraShakeDistance = 20;

	private readonly StringName ThrowTrigger = "parameters/throw_trigger/request";
	private readonly StringName StateTransition = "parameters/state_transition/transition_request";
	private readonly StringName DefeatTransition = "parameters/defeat_transition/transition_request";
	private readonly StringName EnabledConstant = "enabled";
	private readonly StringName DisabledConstant = "disabled";

	protected override void SetUp()
	{
		if (GetParent() is PathFollow3D)
		{
			pathFollower = GetParent<PathFollow3D>();
			pathFollower.UseModelFront = true;
			pathFollower.CubicInterp = false;
			startingProgress = pathFollower.Progress;
		}

		if (gasTank != null)
		{
			_gasTankParent = GetNodeOrNull<Node3D>(gasTankParent);
			gasTank.OnStrike += LockGasTankToGolem;
			gasTank.Monitorable = false;
		}

		base.SetUp();
		AnimationTree.Active = true;
	}

	public override void Respawn()
	{
		if (pathFollower != null)
			pathFollower.Progress = startingProgress;
		AnimationTree.Set(StateTransition, "idle");
		base.Respawn();

		if (gasTank != null)
			CallDeferred(MethodName.RespawnGasTank);
	}

	private void RespawnGasTank()
	{
		canThrowGasTank = true;
		gasTank.GetParent().RemoveChild(gasTank);
		_gasTankParent.AddChild(gasTank);
		gasTank.Transform = Transform3D.Identity;
		gasTank.CallDeferred("InitializeSpawnData");
	}

	private void CheckGasTank()
	{
		if (!canThrowGasTank || !IsActive || !IsInRange || gasTank == null)
			return;

		if (Player.PathFollower.ForwardAxis.Dot(this.Forward()) < 0.5f) // Not facing the player
			return;

		if (gasTank.CastRay(gasTank.GlobalPosition, Player.GlobalPosition - gasTank.GlobalPosition, Runtime.Instance.environmentMask)) // Obstacle
			return;

		canThrowGasTank = false;
		AnimationTree.Set(ThrowTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private void LaunchGasTank()
	{
		if (gasTank.IsTravelling || gasTank.IsDetonated) // Gas tank has already been launched
			return;

		Transform3D t = gasTank.GlobalTransform;
		if (IsDefeated)
		{
			gasTank.endTarget = null;
			gasTank.endPosition = Vector3.Down;
		}
		else
		{
			gasTank.endTarget = Player;
			gasTank.Monitorable = true;
		}

		_gasTankParent.RemoveChild(gasTank);
		StageSettings.Instance.AddChild(gasTank);
		gasTank.SetDeferred("global_position", t.Origin);
		gasTank.CallDeferred(GasTank.MethodName.Launch);
	}

	/// <summary> Update the gas tank to lock onto the golem's head. </summary>
	private void LockGasTankToGolem() => gasTank.endTarget = Hurtbox;

	protected override void EnterRange()
	{
		IsActive = true;

		if (!IsDefeated && pathFollower != null)
			AnimationTree.Set(StateTransition, "walk");
	}

	protected override void Defeat()
	{
		AnimationTree.Set(StateTransition, "defeat");
		base.Defeat();

		if (gasTank != null) // Drop the gas tank
			LaunchGasTank();
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

		CheckGasTank();
		pathFollower.Progress += WalkSpeed * PhysicsManager.physicsDelta;
	}

	public void PlayScreenShake(float magnitude)
	{
		StageSettings.Player.Camera.StartCameraShake(new()
		{
			origin = GlobalPosition,
			maximumDistanceSquared = DefaultCameraShakeDistance * magnitude,
			magnitude = Vector3.One.RemoveDepth() * magnitude,
		});
	}
}