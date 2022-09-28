using Godot;

namespace Project.Gameplay.Objects
{
	public partial class Pickup : RespawnableObject
	{
		[Signal]
		public delegate void CollectedEventHandler();
		protected override bool IsRespawnable() => true;

		public void OnEnter() => CallDeferred(nameof(Collect));

		protected virtual void Collect()
		{
			EmitSignal(SignalName.Collected);
		}
	}
}
