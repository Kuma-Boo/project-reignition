using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers;

public partial class WindTrigger : StageTriggerModule
{
	[Export] private GpuParticles3D windFX;
	[Export] private float windStrength = 15.0f;

	private bool isActive;
	private float influence;

	private readonly float BlendRatio = 2.0f;

	public override void _Ready()
	{
		if (IsInstanceValid(windFX))
		{
			// Place WindFX in the right spot
			foreach (Node node in GetParent().GetChildren())
			{
				if (node is not CollisionShape3D)
					continue;

				Shape3D shape = (node as CollisionShape3D).Shape;
				if (shape is BoxShape3D)
				{
					windFX.GlobalPosition = GlobalPosition + (this.Back() * (shape as BoxShape3D).Size.Z);
					break;
				}
			}
		}

		DisableProcessing();
	}

	public override void _PhysicsProcess(double _)
	{
		influence = Mathf.MoveToward(influence, isActive ? 1f : 0f, BlendRatio * PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(influence))
			DisableProcessing();

		if (!Player.Skills.IsSpeedBreakActive)
			Player.ExternalVelocity += this.Forward() * windStrength * influence;
	}

	public override void Activate()
	{
		Visible = true;
		ProcessMode = ProcessModeEnum.Inherit;

		isActive = true;
		if (IsInstanceValid(windFX))
			windFX.Restart();
	}

	public override void Deactivate()
	{
		isActive = false;
		if (IsInstanceValid(windFX))
			windFX.Emitting = false;
	}

	public override void Respawn() => Deactivate();

	private void DisableProcessing()
	{
		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;
	}
}
