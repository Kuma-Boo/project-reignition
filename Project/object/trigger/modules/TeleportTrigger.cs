using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Teleports the player to the StageTriggerModule's position.
	/// </summary>
	public partial class TeleportTrigger : StageTriggerModule
	{
		[Export]
		public bool enableFX;
		[Export]
		public bool resetMovement;
		[Export]
		public bool crossfade = true;

		public override void Activate() => Character.Teleport(GlobalPosition, new TeleportSettings()
		{
			enableFX = enableFX,
			resetMovement = resetMovement,
			crossfade = crossfade
		});
	}
}

namespace Project.Gameplay
{
	public struct TeleportSettings
	{
		/// <summary> Should sound/visual effects be used? </summary>
		public bool enableFX;
		/// <summary> Reset movespeed? </summary>
		public bool resetMovement;
		/// <summary> Use a crossfade? </summary>
		public bool crossfade;

		/// <summary>
		/// Teleport settings used when respawning the player.
		/// </summary>
		public static TeleportSettings RespawnSettings => new TeleportSettings()
		{
			enableFX = true,
			resetMovement = true,
			crossfade = false,
		};
	}
}