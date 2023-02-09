using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	public partial class Pearl : Pickup
	{
		[Export]
		private bool isRichPearl;
		[Export]
		private CollisionShape3D collider;
		private bool isCollected;

		private Tween tweener;
		private SoundManager Sound => SoundManager.instance;

		protected override void SetUp()
		{
			collider.Shape = isRichPearl ? Runtime.Instance.RichPearlCollisionShape : Runtime.Instance.PearlCollisionShape;
			base.SetUp();
		}

		public override void Respawn()
		{
			if (tweener != null)
				tweener.Kill();

			isCollected = false;
			Basis = Basis.Identity;

			base.Respawn();
		}

		protected override void Collect()
		{
			if (isCollected) return;

			Transform3D t = GlobalTransform;
			GetParent().RemoveChild(this);
			Character.AddChild(this);
			GlobalTransform = t;

			if (tweener != null)
				tweener.Kill();

			tweener = CreateTween().SetTrans(Tween.TransitionType.Sine);
			//Collection tween
			bool reverseDirection = Mathf.Sign(Character.Forward().Dot(Position)) < 0; //True when collecting a pearl behind us

			Vector3 endPoint = new Vector3(0, .5f, 0);
			Vector3 midPoint = new Vector3(Runtime.randomNumberGenerator.Randfn(0, .4f), (Position.Y + endPoint.Y) * .5f, 0);

			if (reverseDirection)
			{
				endPoint.Z *= -1;
				midPoint.X *= -1;
				midPoint.Z *= -1;
			}

			tweener.TweenProperty(this, "position", midPoint, .2f);
			tweener.TweenProperty(this, "position", endPoint, .2f).SetEase(Tween.EaseType.In);
			tweener.Parallel().TweenProperty(this, "scale", Vector3.One * .001f, .2f).SetEase(Tween.EaseType.In);

			//TODO Modify soul gauge
			tweener.TweenCallback(Callable.From(() => Character.Skills.ModifySoulGauge(isRichPearl ? 20 : 1))).SetDelay(.1f);
			tweener.TweenCallback(new Callable(this, MethodName.Despawn)).SetDelay(3f);

			if (!isRichPearl) //Play the correct sfx
				Sound.PlayPearlSFX();
			else
				Sound.PlayRichPearlSFX();

			LevelSettings.instance.UpdateScore(isRichPearl ? 5 : 1, LevelSettings.MathModeEnum.Add);
			isCollected = true;
			base.Collect();
		}
	}
}
