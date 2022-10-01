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
		public NodePath animationTree;
		private AnimationTree _animationTree;

		[Export]
		public bool spawnInstantly;
		[Export]
		public Vector3 launchDirection; //Direction to be launched in

		[Export]
		public AttackType attackType;
		public enum AttackType
		{
			None,
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

			_animationTree = GetNode<AnimationTree>(animationTree);
			_animationTree.Active = true;

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

		protected override void ProcessEnemy()
		{
			//Rotate to face player
		}

		private void Activate()
		{
			if (!spawnInstantly)
			{
				if (spawnOffset.IsEqualApprox(Vector3.Zero)) //Spawn in
				{
					//TODO Play vfx
					_animationTree.Set("parameters/teleport/active", true);
					_animationTree.Set("parameters/idle_seek/seek_position", 0f);
				}

				_hitbox.Monitorable = _hitbox.Monitoring = true;
			}
		}

		private void Deactivate()
		{
			Visible = false;
			_collider.Disabled = true;
			_hitbox.Monitorable = _hitbox.Monitoring = false;
		}

		//Overload activation method for using godot's built-in area trigger
		private void Activate(Area3D _) => Activate();
	}
}
