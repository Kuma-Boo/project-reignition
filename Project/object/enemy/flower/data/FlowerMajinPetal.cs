using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class FlowerMajinPetal : Node3D
{
	[Export]
	private BoneAttachment3D attachment;
	[Export]
	private AnimationPlayer animator;
	[Export]
	private float explosionPower;
	[Export(PropertyHint.Range, "0,1,.01")]
	private float fallDamping;
	[Export(PropertyHint.Range, "0,1,.01")]
	private float riseDamping;
	[Export(PropertyHint.Range, "0,1,.01")]
	private float fallGravityScale;
	[Export(PropertyHint.Range, "0,1,.01")]
	private float riseGravityScale;
	[Export(PropertyHint.Range, "0,1,.01")]
	private float directionBlend = .5f;

	private Vector3 linearVelocity;
	private Vector3 angularVelocity;
	private float currentLifetime;
	private readonly float Lifetime = 10f;

	public override void _Ready() => StageSettings.instance.ConnectRespawnSignal(this);

	public void Respawn() => Visible = false;

	public void Explode()
	{
		animator.Play("RESET");
		animator.Advance(0.0);
		animator.Play("loop");

		// Sync position and rotation
		GlobalRotation = attachment.GlobalRotation;
		GlobalPosition = attachment.GlobalPosition + this.Up();

		linearVelocity = this.Forward().Lerp(this.Up(), directionBlend).Normalized() * explosionPower;
		angularVelocity = this.Forward().Lerp(this.Up(), directionBlend).Normalized() * Runtime.randomNumberGenerator.RandfRange(5f, 10f);

		currentLifetime = 0;
		Visible = true;
	}

	public override void _PhysicsProcess(double _)
	{
		if (!Visible)
			return;

		linearVelocity += this.Up().RemoveVertical() * 5.0f;
		if (linearVelocity.Y <= 0.0f)
		{
			linearVelocity += Vector3.Down * Runtime.Gravity * fallGravityScale;
			linearVelocity *= fallDamping;
		}
		else
		{
			linearVelocity += Vector3.Down * Runtime.Gravity * riseGravityScale;
			linearVelocity *= riseDamping;
		}

		Position += linearVelocity * PhysicsManager.physicsDelta;
		Rotation += angularVelocity * PhysicsManager.physicsDelta;

		currentLifetime += PhysicsManager.physicsDelta;
		if (currentLifetime > Lifetime)
			Visible = false;
	}
}
