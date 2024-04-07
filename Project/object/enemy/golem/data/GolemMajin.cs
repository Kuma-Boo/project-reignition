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
		private const float ROTATION_RESET_SPEED = 5f;
		private const float WALK_SPEED = 2f;

		private readonly StringName STATE_TRANSITION = "parameters/state_transition/transition_request";
		private readonly StringName DEFEAT_TRANSITION = "parameters/defeat_transition/transition_request";

		private readonly StringName ENABLED_CONSTANT = "enabled";
		private readonly StringName DISABLED_CONSTANT = "disabled";

		protected override void SetUp()
		{
			if (pathFollower != null)
				startingProgress = pathFollower.Progress;
			animationTree.Active = true;
			base.SetUp();
		}


		public override void Respawn()
		{
			if (pathFollower != null)
				pathFollower.Progress = startingProgress;
			animationTree.Set(STATE_TRANSITION, "idle");
			base.Respawn();
		}

		protected override void EnterRange()
		{
			IsActive = true;
			animationTree.Set(STATE_TRANSITION, "walk");
		}

		protected override void Defeat()
		{
			base.Defeat();
			animationTree.Set(STATE_TRANSITION, "defeat");
		}


		protected override void UpdateEnemy()
		{
			if (!IsActive) return;
			if (pathFollower == null) return;

			if (IsDefeated)
			{
				pathFollower.Rotation = pathFollower.Rotation.Lerp(Vector3.Zero, ROTATION_RESET_SPEED * PhysicsManager.physicsDelta);
				return;
			}

			pathFollower.Progress += WALK_SPEED * PhysicsManager.physicsDelta;
		}
	}
}
