using Godot;
using Project.Core;

/// <summary>
/// Responsible for the lightning effects in Pirate Storm.
/// </summary>
namespace Project.Gameplay;

public partial class Lightning : Node3D
{
	[Export(PropertyHint.Range, "0,1")] private float environmentFx;
	[Export] private AnimationPlayer animator;

	[Export] private float minLightningInterval = 5.0f;
	[Export] private float maxLightningInterval = 10.0f;
	private Timer timer;
	private readonly StringName ShaderSecondaryEnvironmentFXParameter = "secondary_environment_fx_intensity";

	public override void _Ready()
	{
		timer = new()
		{
			Autostart = false,
		};
		timer.Timeout += StartLightningStrike;
		AddChild(timer);
		RandomizeLightningTime();
	}

	public override void _ExitTree()
	{
		// Reset secondary environment parameter
		RenderingServer.GlobalShaderParameterSet(ShaderSecondaryEnvironmentFXParameter, 0f);
	}

	private void StartLightningStrike()
	{
		animator.Play(Runtime.randomNumberGenerator.Randf() > 0.5f ? "strike-small" : "strike-large");
		animator.Advance(0.0);
		animator.Play("strike");
	}

	public override void _PhysicsProcess(double _) =>
		RenderingServer.GlobalShaderParameterSet(ShaderSecondaryEnvironmentFXParameter, environmentFx);

	private void RandomizeLightningTime() =>
		timer.Start(Runtime.randomNumberGenerator.RandfRange(minLightningInterval, maxLightningInterval));
}
