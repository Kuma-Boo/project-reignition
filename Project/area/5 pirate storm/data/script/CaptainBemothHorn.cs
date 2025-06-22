using Godot;
using Project.Core;
using Project.CustomNodes;

namespace Project.Gameplay.Bosses;

public partial class CaptainBemothHorn : Node3D
{
	[Signal] public delegate void PoppedEventHandler();
	[Signal] public delegate void HangStartedEventHandler();
	[Signal] public delegate void JoltedEventHandler();
	[Signal] public delegate void JumpedEventHandler();

	[Export] private AnimationTree animator;
	[Export] private CollisionShape3D collider;
	[Export] private GroupGpuParticles3D joltFx;
	[Export] private int maxHealth;
	// Jolt curve for pulling out horns
	[Export] private Curve joltCurve;
	private int damageDealt;

	private SpawnData spawnData;

	public Node3D FollowObject => collider;
	public bool IsPopping { get; private set; }
	public bool IsPopReady => damageDealt == maxHealth;
	public bool IsJoltingHorn => joltTimer >= 0;
	private float joltTimer;
	private readonly float JoltLength = 0.4f;
	private readonly float PopSpeed = 10f;

	private readonly StringName PullBlend = "parameters/pull_blend/blend_amount";
	private readonly StringName PopTransition = "parameters/pop_transition/transition_request";

	public override void _Ready()
	{
		spawnData = new(GetParent(), Transform);
		animator.Active = true;
		Respawn();
	}

	public void Respawn()
	{
		joltTimer = -1f;
		damageDealt = 0;
		IsPopping = false;

		Visible = true;
		ProcessMode = ProcessModeEnum.Inherit;

		spawnData.Respawn(this);
		animator.Set(PullBlend, 0f);
		animator.Set(PopTransition, "disabled");
	}

	public void Despawn()
	{
		IsPopping = false;
		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;
	}

	public void EnableLockon() => collider.Disabled = false;
	public void DisableLockon() => collider.Disabled = true;

	public override void _PhysicsProcess(double _)
	{
		if (IsPopping)
			GlobalPosition += (Vector3.Up + this.Back()) * PhysicsManager.physicsDelta * PopSpeed;

		if (!IsJoltingHorn)
			return;

		joltTimer = Mathf.MoveToward(joltTimer, JoltLength, PhysicsManager.physicsDelta);
		float start = (damageDealt - 1) / (float)maxHealth;
		float end = damageDealt / (float)maxHealth;
		float t = joltCurve.Sample(joltTimer / JoltLength);
		animator.Set(PullBlend, Mathf.Lerp(start, end, t));

		if (Mathf.IsEqualApprox(joltTimer, JoltLength)) // Finish jolt
			joltTimer = -1f;
	}

	public void StartPop()
	{
		// Move to global space
		Transform3D t = GlobalTransform;
		GetParent().RemoveChild(this);
		StageSettings.Instance.AddChild(this);
		GlobalTransform = t;

		IsPopping = true;
		animator.Set(PopTransition, "enabled");
		EmitSignal(SignalName.Popped);
	}

	///<summary> Pulls the horn out by a tiny bit. </summary>
	public void JoltHorn(bool strongJolt = true)
	{
		// Start jolt
		joltTimer = 0;
		damageDealt += strongJolt ? 2 : 1;

		// TODO Play SFX
		joltFx.RestartGroup();

		if (damageDealt >= maxHealth)
		{
			damageDealt = maxHealth;
			return;
		}

		EmitSignal(SignalName.Jolted);
	}

	public void JumpOff() => EmitSignal(SignalName.Jumped);

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player"))
			return;

		if (StageSettings.Player.IsLaunching)
			return;

		StageSettings.Player.StartHorn(this);
		EmitSignal(SignalName.HangStarted);
	}
}
