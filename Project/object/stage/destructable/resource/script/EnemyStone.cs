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
		public NodePath effectMesh;
		private MeshInstance3D _effectMesh;
		[Export]
		public Material effectMaterial;
		[Export]
		public NodePath shatterNode;
		private DestructableObject _shatterNode;

		private StageSettings.SpawnData spawnData;

		public override void _Ready()
		{
			spawnData = new StageSettings.SpawnData(GetParent(), Transform);
			StageSettings.instance.RegisterRespawnableObject(this);

			_effectMesh = GetNode<MeshInstance3D>(effectMesh);
			_effectMesh.MaterialOverride = effectMaterial;
			_shatterNode = GetNode<DestructableObject>(shatterNode);
		}

		public void Respawn()
		{
			if (!IsInsideTree() && GetParent() != spawnData.parentNode)
				spawnData.parentNode.AddChild(this);
			Transform = spawnData.spawnTransform;

			currentEnemyCount = 0;
		}

		public void Despawn()
		{
			if (!IsInsideTree() || currentEnemyCount < enemyCount) //Player respawned during animation
				return;

			GetParent().CallDeferred("remove_child", this);
		}

		private void IncrementCounter()
		{
			currentEnemyCount++;
			if (currentEnemyCount >= enemyCount)
			{
				//Shatter
				Tween tween = CreateTween();
				tween.TweenProperty(effectMaterial, "albedo_color", Colors.White, .2f);
				tween.TweenProperty(effectMaterial, "albedo_color", Colors.Transparent, .2f);
				tween.TweenCallback(new Callable(_shatterNode, nameof(_shatterNode.Shatter)));
				tween.TweenCallback(new Callable(this, MethodName.Despawn)).SetDelay(5f);
			}
		}
	}
}
