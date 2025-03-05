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
		private float collectionRange;
		private Vector3 collectionVelocity;
		private readonly float CollectionSpeed = 10.0f;

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
			if (!isMagnetized || Collider.Disabled)
				return;

			if (!Player.IsLightDashing)
			{
				GlobalPosition = GlobalPosition.SmoothDamp(Player.CenterPosition, ref collectionVelocity, CollectionSpeed * PhysicsManager.physicsDelta);
				GlobalPosition = GlobalPosition.MoveToward(Player.CenterPosition, Player.MoveSpeed * PhysicsManager.physicsDelta);
			}

			if (Player.CenterPosition.DistanceSquaredTo(GlobalPosition) > collectionRange)
				return;

			ApplyCollection();
		}

		private void ApplyCollection()
		{
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

			BonusManager.instance.AddRingChain();
			Animator.Play("collect");
			base.Collect();
		}
	}
}
