using Godot;
using Project.Core;
using Project.Gameplay.Objects;

namespace Project.Gameplay.Hazards;

/// <summary> Flies in a straight line or follows a path. </summary>
public partial class Missile : Node3D
{
	[Signal] public delegate void ActivatedEventHandler();
	[Signal] public delegate void ExplodedEventHandler();
	[Signal] public delegate void DisabledEventHandler();
	[Signal] public delegate void ObjectHitEventHandler();

	public bool DisableAutoRespawn { get; set; }

	[Export] private bool disableHitboxes;
	[Export] private float activationDelay;
	[Export] private float lifetime = 5.0f;
	public void SetLifetime(float value) => lifetime = value;
	[Export] private float moveSpeed = 20.0f;
	public void SetSpeed(float value) => moveSpeed = value;
	[Export] private AnimationPlayer animator;
	[Export] private Hazard hitbox;

	private float activationTimer;
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
		if (!DisableAutoRespawn)
			StageSettings.Instance.Respawned += Respawn;

		spawnData = new(GetParent(), Transform);
		Respawn();
	}

	public void Respawn()
	{
		Visible = false;
		isExploded = false;
		currentLifetime = lifetime;
		activationTimer = activationDelay;
		spawnData.Respawn(this);

		animator.Play("RESET");
		animator.Advance(0.0);
		if (animator.HasAnimation("init"))
		{
			animator.Play("init");
			animator.Advance(0.0);
		}

		if (hitbox != null)
			SetHitboxState(disableHitboxes);

		if (Mathf.IsZeroApprox(activationTimer))
			Activate();
	}

	public void SetHitboxState(bool isDisabled)
	{
		if (hitbox == null)
			return;

		disableHitboxes = isDisabled;
		hitbox.isDisabled = isDisabled;
	}

	public void Activate()
	{
		Visible = true;
		animator.Play("launch");
		currentLifetime = lifetime;
		ProcessMode = ProcessModeEnum.Inherit;
		EmitSignal(SignalName.Activated);
	}

	public override void _PhysicsProcess(double _)
	{
		if (!Mathf.IsZeroApprox(activationTimer))
		{
			activationTimer = Mathf.MoveToward(activationTimer, 0, PhysicsManager.physicsDelta);
			if (!Mathf.IsZeroApprox(activationTimer))
				return;

			Activate();
		}

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

	public void OnObjectEntered(Node3D _) => EmitSignal(SignalName.ObjectHit);
	public void OnObjectAreaEntered(Area3D _) => EmitSignal(SignalName.ObjectHit);
}
