using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	public class Pearl : RespawnableObject
	{
		[Export]
		public NodePath collider;
		[Export]
		public bool isRichPearl;

		private bool isCollected;
		private Tween tweener;

		protected override bool IsRespawnable() => true;
		protected override void SetUp()
		{
			base.SetUp();
			GetNode<CollisionShape>(collider).Shape = isRichPearl ? StageSettings.RichPearlCollisionShape : StageSettings.PearlCollisionShape;

			tweener = new Tween();
			AddChild(tweener);
		}

		/*
		public override void _Process(float _)
		{
			if(isReparenting)
				Collect();
		}
		*/

		public override void Respawn()
		{
			ResetTweener();
			isCollected = false;
			
			base.Respawn();
		}

		private void ResetTweener() => tweener.StopAll(); //Unreference tweener

		private void OnEntered(Area _)
		{
			if (isCollected) return;
			CallDeferred(nameof(Collect));
		}

		//Godot doesn't allow reparenting from signals, so a separate function is needed.
		private void Collect()
		{
			Transform t = GlobalTransform;
			GetParent().RemoveChild(this);
			Character.AddChild(this);
			GlobalTransform = t;
			
			SoundManager.instance.PlayPearlSoundEffect();

			//Collection tween
			int travelDirection = StageSettings.randomNumberGenerator.RandiRange(-1, 1);
			bool reverseDirection = Mathf.Sign(Character.Forward().Dot(Transform.origin)) < 0; //True when collecting a pearl behind us
			if (travelDirection == 0)
			{
				tweener.InterpolateProperty(this, "translation", Vector3.Zero, new Vector3(0, .5f, (reverseDirection ? -1 : 1) * .8f), .2f, Tween.TransitionType.Sine, Tween.EaseType.InOut);
				tweener.InterpolateProperty(this, "scale", Vector3.One, Vector3.Zero, .2f, Tween.TransitionType.Sine, Tween.EaseType.In);
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

				tweener.InterpolateProperty(this, "translation:y", Transform.origin.y, endPoint.y, .2f);
				tweener.InterpolateProperty(this, "translation:x", Transform.origin.x, midPoint.x, .2f, Tween.TransitionType.Sine, Tween.EaseType.Out);
				tweener.InterpolateProperty(this, "translation:z", Transform.origin.z, midPoint.z, .2f, Tween.TransitionType.Sine, Tween.EaseType.In);
				tweener.InterpolateProperty(this, "translation:x", midPoint.x, endPoint.x, .2f, Tween.TransitionType.Sine, Tween.EaseType.In, .2f);
				tweener.InterpolateProperty(this, "translation:z", midPoint.z, endPoint.z, .2f, Tween.TransitionType.Sine, Tween.EaseType.Out, .2f);
				tweener.InterpolateProperty(this, "scale", Vector3.One, Vector3.One * .6f, .2f, Tween.TransitionType.Sine, Tween.EaseType.In);
			}

			tweener.InterpolateCallback(HeadsUpDisplay.instance, .1f, nameof(HeadsUpDisplay.instance.ModifySoulGauge), isRichPearl ? 20 : 1);
			tweener.InterpolateCallback(this, .3f, nameof(Despawn));
			tweener.Start();

			isCollected = true;
		}
	}
}
