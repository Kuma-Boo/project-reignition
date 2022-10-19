using Godot;
using Project.Core;
using Project.Gameplay.Objects;

namespace Project.Gameplay.Bosses
{
	/// <summary> Travels in an arc and lands on the target. </summary>
	public partial class Missile : Area3D
	{
		[Export]
		public NodePath model;
		private Node3D _model;
		[Export]
		public NodePath trailParticle;
		private GPUParticles3D _trailParticle;
		[Export]
		public NodePath[] impactParticle;
		private GPUParticles3D[] _impactParticle;
		[Export(PropertyHint.Layers3dPhysics)]
		public uint environmentMask;

		public bool IsActive { get; private set; } //Is the missile currently traveling?
		private float travelInterpolation;

		private LaunchData launchData { get; set; }

		public void SetUp()
		{
			_model = GetNode<Node3D>(model);
			_trailParticle = GetNode<GPUParticles3D>(trailParticle);
			_impactParticle = new GPUParticles3D[impactParticle.Length];
			for (int i = 0; i < impactParticle.Length; i++)
				_impactParticle[i] = GetNode<GPUParticles3D>(impactParticle[i]);
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
			GD.Print(travelInterpolation >= launchData.TotalTravelTime);

			//Cast a ray
			if (travelInterpolation >= launchData.TotalTravelTime)
			{
				_trailParticle.Emitting = false;
				for (int i = 0; i < _impactParticle.Length; i++) //Impact effect
					_impactParticle[i].Restart();

				IsActive = false;
				_model.Visible = false;
			}
		}

		public void Launch(LaunchData data)
		{
			IsActive = true;
			travelInterpolation = 0;

			launchData = data;
			_trailParticle.Emitting = true;
			_model.Visible = true;

			UpdatePosition();
		}

		public void Collision()
		{
			_trailParticle.Emitting = false; //Stop emitting

			//Play impact explosion
			for (int i = 0; i < _impactParticle.Length; i++)
				_impactParticle[i].Restart();
		}

		public void OnAreaEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			CharacterController.instance.TakeDamage(this);
		}
	}
}
