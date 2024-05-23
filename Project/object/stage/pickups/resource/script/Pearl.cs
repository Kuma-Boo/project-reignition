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
			Character.Animator.AddChild(this);
			GlobalTransform = t;

			if (tweener != null)
				tweener.Kill();

			// Collection tween
			tweener = CreateTween().SetTrans(Tween.TransitionType.Sine);
			float distance = Runtime.randomNumberGenerator.RandfRange(.2f, .8f);
			float height = Runtime.randomNumberGenerator.RandfRange(.2f, .5f);
			Vector3 endPoint = new(distance, height, -.5f);
			Vector3 midPoint = new(distance * 2, height * 2f, .5f);

			if (Runtime.randomNumberGenerator.Randf() > .5f)
			{
				endPoint.X *= -1;
				midPoint.X *= -1;
			}

			if (Character.IsMovingBackward)
			{
				endPoint.Z *= -1;
				midPoint.Z *= -1;
			}

			tweener.TweenProperty(this, "position", midPoint, .2f);
			tweener.TweenProperty(this, "position", endPoint, .2f).SetEase(Tween.EaseType.In);
			tweener.Parallel().TweenProperty(this, "scale", Vector3.One * .001f, .2f).SetEase(Tween.EaseType.In);

			tweener.Parallel().TweenCallback(Callable.From(() => Character.Skills.ModifySoulGauge(isRichPearl ? 20 : 1)));
			tweener.TweenCallback(new Callable(this, MethodName.Despawn)).SetDelay(3f);

			if (isRichPearl) // Play the correct sfx
				Sound.PlayRichPearlSFX();
			else
				Sound.PlayPearlSFX();

			StageSettings.instance.UpdateScore(isRichPearl ? 5 : 1, StageSettings.MathModeEnum.Add);
			isCollected = true;
			base.Collect();
		}
	}
}
