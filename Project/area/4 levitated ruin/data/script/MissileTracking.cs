using Godot;
using Project.Core;

namespace Project.Gameplay.Hazards;

/// <summary> Follows a path. </summary>
public partial class MissileTracking : PathFollow3D
{
	[Signal] public delegate void ActivatedEventHandler();
	[Signal] public delegate void ExplodedEventHandler();
	[Signal] public delegate void DisabledEventHandler();

	[Export] private int explosionPoint = 0;
	[Export] private float baseSpeed = 20f;
	[Export] private float activationDelay = 0.1f;
	[Export] private AnimationPlayer animator;

	private PlayerController Player => StageSettings.Player;

	private SpawnData spawnData;
	public void UpdateSpawnTransform(Transform3D t)
	{
		spawnData.spawnTransform = t;
		ResetPhysicsInterpolation();
	}

	private float activationTimer;
	private bool isExploded;
	private float initialProgress;

	private float moveSpeed;
	private float moveVelocity;
	private const float TargetRubberbandingDistance = 5f;
	private const float RubberbandingInterpolationDistance = 10f;
	private const float RubberbandingSpeed = 20f;
	private const float MoveSmoothing = 0.2f;

	public override void _Ready()
	{
		StageSettings.Instance.Respawned += Respawn;
		spawnData = new(GetParent(), Transform);
		initialProgress = Progress;

		Respawn();
	}

	public void Respawn()
	{
		moveSpeed = moveVelocity = 0;
		Visible = false;
		isExploded = false;
		activationTimer = activationDelay;
		Progress = initialProgress;
		spawnData.Respawn(this);

		animator.Play("RESET");
		animator.Advance(0.0);

		if (Mathf.IsZeroApprox(activationTimer))
			Activate();
	}

	public void Activate()
	{
		Visible = true;
		animator.Play("launch");
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

		CalculateSpeed();
		Progress += moveSpeed * PhysicsManager.physicsDelta;

		if ((explosionPoint != 0 && Progress > explosionPoint) || Mathf.IsEqualApprox(ProgressRatio, 1f))
			Explode();
	}

	private void CalculateSpeed()
	{
		float targetSpeed = Mathf.Min(Player.MoveSpeed, Player.Stats.GroundSettings.Speed);
		targetSpeed = Mathf.Max(targetSpeed, baseSpeed);
		targetSpeed += CalculateRubberbandingSpeed();
		moveSpeed = ExtensionMethods.SmoothDamp(moveSpeed, targetSpeed, ref moveVelocity, MoveSmoothing);
	}

	private float CalculateRubberbandingSpeed()
	{
		float playerProgress = Player.PathFollower.Progress;
		float missileProgress = Player.PathFollower.GetProgress(GlobalPosition);
		float targetRubberbandingSpeed = playerProgress - TargetRubberbandingDistance - missileProgress;
		targetRubberbandingSpeed /= RubberbandingInterpolationDistance;
		targetRubberbandingSpeed = Mathf.Clamp(targetRubberbandingSpeed, 0f, 1f);
		targetRubberbandingSpeed *= RubberbandingSpeed;
		return targetRubberbandingSpeed;
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
