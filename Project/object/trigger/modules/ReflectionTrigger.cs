using Godot;

namespace Project.Gameplay.Triggers
{
	public partial class ReflectionTrigger : StageTriggerModule
	{
		[Export(PropertyHint.NodeType, "Path3D")]
		private NodePath reflectionSyncNodePath;
		private Path3D reflectionSyncPath;

		// Store previous data for deactivation.
		private Vector3 previousPosition;
		private Path3D previousReflectionSyncPath;

		public override void _Ready() => reflectionSyncPath = GetNodeOrNull<Path3D>(reflectionSyncNodePath);

		public override void Activate()
		{
			// Cache data
			previousPosition = PlanarReflectionRenderer.instance.GlobalPosition;
			previousReflectionSyncPath = PlanarReflectionRenderer.instance.ReflectionSyncPath;

			if (!IsInstanceValid(reflectionSyncPath))
				PlanarReflectionRenderer.instance.GlobalPosition = GlobalPosition;
			PlanarReflectionRenderer.instance.ReflectionSyncPath = reflectionSyncPath; // Update reflection sync path
		}

		public override void Deactivate()
		{
			PlanarReflectionRenderer.instance.GlobalPosition = previousPosition;
			PlanarReflectionRenderer.instance.ReflectionSyncPath = previousReflectionSyncPath;
		}
	}
}
