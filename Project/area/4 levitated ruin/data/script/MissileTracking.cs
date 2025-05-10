using Godot;
using Project.Core;

namespace Project.Gameplay.Hazards;

/// <summary> Follows a path. </summary>
public partial class MissileTracking : PathFollow3D
{
	[Signal] public delegate void ActivatedEventHandler();
	[Signal] public delegate void ExplodedEventHandler();
	[Signal] public delegate void DisabledEventHandler();

	[Export] private float moveSpeed = 30.0f;
	public void SetSpeed(float value) => moveSpeed = value;
	[Export] private AnimationPlayer animator;

	private SpawnData spawnData;
	public void UpdateSpawnTransform(Transform3D t)
	{
		spawnData.spawnTransform = t;
		ResetPhysicsInterpolation();
	}

	private bool isActive;
	private bool isExploded;
	private float initialProgress;

	public override void _Ready()
	{
		StageSettings.Instance.Respawned += Respawn;
		spawnData = new(GetParent(), Transform);
		initialProgress = Progress;

		Respawn();
	}

	public void Respawn()
	{
		isActive = false;
		isExploded = false;
		spawnData.Respawn(this);
		Progress = initialProgress;

		animator.Play("RESET");
		animator.Advance(0.0);
	}

	public void Activate()
	{
		Visible = true;
		isActive = true;
		animator.Play("fly");
		ProcessMode = ProcessModeEnum.Inherit;
		EmitSignal(SignalName.Activated);
	}

	public override void _PhysicsProcess(double _)
	{
		if (!isActive || isExploded)
			return;

		Progress += moveSpeed * PhysicsManager.physicsDelta;

		if (Mathf.IsEqualApprox(ProgressRatio, 1f))
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
