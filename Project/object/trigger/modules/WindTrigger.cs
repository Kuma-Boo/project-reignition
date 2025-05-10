using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers;

public partial class WindTrigger : StageTriggerModule
{

	[Export(PropertyHint.NodePathValidTypes, "GPUParticles3D")] private NodePath windFX;
	private GpuParticles3D _windFX;
	[Export] private float windStrength = 15.0f;

	private bool isActive;
	private float influence;

	private readonly float BlendRatio = 2.0f;

	public override void _Ready()
	{
		_windFX = GetNodeOrNull<GpuParticles3D>(windFX);
		DisableProcessing();
	}

	public override void _PhysicsProcess(double _)
	{
		influence = Mathf.MoveToward(influence, isActive ? 1f : 0f, BlendRatio * PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(influence))
			DisableProcessing();

		if (!Player.Skills.IsSpeedBreakActive)
			Player.ExternalVelocity += this.Forward() * windStrength * influence;

		GlobalPosition = Player.PathFollower.GlobalPosition;
	}

	public override void Activate()
	{
		Visible = true;
		ProcessMode = ProcessModeEnum.Inherit;

		isActive = true;
		if (IsInstanceValid(_windFX))
			_windFX.Restart();
	}

	public override void Deactivate()
	{
		isActive = false;
		if (IsInstanceValid(_windFX))
			_windFX.Emitting = false;
	}

	public override void Respawn() => Deactivate();

	private void DisableProcessing()
	{
		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;
	}
}
