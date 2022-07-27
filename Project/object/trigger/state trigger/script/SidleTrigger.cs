using Godot;

namespace Project.Gameplay.Triggers
{
	public class SidleTrigger : Area
	{
		private CharacterController Character => CharacterController.instance;

		public void OnEntered(Area a)
		{
			if (!a.IsInGroup("player")) return;

			//Apply state
			Character.StartSidle();
		}

		public void OnExited(Area a)
		{
			if (!a.IsInGroup("player")) return;

			Path activePath = CharacterController.instance.ActivePath;
			Curve3D pathCurve = activePath.Curve;
			float characterOffset = pathCurve.GetClosestOffset(CharacterController.instance.GlobalTranslation - activePath.GlobalTranslation);
			float triggerOffset = pathCurve.GetClosestOffset(GlobalTranslation - activePath.GlobalTranslation);

			bool isMovingForward = Mathf.Sign(characterOffset - triggerOffset) > 0;
			if (isMovingForward) return; //Keep state change

			Character.StopSidle();
		}
	}
}
