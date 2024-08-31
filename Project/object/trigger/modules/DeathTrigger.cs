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
			Player.Camera.IsDefeatFreezeActive = true;
			Player.State.StartRespawn();
			Player.Effect.PlayVoice("fall");

			if (triggerType == TriggerType.Water)
				Player.Effect.PlayLandingWaterFX();
		}
	}
}