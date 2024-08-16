using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Gameplay.Objects;

[Tool]
public partial class GasTank : Area3D
{
	[Export]
	private float height;
	/// <summary> Field is public so enemies can set this as needed. </summary>
	[Export]
	public Vector3 endPosition;
	private Vector3 startPosition;

	[Export]
	private AnimationPlayer animator;

	private bool isInteractingWithPlayer;
	private bool isPlayerInExplosion;
	private readonly List<Enemy> enemyList = [];

	public bool IsDetonated { get; private set; }
	public bool IsTraveling { get; private set; }
	private float travelTime;
	private const float TimeScale = .8f;

	public LaunchSettings GetLaunchSettings() => LaunchSettings.Create(StartPosition, EndPosition, height);
	private CharacterController Character => CharacterController.instance;
	private Vector3 StartPosition => Engine.IsEditorHint() ? GlobalPosition : startPosition;
	private Vector3 EndPosition => StartPosition + (GlobalBasis * endPosition);

	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;
		StageSettings.instance.ConnectRespawnSignal(this);
	}

	private void Respawn()
	{
		travelTime = 0;
		IsTraveling = false;
		GlobalPosition = StartPosition;
		IsDetonated = false;
		animator.Play("RESET");
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint()) return;

		if (IsDetonated) return;
		if (!IsTraveling && !CheckInteraction()) return;

		LaunchSettings launchSettings = GetLaunchSettings();

		if (launchSettings.IsLauncherFinished(travelTime))
		{
			if (!IsDetonated)
				Detonate();
			return;
		}

		travelTime = Mathf.MoveToward(travelTime, launchSettings.TotalTravelTime, PhysicsManager.physicsDelta * TimeScale);
		GlobalPosition = launchSettings.InterpolatePositionTime(travelTime);
	}

	private bool CheckInteraction()
	{
		if (!isInteractingWithPlayer) return false;

		// TODO Check for stomp
		if (Character.Skills.IsSpeedBreakActive)
		{
			Detonate(); // Detonate instantly
			return false;
		}

		if (Character.ActionState != CharacterController.ActionStates.JumpDash) return false;

		Character.Lockon.StartBounce();
		animator.Play("strike");
		animator.Advance(0);
		Launch();

		return true;
	}

	public void Launch()
	{
		IsTraveling = true;
		animator.Play("launch");
		startPosition = GlobalPosition;
	}

	private void Detonate()
	{
		IsDetonated = true;
		IsTraveling = false;
		animator.Play("detonate");

		for (int i = 0; i < enemyList.Count; i++)
			enemyList[i].TakeDamage(); // Damage all enemies in range

		if (isPlayerInExplosion)
			Character.StartKnockback();
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