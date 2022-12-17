using Godot;

namespace Project.Gameplay
{
	/// <summary>
	/// Most common enemy type in Secret Rings.
	/// </summary>
	[Tool]
	public partial class Majin : Enemy
	{
		[Export]
		private AnimationTree animationTree;

		[Export]
		private bool spawnInstantly;
		[Export]
		private Vector3 launchDirection; //For the enemy to be launched a particular direction when defeated?

		[Export]
		private AttackType attackType;
		private enum AttackType
		{
			None,
			Spin,
			FireRotating,
			FireStraight
		}

		[Export]
		public Vector3 spawnOffset; //Where to come from (Based on local position in the editor)
		private Vector3 targetPosition;

		protected override void SetUp()
		{
			if (Engine.IsEditorHint()) return; //In Editor

			base.SetUp();

			targetPosition = GlobalPosition;
			StageSettings.instance.RegisterRespawnableObject(this);

			animationTree.Active = true;

			Respawn();
		}

		public override void Respawn()
		{
			base.Respawn();

			//No activation trigger. Activate immediately.
			if (spawnInstantly)
				Activate();
			else
				Deactivate();
		}

		protected override void Defeat()
		{
			base.Defeat();

			if (!launchDirection.IsEqualApprox(Vector3.Zero))
			{
				//Get knocked back
				Tween tween = CreateTween().SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
				tween.TweenProperty(this, "global_transform:origin", GlobalPosition + launchDirection, .5f);
				tween.TweenCallback(new Callable(this, MethodName.Despawn)).SetDelay(.5f);
			}
			else
				Despawn();
		}

		protected override void UpdateEnemy()
		{
			//Rotate to face player
		}

		protected override void Activate()
		{
			if (!spawnInstantly)
			{
				if (spawnOffset.IsEqualApprox(Vector3.Zero)) //Spawn in
				{
					//TODO Play vfx
					animationTree.Set("parameters/teleport/active", true);
					animationTree.Set("parameters/idle_seek/seek_position", 0f);
				}

				hurtbox.Monitorable = hurtbox.Monitoring = true;
			}
		}

		protected override void Deactivate()
		{
			Visible = false;
			collider.Disabled = true;
			hurtbox.Monitorable = hurtbox.Monitoring = false;
		}

		//Overload activation method for using godot's built-in area trigger
		private void Activate(Area3D _) => Activate();
	}
}
