using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Moves player with moving platforms.
	/// </summary>
	public class PlatformTrigger : StageTriggerModule
	{
		[Export]
		public float calculationOffset;

		//Sleep
		public override void _Ready() => SetProcess(false);

		public override void _Process(float _)
		{
			float targetYPosition = GlobalTranslation.y + calculationOffset;
			if (Character.GlobalTranslation.y < targetYPosition)
				Character.GlobalTranslate(Vector3.Up * (targetYPosition - Character.GlobalTranslation.y));
		}

		public override void Activate() => SetProcess(true); //Start Processing
		public override void Deactivate() => SetProcess(false); //Stop Processing
	}
}
