using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Teleports the player.
	/// </summary>
	public partial class TeleportTrigger : StageTriggerModule
	{
		[Signal]
		public delegate void TeleportEventHandler();

		[Export]
		/// <summary> Should sound/visual effects be used when starting the teleport? </summary>
		public bool enableStartFX;
		[Export]
		/// <summary> Should sound/visual effects be used when finishing the teleport? </summary>
		public bool enableEndFX = true;
		[Export]
		/// <summary> Reset movespeed? </summary>
		public bool resetMovespeed = true;
		[Export]
		/// <summary> Use a crossfade? </summary>
		public bool crossfade;
		[Export]
		/// <summary> Target to warp to. </summary>
		private Node3D warpTarget;
		public Vector3 WarpPosition => warpTarget == null ? GlobalPosition : warpTarget.GlobalPosition;

		public override void Activate() => Player.Teleport(this);

		/// <summary> Emits signal when warp actually occours. </summary>
		public void ApplyTeleport() => EmitSignal(SignalName.Teleport);
	}
}