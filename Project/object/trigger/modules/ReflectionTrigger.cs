using Godot;

namespace Project.Gameplay.Triggers
{
	public partial class ReflectionTrigger : StageTriggerModule
	{
		[Export] private PlanarReflectionRenderer reflectionRenderer;

		// Store previous data for deactivation.
		private Vector3 previousPosition;

		public override void Activate()
		{
			// Cache data
			previousPosition = reflectionRenderer.GlobalPosition;

			// Move to new position
			reflectionRenderer.GlobalPosition = GlobalPosition;
		}

		public override void Deactivate() => reflectionRenderer.GlobalPosition = previousPosition;
	}
}
