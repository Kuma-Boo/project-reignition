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
		[Export]
		private AudioStreamPlayer sfx;
		private bool isCollected;

		private SoundManager Sound => SoundManager.instance;

		protected override void SetUp()
		{
			collider.Shape = isRichPearl ? RuntimeConstants.Instance.RichPearlCollisionShape : RuntimeConstants.Instance.PearlCollisionShape;
			base.SetUp();
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

			Tween tweener = CreateTween().SetTrans(Tween.TransitionType.Sine);
			//Collection tween
			int travelDirection = RuntimeConstants.randomNumberGenerator.RandiRange(-1, 1);
			bool reverseDirection = Mathf.Sign(Character.Forward().Dot(Position)) < 0; //True when collecting a pearl behind us

			Vector3 endPoint = new Vector3(0, .5f, 0);
			Vector3 midPoint = new Vector3(travelDirection * .7f, (Position.y + endPoint.y) * .5f, 0);

			if (reverseDirection)
			{
				endPoint.z *= -1;
				midPoint.x *= -1;
				midPoint.z *= -1;
			}

			tweener.TweenProperty(this, "position", midPoint, .2f);
			tweener.TweenProperty(this, "position", endPoint, .2f).SetEase(Tween.EaseType.In);
			tweener.Parallel().TweenProperty(this, "scale", Vector3.One * .001f, .2f).SetEase(Tween.EaseType.In);

			//TODO BROKEN
			//isRichPearl ? 20 : 1
			//tweener.TweenCallback(new Callable(Character.Soul, CharacterSoulSkill.MethodName.ModifySoulGauge)).SetDelay(.1f);
			tweener.TweenCallback(new Callable(this, MethodName.Despawn)).SetDelay(3f);

			if (!isRichPearl) //Play the correct sfx
			{
				sfx.Stream = Sound.pearlStreams[Sound.PearlSoundEffectIndex];
				Sound.PearlSoundEffectIndex++;
				if (Sound.PearlSoundEffectIndex >= Sound.pearlStreams.Length)
					Sound.PearlSoundEffectIndex = Sound.pearlStreams.Length - 1;

				Sound.StartPearlTimer();
			}
			sfx.Play();

			StageSettings.instance.ChangeScore(isRichPearl ? 5 : 1, StageSettings.ScoreFunction.Add);
			isCollected = true;
			base.Collect();
		}
	}
}
