using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Most common enemy type in Secret Rings.
	/// </summary>
	[Tool]
	public class Majin : Enemy
	{
		[Export]
		public NodePath animationTree;
		private AnimationTree _animationTree;
		[Export]
		public NodePath hurtbox;
		private CollisionShape _hurtbox;
		[Export]
		public NodePath lockonArea;
		private CollisionShape _lockonArea;

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
		public bool isFireMajin;

		[Export]
		public Vector3 spawnOffset; //Where to come from (Based on local position in the editor)
		private Vector3 targetPosition;

		protected override void SetUp()
		{
			if (Engine.EditorHint) return; //In Editor

			targetPosition = GlobalTranslation;
			StageSettings.instance.RegisterRespawnableObject(this);

			_animationTree = GetNode<AnimationTree>(animationTree);
			_animationTree.Active = true;

			_hurtbox = GetNode<CollisionShape>(hurtbox);
			_hurtbox.Disabled = true;

			_lockonArea = GetNode<CollisionShape>(lockonArea);

			base.SetUp();
			Respawn();
		}

		public override void Respawn()
		{
			base.Respawn();

			if (isFireMajin) //Fire majins take 2 hits
				currentHealth = 2;

			//No activation trigger. Activate immediately.
			if (spawnInstantly)
				Activate();
			else
				Despawn(); //Start despawned
		}

		protected override void Defeat()
		{
			base.Defeat();

			if (!launchDirection.IsEqualApprox(Vector3.Zero))
			{
				//Get knocked back
				SceneTreeTween tween = CreateTween().SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
				tween.TweenProperty(this, "global_transform:origin", GlobalTranslation + launchDirection, .5f);
				tween.TweenCallback(this, nameof(Despawn)).SetDelay(.5f);
			}
			else
				Despawn();

		}

		protected override void ProcessEnemy()
		{
			//Rotate to face player
		}

		protected override void Interact()
		{
			if (Character.IsAttacking)
			{
				TakeDamage();
				Character.Lockon.StartBounce();
			}
			else
				Character.TakeDamage(this);
		}

		private void Activate()
		{
			if(!spawnInstantly)
			{
				if (spawnOffset.IsEqualApprox(Vector3.Zero)) //Spawn in
				{
					//TODO Play vfx
					_animationTree.Set("parameters/teleport/active", true);
					_animationTree.Set("parameters/idle_seek/seek_position", 0f);
				}
			}

			_hurtbox.Disabled = false;
			_lockonArea.Disabled = false;
		}

		//Overload activation method for using godot's built-in area trigger
		private void Activate(Area _) => Activate();
	}
}
