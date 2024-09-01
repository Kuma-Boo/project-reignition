using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Gameplay.Objects
{
	[Tool]
	public partial class GasTank : Area3D
	{

		[Export]
		private float height;
		[Export]
		private Vector3 endPosition;
		private Vector3 startPosition;

		[Export]
		private AnimationPlayer animator;

		private bool isInteractingWithPlayer;
		private bool isPlayerInExplosion;
		private readonly List<Enemy> enemyList = new();

		private bool wasDetonated;
		private bool isTraveling;
		private float travelTime;
		private const float TIME_SCALE = .8f;

		private PlayerController Player => StageSettings.Player;
		private Vector3 StartPosition => Engine.IsEditorHint() ? GlobalPosition : startPosition;
		private Vector3 EndPosition => StartPosition + GlobalBasis * endPosition;
		public LaunchSettings GetLaunchSettings() => LaunchSettings.Create(StartPosition, EndPosition, height);


		public override void _Ready()
		{
			if (Engine.IsEditorHint()) return;

			startPosition = GlobalPosition;
			StageSettings.instance.ConnectRespawnSignal(this);
		}


		private void Respawn()
		{
			travelTime = 0;
			isTraveling = false;
			GlobalPosition = StartPosition;
			wasDetonated = false;
			animator.Play("RESET");
		}


		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint()) return;

			if (wasDetonated) return;
			if (!isTraveling && !CheckInteraction()) return;

			LaunchSettings launchSettings = GetLaunchSettings();

			if (launchSettings.IsLauncherFinished(travelTime))
			{
				if (!wasDetonated)
					Detonate();
				return;
			}

			travelTime = Mathf.MoveToward(travelTime, launchSettings.TotalTravelTime, PhysicsManager.physicsDelta * TIME_SCALE);
			GlobalPosition = launchSettings.InterpolatePositionTime(travelTime);
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

			/*
			REFACTOR TODO
			if (Player.ActionState != PlayerController.ActionStates.JumpDash) return false;
			*/

			Player.StartBounce();

			isTraveling = true;
			animator.Play("strike");
			return true;
		}


		private void Detonate()
		{
			wasDetonated = true;
			isTraveling = false;
			animator.Play("detonate");

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
				if (enemyList.Contains(targetEnemy))
					enemyList.Remove(targetEnemy);
			}
		}
	}
}
