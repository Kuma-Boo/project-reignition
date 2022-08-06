using Godot;

namespace Project.Gameplay.Triggers
{
	public class PlatformTrigger : StageTriggerModule //Trigger that carries player on moving platforms
	{
		[Export]
		public float calculationOffset;

		public override void _Ready()
		{
			SetProcess(false); //Sleep
		}

		public override void _Process(float _)
		{
			float targetYPosition = GlobalTranslation.y + calculationOffset;
			if (Character.GlobalTranslation.y < targetYPosition)
				Character.GlobalTranslate(Vector3.Up * (targetYPosition - Character.GlobalTranslation.y));
		}

		public override void Activate()
		{
			SetProcess(true);
		}

		public override void Deactivate(bool _)
		{
			SetProcess(false);
		}
	}
}
