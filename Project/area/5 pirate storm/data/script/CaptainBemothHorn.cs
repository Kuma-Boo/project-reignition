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
	[Export] private Area3D area;
	[Export] private GroupGpuParticles3D joltFx;

	[Export] private int maxHealth;
	/// <summary> How long to delay the actual pop so animations have time to catch up. </summary>
	[Export] private float popDelay = .5f;
	[Export] private bool isPopMovementDisabled;
	private float popTimer;

	// Jolt curve for pulling out horns
	[Export] private Curve joltCurve;
	private int damageDealt;

	private SpawnData spawnData;

	public Node3D FollowObject => area;
	public bool IsPopping { get; private set; }
	public bool IsPopReady => damageDealt == maxHealth;
	public bool IsJoltingHorn => joltTimer >= 0;
	private float joltTimer;
	private readonly float JoltLength = 0.4f;
	private readonly float PopSpeed = 20f;

	private readonly StringName JoltTrigger = "parameters/jolt_trigger/request";
	private readonly StringName JoltBlend = "parameters/jolt_blend/blend_amount";
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
		popTimer = 0;
		damageDealt = 0;
		IsPopping = false;

		Visible = true;
		ProcessMode = ProcessModeEnum.Inherit;

		spawnData.Respawn(this);
		animator.Set(PullBlend, 0f);
		animator.Set(JoltBlend, 0f);
		animator.Set(PopTransition, "disabled");
		animator.Set(JoltTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
	}

	public void Despawn()
	{
		IsPopping = false;
		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;
	}

	public void EnableLockon() => area.Monitorable = true;
	public void DisableLockon() => area.Monitorable = false;

	public override void _PhysicsProcess(double _)
	{
		if (IsPopping)
		{
			if (!isPopMovementDisabled)
				GlobalPosition += Vector3.Up * PhysicsManager.physicsDelta * PopSpeed;

			return;
		}

		if (IsPopReady)
		{
			if (Mathf.IsEqualApprox(popTimer, popDelay))
				StartPop();

			popTimer = Mathf.MoveToward(popTimer, popDelay, PhysicsManager.physicsDelta);
			return;
		}

		if (!IsJoltingHorn)
			return;

		joltTimer = Mathf.MoveToward(joltTimer, JoltLength, PhysicsManager.physicsDelta);
		float start = (damageDealt - 1) / (float)maxHealth;
		float end = damageDealt / (float)maxHealth;
		float pullBlend = joltCurve.Sample(joltTimer / JoltLength);
		pullBlend = Mathf.Lerp(start, end, pullBlend);
		animator.Set(PullBlend, pullBlend);
		animator.Set(JoltBlend, pullBlend);

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

		StageSettings.Player.Camera.StartCameraShake(new()
		{
			magnitude = Vector3.One.RemoveDepth() * 2f,
			duration = .5f
		});

		IsPopping = true;
		animator.Set(PopTransition, "enabled");
	}

	///<summary> Pulls the horn out by a tiny bit. </summary>
	public void JoltHorn(int damage)
	{
		// Start jolt
		joltTimer = 0;
		damageDealt += damage;

		// TODO Play SFX
		joltFx.RestartGroup();
		animator.Set(JoltTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		EmitSignal(SignalName.Jolted);

		if (damageDealt >= maxHealth)
		{
			damageDealt = maxHealth;
			HeadsUpDisplay.Instance.HidePrompts();
			EmitSignal(SignalName.Popped); // Emit pop signal early so animations/cameras can play
		}
	}

	public void JumpOff() => EmitSignal(SignalName.Jumped);

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player") || IsPopping || StageSettings.Player.IsLaunching)
			return;

		StageSettings.Player.StartHorn(this);
		EmitSignal(SignalName.HangStarted);
	}
}
