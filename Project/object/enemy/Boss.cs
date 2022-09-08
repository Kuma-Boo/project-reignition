using Godot;
using Godot.Collections;

namespace Project.Gameplay.Bosses
{
	public class Boss : Spatial
	{
		public static Boss instance;
		protected CharacterController Character => CharacterController.instance;

		[Export]
		public int health;
		protected int DamageTaken { get; private set; }

		[Export]
		public NodePath lockonTarget;
		protected Area _lockonTarget;

		[Export]
		public Array<BossPatternResource> patterns = new Array<BossPatternResource>();
		protected int CurrentPattern { get; private set; }

		[Export]
		public NodePath animationTree;
		protected AnimationTree Animator { get; private set; }

		public override void _Ready()
		{
			instance = this;
			SetUp();
		}

		protected virtual void SetUp()
		{
			_lockonTarget = GetNode<Area>(lockonTarget);
			Animator = GetNode<AnimationTree>(animationTree);
			Animator.Active = true;
			LoadAttackPattern();
		}

		public override void _PhysicsProcess(float _) => ProcessBoss();
		protected virtual void ProcessBoss() { }

		public virtual void TakeDamage(int amount)
		{
			DamageTaken += amount;

			if (DamageTaken >= health)
			{
				//Defeat boss
			}
			else if (CurrentPattern < patterns.Count - 1 && DamageTaken > patterns[CurrentPattern].damage) //Advance attack pattern
			{
				CurrentPattern++;
				LoadAttackPattern();
			}
		}

		protected virtual void LoadAttackPattern() { }
	}
}
