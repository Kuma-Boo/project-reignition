using Godot;
using Project.Core;

namespace Project.Gameplay
{
	public class Pearl : RespawnableObject
	{
		[Export]
		public NodePath collider;
		[Export]
		public bool isRichPearl;

		private bool isCollected;
		private bool isReparenting;
		private Tween tween;

		public override bool IsRespawnable() => true;
		public override void SetUp()
		{
			base.SetUp();
			GetNode<CollisionShape>(collider).Shape = isRichPearl ? StageManager.instance.RichPearlCollisionShape : StageManager.instance.PearlCollisionShape;
		}

		public override void _Process(float _)
		{
			if(isReparenting)
				Collect();
		}

		public override void Spawn()
		{
			base.Spawn();
			isCollected = false;
		}

		public override void OnEntered(Area _)
		{
			if (isCollected) return;
			isReparenting = true;
		}

		//Godot doesn't allow reparenting from signals, so a separate function is needed.
		private void Collect()
		{
			Transform t = GlobalTransform;
			GetParent().RemoveChild(this);
			Character.AddChild(this);
			GlobalTransform = t;

			if (tween == null)
			{
				tween = new Tween()
				{
					PlaybackProcessMode = Tween.TweenProcessMode.Physics
				};
				AddChild(tween);
			}
			else
				tween.StopAll();

			SFXLibrary.instance.PlayPearlSoundEffect();

			//Collection tween
			int travelDirection = StageManager.instance.randomNumberGenerator.RandiRange(-1, 1);
			bool reverseDirection = Mathf.Sign(Character.Forward().Dot(Transform.origin)) < 0; //True when collecting a pearl behind us
			if (travelDirection == 0)
			{
				tween.InterpolateProperty(this, "translation", Transform.origin, new Vector3(0, .5f, (reverseDirection ? -1 : 1) * .8f), .2f, Tween.TransitionType.Sine, Tween.EaseType.InOut);
				tween.InterpolateProperty(this, "scale", Vector3.One, Vector3.Zero, .2f, Tween.TransitionType.Sine, Tween.EaseType.In);
			}
			else
			{
				Vector3 endPoint = new Vector3(0, .5f, -1f);
				Vector3 midPoint = new Vector3(travelDirection * .7f, (Transform.origin.y + endPoint.y) * .5f, 0);

				if (reverseDirection)
				{
					endPoint.z *= -1;
					midPoint.x *= -1;
					midPoint.z *= -1;
				}

				tween.InterpolateProperty(this, "translation:y", Transform.origin.y, endPoint.y, .2f);
				tween.InterpolateProperty(this, "translation:x", Transform.origin.x, midPoint.x, .2f, Tween.TransitionType.Sine, Tween.EaseType.Out);
				tween.InterpolateProperty(this, "translation:z", Transform.origin.z, midPoint.z, .2f, Tween.TransitionType.Sine, Tween.EaseType.In);
				tween.InterpolateProperty(this, "translation:x", midPoint.x, endPoint.x, .2f, Tween.TransitionType.Sine, Tween.EaseType.In, .2f);
				tween.InterpolateProperty(this, "translation:z", midPoint.z, endPoint.z, .2f, Tween.TransitionType.Sine, Tween.EaseType.Out, .2f);
				tween.InterpolateProperty(this, "scale", Vector3.One, Vector3.One * .6f, .2f, Tween.TransitionType.Sine, Tween.EaseType.In);
			}

			tween.InterpolateCallback(GameplayInterface.instance, .1f, nameof(GameplayInterface.instance.ModifySoulPearl), isRichPearl ? 20 : 1);
			tween.InterpolateCallback(this, .3f, nameof(Despawn));
			tween.Start();

			isReparenting = false;
			isCollected = true;
		}
	}
}
