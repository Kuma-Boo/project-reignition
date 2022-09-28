using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Moves player with moving platforms.
	/// </summary>
	public partial class PlatformTrigger : StageTriggerModule
	{
		[Export]
		public float calculationOffset;

		//Sleep
		public override void _Ready() => SetProcess(false);

		public override void _Process(double _)
		{
			float targetYPosition = GlobalPosition.y + calculationOffset;
			if (Character.GlobalPosition.y < targetYPosition)
				Character.GlobalTranslate(Vector3.Up * (targetYPosition - Character.GlobalPosition.y));
		}

		public override void Activate() => SetProcess(true); //Start Processing
		public override void Deactivate() => SetProcess(false); //Stop Processing
	}
}
