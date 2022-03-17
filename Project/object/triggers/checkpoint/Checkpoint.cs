using Godot;

namespace Project.Gameplay
{
	public class Checkpoint : StageObject
	{
		public override bool IsRespawnable() => false;

		//public override void OnEnter() => ActiveCharacter.ChangeCheckpoint(this);
	}
}
