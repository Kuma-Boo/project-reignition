using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	public partial class Ring : Pickup
	{
		[Export]
		private bool isRichRing;
		[Export(PropertyHint.NodePathValidTypes, "AnimationPlayer")]
		private NodePath animator;
		private AnimationPlayer Animator { get; set; }
		[Export(PropertyHint.NodePathValidTypes, "CollisionShape3D")]
		private NodePath collider;
		private CollisionShape3D Collider { get; set; }

		private bool isMagnetized;
		private bool isCollected;
		/* <summary> A timer to keep track of how long the ring has been trailing the player.
			Artificially increases speed to force rings to be collected. </summary> */
		private float collectionTimer;
		private float collectionRange;
		private readonly float CollectionSpeed = 10.0f;

		private readonly int RingAchievementRequirement = 10000;
		private readonly StringName RingAchievementName = "ring getter";

		protected override void SetUp()
		{
			Animator = GetNodeOrNull<AnimationPlayer>(animator);
			Collider = GetNodeOrNull<CollisionShape3D>(collider);

			// Calculate collection range BEFORE updating the collision shape
			collectionRange = Mathf.Pow((Collider.Shape as SphereShape3D).Radius, 2.0f);
			Collider.Shape = isRichRing ? Runtime.Instance.RichRingCollisionShape : Runtime.Instance.RingCollisionShape;
			base.SetUp();
		}

		public override void Respawn()
		{
			isMagnetized = false;
			isCollected = false;
			collectionTimer = 0f;

			Animator.Play("RESET");
			Animator.Queue("loop");

			base.Respawn();
		}

		protected override void Collect()
		{
			if (!SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.RingRange))
			{
				ApplyCollection();
				return;
			}

			isMagnetized = true;
		}

		public override void _PhysicsProcess(double _)
		{
			if (!isMagnetized || isCollected)
				return;

			if (!Player.IsLightDashing)
			{
				collectionTimer = Mathf.Min(collectionTimer + (CollectionSpeed * PhysicsManager.physicsDelta), 1f);
				GlobalPosition = GlobalPosition.Lerp(Player.CenterPosition, collectionTimer);
				GlobalPosition = GlobalPosition.MoveToward(Player.CenterPosition, Player.MoveSpeed * PhysicsManager.physicsDelta);
			}

			if (Player.CenterPosition.DistanceSquaredTo(GlobalPosition) > collectionRange)
				return;

			ApplyCollection();
		}

		private void ApplyCollection()
		{
			if (isCollected)
				return;

			if (isRichRing)
			{
				SoundManager.instance.PlayRichRingSFX();
				Stage.UpdateScore(100, StageSettings.MathModeEnum.Add);
				if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.RingPearlConvert))
				{
					Stage.UpdateRingCount(0, StageSettings.MathModeEnum.Add);
					Player.Skills.ModifySoulGauge(40);
				}
				else
				{
					Stage.UpdateRingCount(20, StageSettings.MathModeEnum.Add);
				}
			}
			else
			{
				SoundManager.instance.PlayRingSFX();
				Stage.UpdateScore(10, StageSettings.MathModeEnum.Add);
				if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.RingPearlConvert))
				{
					Stage.UpdateRingCount(0, StageSettings.MathModeEnum.Add);
					Player.Skills.ModifySoulGauge(2);
				}
				else
				{
					Stage.UpdateRingCount(1, StageSettings.MathModeEnum.Add);
				}
			}

			isCollected = true;

			SaveManager.SharedData.RingCount = (int)Mathf.MoveToward(SaveManager.SharedData.RingCount, int.MaxValue, isRichRing ? 20 : 1);
			if (SaveManager.SharedData.RingCount >= RingAchievementRequirement)
				AchievementManager.Instance.UnlockAchievement(RingAchievementName);

			BonusManager.instance.AddRingChain();
			Animator.Play("collect");
			Animator.Advance(0.0);
			base.Collect();
		}
	}
}
