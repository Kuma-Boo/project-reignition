using Godot;

namespace Project.Gameplay
{
	/*
	Most one ways should be separate objects that get enabled/disabled at the correct time, using OneWayColliderTrigger,
	but some collision shapes are difficult to place in godot's 3d editor, so exporting a new shape from blender and 
	using the player's one way collision mode may be easier.
	*/
	public class LoadTrigger : StageObject
	{
		[Export]
		public bool startUnloaded;
		[Export]
		public LoadMode loadMode;
		public enum LoadMode
		{
			Load,
			Unload,
		}
		[Export]
		public NodePath target;
		private Spatial _target;
		private SpawnData objectSpawnData;

		public override void SetUp()
		{
			_target = GetNode<Spatial>(target);
			objectSpawnData.UpdateSpawnData(_target);

			if (startUnloaded)
				_target.GetParent().CallDeferred("remove_child", _target);
		}

		public override void OnEnter()
		{
			if (loadMode == LoadMode.Load && !_target.IsInsideTree())
			{
				objectSpawnData.parentNode.AddChild(_target);
				_target.GlobalTransform = objectSpawnData.spawnTransform;
			}
			else if (loadMode == LoadMode.Unload && _target.IsInsideTree())
				_target.GetParent().CallDeferred("remove_child", _target);
		}
		public override bool IsRespawnable() => false;
	}
}
