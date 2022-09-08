using Godot;
using Project.Core;

namespace Project.Gameplay.Bosses
{
	[Tool]
	public class ErazorChain : Spatial
	{
		[Export]
		public NodePath parent;
		private ErazorChain parentChain;
		[Export]
		public NodePath child;
		private ErazorChain childChain;
		[Export]
		public float chainSize;
		[Export]
		public bool disableSimulation;

		[Export]
		public float gravity;

		public override void _EnterTree()
		{
			GetComponents();
		}

		public override void _PhysicsProcess(float delta)
		{
			//Child chain or simulation is disabled
			if (disableSimulation || parentChain != null) return;
			
			Scale = GetParent<Spatial>().Scale; //Copy rotation from parent

			if (childChain != null)
				childChain.UpdateChain(this, gravity * delta);
		}

		private void UpdateChain(Spatial parent, float gravityAmount)
		{
			Vector3 targetPosition = GlobalTranslation + Vector3.Down * gravityAmount;
			Vector3 delta = targetPosition - parent.GlobalTranslation;

			if (chainSize == 0)
				targetPosition = parent.GlobalTranslation;
			else
			{
				delta = delta.LimitLength(chainSize);
				targetPosition = parent.GlobalTranslation + delta;
			}

			Transform transform = GlobalTransform;
			transform.basis.y = -delta.Normalized();
			//Rotate Chain
			transform.basis.x = parent.Forward();
			transform.basis.z = parent.Right();
			transform.origin = targetPosition;
			transform = transform.Orthonormalized();
			GlobalTransform = transform;
			Scale = -parent.Scale;

			if (childChain != null)
				childChain.UpdateChain(this, gravityAmount);
		}

		private void GetComponents()
		{
			if (parent != null)
				parentChain = GetNodeOrNull<ErazorChain>(parent);

			if (child != null)
				childChain = GetNodeOrNull<ErazorChain>(child);
		}
	}
}
