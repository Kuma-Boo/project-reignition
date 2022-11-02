using Godot;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Shatters after a certain number of enemies are defeated. Connect target enemies with Signals.
	/// </summary>
	public partial class EnemyStone : Node3D
	{
		[Export]
		public int enemyCount;
		private int currentEnemyCount;
		[Export]
		private AnimationPlayer animator;

		private bool isShattered;
		private SpawnData spawnData;

		public override void _Ready()
		{
			spawnData = new SpawnData(GetParent(), Transform);
			StageSettings.instance.RegisterRespawnableObject(this);
		}

		public void Respawn()
		{
			spawnData.Respawn(this);

			isShattered = false;
			currentEnemyCount = 0;

			animator.Play("RESET");
		}

		public void Despawn()
		{
			if (!IsInsideTree() || currentEnemyCount < enemyCount) //Player respawned during animation
				return;

			GetParent().CallDeferred("remove_child", this);
		}

		private void IncrementCounter()
		{
			if (isShattered) return;

			currentEnemyCount++;
			if (currentEnemyCount >= enemyCount)
			{
				isShattered = true;
				animator.Play("shatter");
			}
		}
	}
}
