using Godot;
using Project.Core;

namespace Project.Gameplay
{
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
		public NodePath activationTrigger;
		public Area _activationTrigger;

		[Export]
		public AttackType attackType;
		public enum AttackType
		{
			None,
			FireRotating,
			FireStraight
		}

		[Export]
		public Vector3 spawnOffset; //Where to come from
		private Vector3 targetPosition;
		private Tween movementTween;

		public override void SetUp()
		{
			if (Engine.EditorHint) return; //In Editor

			targetPosition = GlobalTransform.origin;
			spawnData.UpdateSpawnData(this);
			spawnData.spawnTransform.origin = GlobalTransform.origin + spawnOffset;
			StageManager.instance.RegisterRespawnableObject(this);

			_animationTree = GetNode<AnimationTree>(animationTree);
			_animationTree.Active = true;

			_hurtbox = GetNode<CollisionShape>(hurtbox);
			_hurtbox.Disabled = true;

			_lockonArea = GetNode<CollisionShape>(lockonArea);

			if (!activationTrigger.IsEmpty())
			{
				_activationTrigger = GetNode<Area>(activationTrigger);
				_activationTrigger.Connect("area_entered", this, nameof(Activate));
			}

			movementTween = new Tween();
			AddChild(movementTween);

			Spawn();
		}

		public override void Spawn()
		{
			base.Spawn();

			if (activationTrigger.IsEmpty()) //No activation trigger. Activate immediately.
				Activate();
			else
				Despawn(); //Start despawned
		}

		public override void OnEnter()
		{
			if (Character.IsAttacking)
			{
				Vector3 travelOffset = (_lockonArea.GlobalTransform.origin - Character.CenterPosition).Flatten().Normalized() * 10f * Character.MoveSpeed / Character.homingAttackSpeed;
				movementTween.InterpolateProperty(this, "global_transform:origin", GlobalTransform.origin, GlobalTransform.origin + travelOffset, .5f);
				movementTween.InterpolateCallback(this, .5f, nameof(Despawn));
				movementTween.Start();

				_lockonArea.Disabled = true;
				Character.HitEnemy(_lockonArea.GlobalTransform.origin);

				EmitSignal(nameof(OnDefeated));
			}
			else
				Character.TakeDamage(this);
		}

		public override void OnExit() => Character.CancelDamage(this);

		public override void _PhysicsProcess(float _)
		{
			if (!IsInsideTree() || !Visible) return;

			//Look at player
		}

		private void Activate()
		{
			if (spawnOffset.IsEqualApprox(Vector3.Zero))
			{
				//TODO Play vfx
				_animationTree.Set("parameters/teleport/active", true);
				_animationTree.Set("parameters/idle_seek/seek_position", 0f);
			}
			else
			{
				movementTween.InterpolateProperty(this, "global_transform:origin", spawnData.spawnTransform.origin, targetPosition, 1f, Tween.TransitionType.Sine, Tween.EaseType.Out);
				movementTween.Start();
			}

			_hurtbox.Disabled = false;
			_lockonArea.Disabled = false;
		}

		//Call this from an activation signal
		private void Activate(Area _) => Activate();
	}
}
