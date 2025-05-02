using Godot;
using Project.Core;

namespace Project.Gameplay.Objects;

public partial class FlyingDestructableObject : DestructableObject
{
	[Export] private Node3D rotationRoot;
	private Transform3D initialTransform;
	[Export] private float flyingSpeed = 10.0f;
	[Export] private float lifetime = 10.0f;
	private float currentLifetime;
	private bool isSleeping;

	protected override void SetUp()
	{
		initialTransform = rotationRoot.Transform;
		base.SetUp();
	}

	public override void Respawn()
	{
		isSleeping = true;
		currentLifetime = 0;
		rotationRoot.Transform = initialTransform;

		base.Respawn();
	}

	protected override void ProcessObject()
	{
		base.ProcessObject();

		if (!isSleeping)
		{
			rotationRoot.GlobalPosition += this.Forward() * flyingSpeed * PhysicsManager.physicsDelta;
			currentLifetime += PhysicsManager.physicsDelta;
			if (currentLifetime >= lifetime)
				Shatter();
		}
	}

	public override void Shatter()
	{
		if (isShattered) return;

		base.Shatter();
		isSleeping = true;
		pieceRoot.GlobalTransform = rotationRoot.GlobalTransform;
	}

	public void Activate()
	{
		isSleeping = false;
		animator.Play("fly", .5f);
	}

	public void Deactivate()
	{
		isSleeping = true;
		if (animator.HasAnimation("deactivate"))
			animator.Play("deactivate");
	}
}
