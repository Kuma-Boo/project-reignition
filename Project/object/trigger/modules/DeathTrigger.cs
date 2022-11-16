using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Instantly kills the player.
	/// </summary>
	public partial class DeathTrigger : StageTriggerModule
	{
		[Export]
		public TriggerType triggerType;
		public enum TriggerType
		{
			Height,
			Water,
		}

		public override void Activate()
		{
			//TODO Play VFX
			Character.Kill();
			Character.Sound.PlayVoice("fall");

			if (triggerType == TriggerType.Water)
				Character.Sound.PlayActionSFX("splash");
		}
	}
}