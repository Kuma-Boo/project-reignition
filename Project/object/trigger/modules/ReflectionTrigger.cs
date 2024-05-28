using Godot;

namespace Project.Gameplay.Triggers
{
	public partial class ReflectionTrigger : StageTriggerModule
	{
		// Store previous data for deactivation.
		private Vector3 previousPosition;
		private Path3D previousReflectionSyncPath;

		public override void Activate()
		{
			// Cache data
			previousPosition = PlanarReflectionRenderer.instance.GlobalPosition;

			// Move to new position
			PlanarReflectionRenderer.instance.GlobalPosition = GlobalPosition;
		}

		public override void Deactivate() => PlanarReflectionRenderer.instance.GlobalPosition = previousPosition;
	}
}
