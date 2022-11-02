using Godot;
using Project.Core;
using Project.Gameplay.Objects;

namespace Project.Gameplay.Bosses
{
	/// <summary> Travels in an arc and lands on the target. </summary>
	public partial class Missile : Area3D
	{
		[Export]
		private AnimationPlayer animator;

		public bool IsActive { get; private set; } //Is the missile currently traveling?
		private float travelInterpolation;

		private LaunchData launchData { get; set; }

		public override void _PhysicsProcess(double _)
		{
			if (!IsActive) return;

			UpdatePosition();
		}

		private void UpdatePosition()
		{
			GlobalPosition = launchData.InterpolatePositionTime(travelInterpolation);
			travelInterpolation += PhysicsManager.physicsDelta;

			//Reached the ground
			if (travelInterpolation >= launchData.TotalTravelTime)
			{
				animator.Play("impact"); //Impact effect
				IsActive = false;
			}
		}

		public void Launch(LaunchData data)
		{
			IsActive = true;
			travelInterpolation = 0;

			launchData = data;
			animator.Play("launch");

			UpdatePosition();
		}

		public void OnAreaEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			CharacterController.instance.TakeDamage(this);
		}
	}
}
