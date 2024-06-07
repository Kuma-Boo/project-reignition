using Godot;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Shatters after a certain number of enemies are defeated. Connect target enemies with Signals.
	/// </summary>
	public partial class EnemyStone : DestructableObject
	{
		[Export]
		public int enemyCount;
		private int currentEnemyCount;

		public override void Respawn()
		{
			currentEnemyCount = 0;
			base.Respawn();
		}

		public void IncrementCounter() //Connect this from a signal
		{
			if (isShattered) return;

			currentEnemyCount++;
			if (currentEnemyCount >= enemyCount)
				animator.Play("shatter");
		}
	}
}
