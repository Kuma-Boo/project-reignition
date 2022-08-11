using Godot;

namespace Project.Gameplay.Objects
{
    /// <summary>
    /// Shatters after a certain number of enemies are defeated. Connect target enemies with Signals.
    /// </summary>
    public class EnemyStone : RespawnableObject
    {
        [Export]
        public int enemyCount;
        private int currentEnemyCount;
		[Export]
		public NodePath effectMesh;
		private MeshInstance _effectMesh;
		[Export]
		public Material effectMaterial;
		[Export]
		public NodePath shatterNode;
		private DestructableObject _shatterNode;
		protected override bool IsRespawnable() => true; //Set to True for enemies

		protected override void SetUp()
		{
			base.SetUp();
			_effectMesh = GetNode<MeshInstance>(effectMesh);
			_effectMesh.MaterialOverride = effectMaterial;
			_shatterNode = GetNode<DestructableObject>(shatterNode);
		}

		public override void Respawn()
		{
			base.Respawn();
            currentEnemyCount = 0;
		}

		public override void Despawn()
		{
			if (currentEnemyCount < enemyCount) //Player respawned during animation
				return;

			base.Despawn();
		}

		private void IncrementCounter()
		{
			currentEnemyCount++;
			if(currentEnemyCount >= enemyCount)
			{
				//Shatter
				SceneTreeTween tween = CreateTween();
				tween.TweenProperty(effectMaterial, "albedo_color", Colors.White, .2f);
				tween.TweenProperty(effectMaterial, "albedo_color", Colors.Transparent, .2f);
				tween.TweenCallback(_shatterNode, nameof(_shatterNode.Shatter));
				tween.TweenCallback(this, nameof(Despawn)).SetDelay(5f);
			}
		}
	}
}
