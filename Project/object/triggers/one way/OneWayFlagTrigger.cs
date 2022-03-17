using Godot;

namespace Project.Gameplay
{
	/*
	Most one ways should be basic objects that get enabled/disabled at the correct time, using LoadTrigger,
	but some collision shapes are difficult to place in godot's 3d editor, so exporting a new shape from blender and 
	using the player's one way collision mode may be easier.
	*/
	public class OneWayFlagTrigger : StageObject
	{
		[Export]
		public CharacterController.OneWayCollisionMode targetCollisionMode;
		public override void OnEnter() => Character.oneWayCollisionMode = targetCollisionMode;
		public override bool IsRespawnable() => false;
	}
}
