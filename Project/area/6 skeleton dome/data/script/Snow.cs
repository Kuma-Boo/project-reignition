using Godot;

// Controls the "snow" inside of Skeleton Dome (Might be modified later to handle all precipitation)
namespace Project.Gameplay.Objects
{
	public partial class Snow : GpuParticles3D
	{
		public override void _Ready()
		{
			// Reparent to camera
			GetParent().RemoveChild(this);
			StageSettings.Player.Camera.AddChild(this);
			Restart();
			Preprocess = 0.0f;
		}

		public void Activate() => Emitting = true;
		public void Deactivate() => Emitting = false;
	}
}
