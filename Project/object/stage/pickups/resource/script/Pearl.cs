using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	public partial class Pearl : Pickup
	{
		[Export]
		public NodePath collider;
		[Export]
		public bool isRichPearl;

		private bool isCollected;

		protected override void SetUp()
		{
			base.SetUp();
			GetNode<CollisionShape3D>(collider).Shape = isRichPearl ? RuntimeConstants.RichPearlCollisionShape : RuntimeConstants.PearlCollisionShape;
		}

		public override void Respawn()
		{
			isCollected = false;

			base.Respawn();
		}

		protected override void Collect()
		{
			if (isCollected) return;

			Transform3D t = GlobalTransform;
			GetParent().RemoveChild(this);
			Character.AddChild(this);
			GlobalTransform = t;

			SoundManager.instance.PlayPearlSoundEffect();

			Tween tweener = CreateTween().SetTrans(Tween.TransitionType.Sine);
			//Collection tween
			int travelDirection = RuntimeConstants.randomNumberGenerator.RandiRange(-1, 1);
			bool reverseDirection = Mathf.Sign(Character.Forward().Dot(Position)) < 0; //True when collecting a pearl behind us
			if (travelDirection == 0)
			{
				tweener.TweenProperty(this, "translation", new Vector3(0, .5f, (reverseDirection ? -1 : 1) * .8f), .2f).SetEase(Tween.EaseType.InOut);
				tweener.TweenProperty(this, "scale", Vector3.Zero, .2f).SetEase(Tween.EaseType.In);
			}
			else
			{
				Vector3 endPoint = new Vector3(0, .5f, -1f);
				Vector3 midPoint = new Vector3(travelDirection * .7f, (Position.y + endPoint.y) * .5f, 0);

				if (reverseDirection)
				{
					endPoint.z *= -1;
					midPoint.x *= -1;
					midPoint.z *= -1;
				}

				tweener.TweenProperty(this, "translation:y", endPoint.y, .2f);
				tweener.TweenProperty(this, "translation:x", midPoint.x, .2f).SetEase(Tween.EaseType.Out);
				tweener.TweenProperty(this, "translation:z", midPoint.z, .2f).SetEase(Tween.EaseType.In);
				tweener.TweenProperty(this, "translation:x", endPoint.x, .2f).SetEase(Tween.EaseType.In).SetDelay(.2f);
				tweener.TweenProperty(this, "translation:z", endPoint.z, .2f).SetEase(Tween.EaseType.Out).SetDelay(.2f);
				tweener.TweenProperty(this, "scale", Vector3.One * .6f, .2f).SetEase(Tween.EaseType.In);
			}

			//TODO BROKEN
			//isRichPearl ? 20 : 1
			//tweener.TweenCallback(new Callable(Character.Soul, CharacterSoulSkill.MethodName.ModifySoulGauge)).SetDelay(.1f);
			tweener.TweenCallback(new Callable(this, MethodName.Despawn)).SetDelay(.3f);

			StageSettings.instance.ChangeScore(isRichPearl ? 5 : 1, StageSettings.ScoreFunction.Add);
			isCollected = true;
			base.Collect();
		}
	}
}
