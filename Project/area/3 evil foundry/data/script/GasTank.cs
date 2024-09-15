using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Gameplay.Objects;

[Tool]
public partial class GasTank : Area3D
{
	[Signal]
	public delegate void OnStrikeEventHandler();

	/// <summary> Field is public so enemies can set this as needed. </summary>
	[Export]
	public float height;
	/// <summary> Field is public so enemies can set this as needed. </summary>
	[Export]
	public Vector3 endPosition;
	/// <summary> Used if you want to target a particular object instead of a position (End position is recalculated on launch). </summary>
	[Export]
	public Node3D endTarget;
	private Vector3 startPosition;

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

	public bool IsDetonated { get; private set; }
	public bool IsTraveling { get; private set; }
	private float travelTime;
	private readonly float VisualRotationSpeed = 10f;
	private readonly float TimeScale = .8f;

	public LaunchSettings GetLaunchSettings() => LaunchSettings.Create(StartPosition, EndPosition, height, true);
	public Basis TransformBasis => endTarget == null ? GlobalBasis : Basis.Identity;
	private PlayerController Player => StageSettings.Player;
	private Vector3 StartPosition => Engine.IsEditorHint() ? GlobalPosition : startPosition;
	private Vector3 EndPosition => StartPosition + (TransformBasis * endPosition);

	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;

		Root = GetNodeOrNull<Node3D>(root);
		Animator = GetNodeOrNull<AnimationPlayer>(animator);
		InitializeSpawnData();
		StageSettings.Instance.ConnectRespawnSignal(this);
	}

	public void InitializeSpawnData() => spawnData = new SpawnData(GetParent(), Transform);

	private void Respawn()
	{
		Root.Rotation = Vector3.Zero;
		travelTime = 0;
		IsTraveling = false;
		Position = spawnData.spawnTransform.Origin;
		IsDetonated = false;
		Animator.Play("RESET");
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint()) return;

		if (IsDetonated) return;
		CheckInteraction();

		if (!IsTraveling) return;

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
		Character.Lockon.StartBounce();
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
		IsTraveling = true;
		Animator.Play("launch");
		startPosition = GlobalPosition;
	}

	private void Detonate()
	{
		IsDetonated = true;
		IsTraveling = false;
		Animator.Play("detonate");

		for (int i = 0; i < enemyList.Count; i++)
			enemyList[i].TakeDamage(); // Damage all enemies in range

			if (isPlayerInExplosion)
				Player.StartKnockback();
		}


	private void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player")) return;
		isInteractingWithPlayer = true;
	}

	private void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player")) return;
		isInteractingWithPlayer = false;
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
	}
}