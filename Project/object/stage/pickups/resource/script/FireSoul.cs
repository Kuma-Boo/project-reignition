using Godot;

namespace Project.Gameplay
{
	public class FireSoul : StageObject
	{
		[Export]
		public Material collectedMaterial;
		[Export]
		public NodePath mesh;
		private MeshInstance _mesh;

		[Export]
		public bool isCollected; //Determined by save data

		public override bool IsRespawnable() => true;
		public override void SetUp()
		{
			base.SetUp();

			_mesh = GetNode<MeshInstance>(mesh);

			if (isCollected)
				_mesh.MaterialOverride = collectedMaterial;
		}

		public override void OnEnter()
		{
			//ActiveCharacter.AddScore(1000);
			Despawn();
		}
	}
}
