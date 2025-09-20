using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Gameplay.Objects;

[Tool]
public partial class GasTank : Area3D
{
	[Signal]
	public delegate void OnStrikeEventHandler();
	[Signal]
	public delegate void DetonatedEventHandler();

	/// <summary> Field is public so enemies can set this as needed. </summary>
	[Export] public float height;
	[Export] public bool globalEndPosition;
	/// <summary> Field is public so enemies can set this as needed. </summary>
	[Export] public Vector3 endPosition;
	/// <summary> Used if you want to target a particular object instead of a position (End position is recalculated on launch). </summary>
	[Export] public Node3D endTarget;
	private Vector3 startPosition;
	[Export] public bool disableRespawning;
	/// <summary> Used to prevent gas tanks from exploding each other. </summary>
	[Export] public bool disableGasTankInteractions;

	[ExportGroup("Components")]
	[Export(PropertyHint.NodePathValidTypes, "Node3D")]
	private NodePath root;
	private Node3D Root { get; set; }

	[Export(PropertyHint.NodePathValidTypes, "AnimationPlayer")]
	private NodePath animator;
	private AnimationPlayer Animator { get; set; }
	[Export] private float timeScale = 0.8f;
	private float currentTimeScale;

	private bool isInteractingWithPlayer;
	private bool isPlayerInExplosion;
	private SpawnData spawnData;
	private readonly List<Enemy> enemyList = [];
	private readonly List<GasTank> tankList = [];

	public bool AllowDoubleLaunch { get; set; }
	public bool IsFalling { get; private set; }
	public bool IsDetonated { get; private set; }
	public bool IsTravelling { get; private set; }
	private float travelTime;
	private readonly float VisualRotationSpeed = 10f;
	private readonly float StrikeTimeScale = 1.2f;

	public LaunchSettings GetLaunchSettings() => LaunchSettings.Create(StartPosition, EndPosition, height, true);
	public Basis TransformBasis => endTarget == null ? GlobalBasis : Basis.Identity;
	private PlayerController Player => StageSettings.Player;
	private Vector3 StartPosition => Engine.IsEditorHint() ? GlobalPosition : startPosition;
	private Vector3 EndPosition => globalEndPosition ? endPosition : StartPosition + (TransformBasis * endPosition);

	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;

		Root = GetNodeOrNull<Node3D>(root);
		Animator = GetNodeOrNull<AnimationPlayer>(animator);
		InitializeSpawnData();

		currentTimeScale = timeScale;

		if (!disableRespawning)
			StageSettings.Instance.Respawned += Respawn;
	}

	public void InitializeSpawnData() => spawnData = new(GetParent(), Transform);

	public void Respawn()
	{
		tankList.Clear();
		travelTime = 0;
		IsFalling = false;
		IsDetonated = false;
		IsTravelling = false;
		AllowDoubleLaunch = false;

		Root.Rotation = Vector3.Zero;
		Position = spawnData.spawnTransform.Origin;
		Animator.Play("RESET");
		Animator.Advance(0.0);

		BonusManager.instance.UnregisterEnemyComboExtender(this);
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint()) return;

		if (IsDetonated) return;
		CheckInteraction();

		if (IsFalling)
		{
			ProcessFalling();
			return;
		}

		if (!IsTravelling) return;

		LaunchSettings launchSettings = GetLaunchSettings();
		if (launchSettings.IsLauncherFinished(travelTime))
		{
			Detonate();
			return;
		}

		travelTime = Mathf.MoveToward(travelTime, launchSettings.TotalTravelTime, PhysicsManager.physicsDelta * currentTimeScale);
		GlobalPosition = launchSettings.InterpolatePositionTime(travelTime);
		Root.Rotation += Vector3.Forward * VisualRotationSpeed * PhysicsManager.physicsDelta * currentTimeScale;
	}

	private bool CheckInteraction()
	{
		if (!isInteractingWithPlayer || Player.AttackState == PlayerController.AttackStates.None)
			return false;

		if (Player.AttackState == PlayerController.AttackStates.OneShot)
		{
			Detonate(); // Detonate instantly
			return false;
		}

		StrikeTank();
		return true;
	}

	private void StrikeTank()
	{
		currentTimeScale = StrikeTimeScale;

		Player.StartBounce(BounceState.SnapMode.SnappingEnabledNoHeight, 1f, this);
		Animator.Play("strike");
		Animator.Advance(0);
		EmitSignal(SignalName.OnStrike);

		BonusManager.instance.RegisterEnemyComboExtender(this);
		Launch();
	}

	public void Launch()
	{
		if (IsTravelling && !AllowDoubleLaunch) // Already traveling
			return;

		AllowDoubleLaunch = false;

		if (endTarget != null)
			endPosition = endTarget.GlobalPosition - GlobalPosition;

		GD.Print($"Launching {Name}");

		travelTime = 0;
		IsFalling = false;
		IsTravelling = true;
		Animator.Play("launch");
		startPosition = GlobalPosition;
	}

	/// <summary> Causes a gas tank to fall straight down if it is inactive. </summary>
	private void Fall()
	{
		if (IsTravelling || IsDetonated)
			return;

		IsFalling = true;
		fallingVelocity = 0;
		Animator.Play("launch");
	}

	private float fallingVelocity;
	private void ProcessFalling()
	{
		fallingVelocity = Mathf.MoveToward(fallingVelocity, Runtime.MaxGravity, Runtime.Gravity * PhysicsManager.physicsDelta);
		GlobalPosition += Vector3.Up * fallingVelocity * PhysicsManager.physicsDelta;
	}

	private void Detonate()
	{
		GD.Print($"Detonating {Name}");

		IsDetonated = true;
		IsTravelling = false;
		Animator.Play("detonate");
		Animator.CallDeferred("advance", 0.0);
		Player.Camera.StartCameraShake(new()
		{
			origin = GlobalPosition,
			maximumDistance = 20f,
		});

		for (int i = 0; i < enemyList.Count; i++)
			enemyList[i].TakeDamage(); // Damage all enemies in range

		for (int i = 0; i < tankList.Count; i++)
		{
			tankList[i].Launch(); // Launch all gas tanks in range
			if (BonusManager.instance.IsEnemyComboExtenderRegistered(this))
				BonusManager.instance.RegisterEnemyComboExtender(tankList[i]);
		}
		BonusManager.instance.UnregisterEnemyComboExtender(this);

		if (isPlayerInExplosion && !Player.Skills.IsSpeedBreakActive)
			Player.StartKnockback();
	}

	private void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player"))
			return;

		isInteractingWithPlayer = true;
	}

	private void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player"))
			return;

		isInteractingWithPlayer = false;
	}

	private void OnBodyEntered(Node3D b)
	{
		if (b.IsInGroup("floor") && (IsTravelling || IsFalling))
			Detonate();
	}

	private void OnExplosionEntered(Area3D a)
	{
		if (a.IsInGroup("player"))
		{
			isPlayerInExplosion = true;
			return;
		}

		if (a is EnemyHurtbox)
		{
			Enemy targetEnemy = (a as EnemyHurtbox).enemy;
			if (!enemyList.Contains(targetEnemy))
				enemyList.Add(targetEnemy);
		}

		if (a is GasTank && !disableGasTankInteractions)
		{
			GasTank tank = a as GasTank;
			if (a != this && !tankList.Contains(tank))
				tankList.Add(tank);
		}
	}

	private void OnExplosionExited(Area3D a)
	{
		if (a.IsInGroup("player"))
		{
			isPlayerInExplosion = false;
			return;
		}

		if (a is EnemyHurtbox)
		{
			Enemy targetEnemy = (a as EnemyHurtbox).enemy;
			enemyList.Remove(targetEnemy);
		}

		if (a is GasTank && !disableGasTankInteractions)
		{
			GasTank tank = a as GasTank;
			if (a != this)
				tankList.Remove(tank);
		}
	}
}