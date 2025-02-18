using Godot;
using Project.Core;

namespace Project.Gameplay.Hazards;

/// <summary> Flies in a straight line or follows a path. </summary>
public partial class Missile : Node3D
{
	[Export] private float lifetime = 5.0f;
	public void SetLifetime(float value) => lifetime = value;
	[Export] private float moveSpeed = 20.0f;
	public void SetSpeed(float value) => moveSpeed = value;
	[Export] private AnimationPlayer animator;

	private float currentLifetime;
	private SpawnData spawnData;
	public void UpdateSpawnTransform(Transform3D t) => spawnData.spawnTransform = t;

	private PathFollow3D pathFollow;
	private float initialProgress;

	public override void _Ready()
	{
		StageSettings.Instance.Respawned += Respawn;
		spawnData = new SpawnData(GetParent(), Transform);

		if (GetParent() is PathFollow3D)
		{
			pathFollow = GetParent<PathFollow3D>();
			initialProgress = pathFollow.Progress;
		}

		Respawn();
	}

	public void Respawn()
	{
		spawnData.Respawn(this);
		if (pathFollow != null)
			pathFollow.Progress = initialProgress;

		animator.Play("RESET");
		animator.Advance(0.0);
		animator.Play("fly");

		currentLifetime = lifetime;

		Visible = true;
		ProcessMode = ProcessModeEnum.Inherit;
	}

	public override void _PhysicsProcess(double _)
	{
		if (pathFollow != null)
		{
			pathFollow.Progress += moveSpeed * PhysicsManager.physicsDelta;

			if (Mathf.IsEqualApprox(pathFollow.ProgressRatio, 1f))
				Explode();

			return;
		}

		GlobalPosition += this.Forward() * moveSpeed * PhysicsManager.physicsDelta;

		currentLifetime = Mathf.MoveToward(currentLifetime, 0, PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(currentLifetime))
			Explode();
	}

	private void Explode() => animator.Play("explode");

	private void Disable()
	{
		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		StageSettings.Player.StartKnockback();
		Explode();
	}
}
