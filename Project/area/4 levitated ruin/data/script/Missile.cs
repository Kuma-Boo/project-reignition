using Godot;
using Project.Core;

namespace Project.Gameplay.Hazards;

/// <summary> Flies in a straight line or follows a path. </summary>
public partial class Missile : Node3D
{
	[Signal] public delegate void ExplodedEventHandler();
	[Signal] public delegate void DisabledEventHandler();

	[Export] private float lifetime = 5.0f;
	public void SetLifetime(float value) => lifetime = value;
	[Export] private float moveSpeed = 20.0f;
	public void SetSpeed(float value) => moveSpeed = value;
	[Export] private AnimationPlayer animator;

	private float currentLifetime;
	private SpawnData spawnData;
	public void UpdateSpawnTransform(Transform3D t)
	{
		spawnData.spawnTransform = t;
		ResetPhysicsInterpolation();
	}

	private float initialProgress;
	private bool isExploded;

	public override void _Ready()
	{
		StageSettings.Instance.Respawned += Respawn;
		spawnData = new(GetParent(), Transform);

		Respawn();
	}

	public void Respawn()
	{
		isExploded = false;
		spawnData.Respawn(this);

		animator.Play("RESET");
		animator.Advance(0.0);
		animator.Play("fly");

		currentLifetime = lifetime;

		Visible = true;
		ProcessMode = ProcessModeEnum.Inherit;
	}

	public override void _PhysicsProcess(double _)
	{
		if (isExploded)
			return;

		GlobalPosition += this.Forward() * moveSpeed * PhysicsManager.physicsDelta;

		currentLifetime = Mathf.MoveToward(currentLifetime, 0, PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(currentLifetime))
			Explode();
	}

	private void Explode()
	{
		isExploded = true;
		animator.Play("explode");
		EmitSignal(SignalName.Exploded);
	}

	public void Disable()
	{
		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;
		EmitSignal(SignalName.Disabled);
	}
}
