using Godot;
using Godot.Collections;

namespace Project
{
	/// <summary>
	/// Plays a group of GPUParticles together.
	/// </summary>
	public partial class ParticleGroup : GpuParticles3D
	{
		[Export]
		public Array<GpuParticles3D> subSystems;

		public bool IsActive { get; private set; }

		public void StartParticles()
		{
			for (int i = 0; i < subSystems.Count; i++)
				subSystems[i].Restart();

			Restart();
			IsActive = true;
		}

		public override void _PhysicsProcess(double delta)
		{
			base._PhysicsProcess(delta);

			if (IsActive) return;

			for (int i = 0; i < subSystems.Count; i++)
			{
				// Still active
				if (subSystems[i].Emitting) return;
			}

			IsActive = false;
		}
	}
}
