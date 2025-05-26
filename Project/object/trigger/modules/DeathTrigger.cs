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

		[Export] private float waterFxHeightOffset;

		public override void Activate()
		{
			if (!StageSettings.Instance.IsLevelIngame)
				return;

			Player.Camera.IsDefeatFreezeActive = true;
			Player.StartRespawn();
			Player.Effect.PlayVoice("fall");

			if (triggerType == TriggerType.Water)
				Player.Effect.PlayLandingWaterFX(waterFxHeightOffset);
		}
	}
}