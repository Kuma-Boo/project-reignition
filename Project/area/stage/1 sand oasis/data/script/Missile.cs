using Godot;
using Project.Core;
using Project.Gameplay.Objects;

namespace Project.Gameplay.Bosses
{
	/// <summary> Travels in an arc and lands on the target. </summary>
	public partial class Missile : Area3D
	{
		[Export]
		public NodePath animator;
		private AnimationPlayer _animator;

		public bool IsActive { get; private set; } //Is the missile currently traveling?
		private float travelInterpolation;

		private LaunchData launchData { get; set; }

		public void SetUp()
		{
			_animator = GetNode<AnimationPlayer>(animator);
		}

		public override void _PhysicsProcess(double _)
		{
			if (!IsActive) return;

			UpdatePosition();
		}

		private void UpdatePosition()
		{
			GlobalPosition = launchData.InterpolatePosition(travelInterpolation);
			travelInterpolation += PhysicsManager.physicsDelta;

			//Reached the ground
			if (travelInterpolation >= launchData.TotalTravelTime)
			{
				_animator.Play("impact"); //Impact effect
				IsActive = false;
			}
		}

		public void Launch(LaunchData data)
		{
			IsActive = true;
			travelInterpolation = 0;

			launchData = data;
			_animator.Play("launch");

			UpdatePosition();
		}

		public void OnAreaEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			CharacterController.instance.TakeDamage(this);
		}
	}
}
