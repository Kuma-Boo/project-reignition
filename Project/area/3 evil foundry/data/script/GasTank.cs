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

	[ExportGroup("Components")]
	[Export(PropertyHint.NodePathValidTypes, "Node3D")]
	private NodePath root;
	private Node3D Root { get; set; }

	[Export(PropertyHint.NodePathValidTypes, "AnimationPlayer")]
	private NodePath animator;
	private AnimationPlayer Animator { get; set; }

	private bool isInteractingWithPlayer;
	private bool isPlayerInExplosion;
	private SpawnData spawnData;
	private readonly List<Enemy> enemyList = [];
	private readonly List<GasTank> tankList = [];

	public bool IsFalling { get; private set; }
	public bool IsDetonated { get; private set; }
	public bool IsTravelling { get; private set; }
	private float travelTime;
	private readonly float VisualRotationSpeed = 10f;
	private readonly float TimeScale = .8f;

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

		if (!disableRespawning)
			StageSettings.Instance.ConnectRespawnSignal(this);
	}

	public void InitializeSpawnData() => spawnData = new SpawnData(GetParent(), Transform);

	private void Respawn()
	{
		Root.Rotation = Vector3.Zero;
		travelTime = 0;
		IsFalling = false;
		IsTravelling = false;
		Position = spawnData.spawnTransform.Origin;
		IsDetonated = false;
		Animator.Play("RESET");
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

		travelTime = Mathf.MoveToward(travelTime, launchSettings.TotalTravelTime, PhysicsManager.physicsDelta * TimeScale);
		GlobalPosition = launchSettings.InterpolatePositionTime(travelTime);
		Root.Rotation += Vector3.Forward * VisualRotationSpeed * PhysicsManager.physicsDelta * TimeScale;
	}

	private bool CheckInteraction()
	{
		if (!isInteractingWithPlayer) return false;

		// TODO Check for stomp
		if (Player.Skills.IsSpeedBreakActive)
		{
			Detonate(); // Detonate instantly
			return false;
		}

		if (!Player.IsJumpDashOrHomingAttack) return false;

		Player.StartBounce();

		StrikeTank();
		return true;
	}

	private void StrikeTank()
	{
		Player.StartBounce();
		Animator.Play("strike");
		Animator.Advance(0);
		EmitSignal(SignalName.OnStrike);

		Launch();
	}

	public void Launch()
	{
		if (endTarget != null)
			endPosition = endTarget.GlobalPosition - GlobalPosition;

		travelTime = 0;
		IsFalling = false;
		IsTravelling = true;
		Animator.Play("launch");
		startPosition = GlobalPosition;
	}

	/// <summary> Causes a gas tank to fall straight down if it is inactive. </summary>
	private void Fall()
	{
		if (IsTravelling)
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
		IsDetonated = true;
		IsTravelling = false;
		Animator.Play("detonate");

		for (int i = 0; i < enemyList.Count; i++)
			enemyList[i].TakeDamage(); // Damage all enemies in range

		for (int i = 0; i < tankList.Count; i++)
			tankList[i].Launch(); // Launch all gas tanks in range

		if (isPlayerInExplosion)
			Player.StartKnockback();
	}

	private void OnEntered(Area3D a)
	{
		if (a.IsInGroup("player"))
			isInteractingWithPlayer = true;
	}

	private void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player")) return;
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

		if (a is GasTank)
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

		if (a is GasTank)
		{
			GasTank tank = a as GasTank;
			if (a != this)
				tankList.Remove(tank);
		}
	}
}