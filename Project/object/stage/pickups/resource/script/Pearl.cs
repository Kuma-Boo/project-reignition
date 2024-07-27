using Godot;
using Project.Core;

namespace Project.Gameplay.Objects;

public partial class Pearl : Pickup
{
	[Export]
	private bool isRichPearl;
	[Export(PropertyHint.NodePathValidTypes, "CollisionShape3D")]
	private NodePath collider;
	private CollisionShape3D Collider { get; set; }
	private bool isCollected;

	public Tween Tweener { get; set; }
	private SoundManager Sound => SoundManager.instance;

	protected override void SetUp()
	{
		Collider = GetNodeOrNull<CollisionShape3D>(collider);
		Collider.Shape = isRichPearl ? Runtime.Instance.RichPearlCollisionShape : Runtime.Instance.PearlCollisionShape;
		base.SetUp();
	}

	public override void Respawn()
	{
		Tweener?.Kill();

		isCollected = false;
		Basis = Basis.Identity;

		base.Respawn();
	}

	protected override void Collect()
	{
		if (isCollected || !IsInsideTree()) return;

		Transform3D t = GlobalTransform;
		GetParent().RemoveChild(this);
		Character.AddChild(this);
		GlobalTransform = t;

		Tweener?.Kill();

		// Collection tween
		Tweener = CreateTween().SetTrans(Tween.TransitionType.Sine);
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

		Tweener.TweenProperty(this, "position", midPoint, .2f);
		Tweener.TweenProperty(this, "position", endPoint, .2f).SetEase(Tween.EaseType.In);
		Tweener.Parallel().TweenProperty(this, "scale", Vector3.One * .001f, .2f).SetEase(Tween.EaseType.In);

		Tweener.Parallel().TweenCallback(Callable.From(() => Character.Skills.ModifySoulGauge(isRichPearl ? 20 : 1)));
		Tweener.TweenCallback(new Callable(this, MethodName.Despawn)).SetDelay(3f);

		if (isRichPearl) // Play the correct sfx
			Sound.PlayRichPearlSFX();
		else
			Sound.PlayPearlSFX();

		isCollected = true;

		StageSettings.instance.UpdateScore(isRichPearl ? 5 : 1, StageSettings.MathModeEnum.Add);
		if (StageSettings.instance.Data.MissionType == LevelDataResource.MissionTypes.Pearl &&
			StageSettings.instance.Data.MissionObjectiveCount == 0)
		{
			StageSettings.instance.FinishLevel(false);
		}
		base.Collect();
	}
}