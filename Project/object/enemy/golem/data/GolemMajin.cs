using Godot;
using Project.Core;

namespace Project.Gameplay
{
	public partial class GolemMajin : Enemy
	{
		[Export]
		private PathFollow3D pathFollower;
		private float startingProgress;

		private Vector3 velocity;
		private const float RotationResetSpeed = 5f;
		private const float WalkSpeed = 2f;

		private readonly StringName StateTransition = "parameters/state_transition/transition_request";
		private readonly StringName DefeatTransition = "parameters/defeat_transition/transition_request";

		private readonly StringName EnabledConstant = "enabled";
		private readonly StringName DisabledConstant = "disabled";

		protected override void SetUp()
		{
			if (pathFollower != null)
				startingProgress = pathFollower.Progress;
			AnimationTree.Active = true;
			base.SetUp();
		}

		public override void Respawn()
		{
			if (pathFollower != null)
				pathFollower.Progress = startingProgress;
			AnimationTree.Set(StateTransition, "idle");
			base.Respawn();
		}

		protected override void EnterRange()
		{
			IsActive = true;
			AnimationTree.Set(StateTransition, "walk");
		}

		protected override void Defeat()
		{
			base.Defeat();
			AnimationTree.Set(StateTransition, "defeat");
		}

		protected override void UpdateEnemy()
		{
			if (!IsActive) return;
			if (pathFollower == null) return;

			if (IsDefeated)
			{
				pathFollower.Rotation = pathFollower.Rotation.Lerp(Vector3.Zero, RotationResetSpeed * PhysicsManager.physicsDelta);
				return;
			}

			pathFollower.Progress += WalkSpeed * PhysicsManager.physicsDelta;
		}
	}
}
