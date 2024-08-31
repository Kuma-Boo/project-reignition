using Godot;
using Project.Core;

namespace Project.Gameplay.Bosses
{
	/// <summary> Travels in an arc and lands on the target. </summary>
	public partial class Missile : Area3D
	{
		[Export]
		private AnimationPlayer animator;

		public bool IsActive { get; private set; } //Is the missile currently traveling?
		private float travelInterpolation;

		private LaunchSettings LaunchSettings { get; set; }

		public override void _PhysicsProcess(double _)
		{
			if (!IsActive) return;

			UpdatePosition();
		}

		private void UpdatePosition()
		{
			GlobalPosition = LaunchSettings.InterpolatePositionTime(travelInterpolation);
			travelInterpolation += PhysicsManager.physicsDelta;

			//Reached the ground
			if (travelInterpolation >= LaunchSettings.TotalTravelTime)
			{
				animator.Play("impact"); //Impact effect
				IsActive = false;
			}
		}

		public void Launch(LaunchSettings settings)
		{
			IsActive = true;
			travelInterpolation = 0;

			LaunchSettings = settings;
			animator.Play("launch");

			UpdatePosition();
		}

		public void OnAreaEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			StageSettings.Player.State.StartKnockback();
		}
	}
}
