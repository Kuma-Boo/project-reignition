using Godot;
using Project.Core;
using Project.CustomNodes;

namespace Project.Gameplay.Objects;

[Tool]
public partial class SandStep : Node3D
{
	[ExportToolButton("Update Sand")]
	public Callable EditorFlowGroup => Callable.From(UpdateFlowingState);

	[Export] private bool isSandFlowing;
	[Export] private GroupGpuParticles3D particleParent;
	[Export] private Area3D lockonTrigger;
	[Export] private AudioStreamPlayer3D sfx;

	private bool isInteractingWithPlayer;

	public override void _Ready()
	{
		UpdateFlowingState();

		if (Engine.IsEditorHint())
			return;

		StageSettings.Player.Skills.TimeBreakStarted += EnableSandCollision;
		StageSettings.Player.Skills.TimeBreakStopped += DisableSandCollision;
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint() || !isInteractingWithPlayer)
			return;

		if (StageSettings.Player.IsHomingAttacking)
			StageSettings.Player.StartBounce(true, .5f);

		UpdateSfx();
	}

	private void UpdateSfx()
	{
		if (isSandFlowing || !sfx.Playing)
			return;

		// Fade out sfx
		sfx.VolumeLinear = Mathf.MoveToward(sfx.VolumeLinear, 0f, PhysicsManager.physicsDelta);

		if (Mathf.IsZeroApprox(sfx.VolumeLinear))
			sfx.Stop();
	}


	private void SetFlowingState(bool value)
	{
		isSandFlowing = value;
		UpdateFlowingState();
	}

	private void UpdateFlowingState()
	{
		particleParent?.SetEmitting(isSandFlowing);

		if (Engine.IsEditorHint())
			return;

		if (isSandFlowing)
		{
			// Update sfx
			sfx.VolumeLinear = 1f;

			if (!sfx.Playing)
				sfx.Play();
		}

		if (StageSettings.Player?.Skills.IsTimeBreakActive == true)
			EnableSandCollision();
		else
			DisableSandCollision();
	}

	private void EnableSandCollision()
	{
		particleParent?.SetSpeedScale(0);
		lockonTrigger.SetDeferred("monitorable", isSandFlowing);
		lockonTrigger.SetDeferred("monitoring", isSandFlowing);
	}

	private void DisableSandCollision()
	{
		particleParent?.SetSpeedScale(1);
		lockonTrigger.SetDeferred("monitorable", false);
		lockonTrigger.SetDeferred("monitoring", false);
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		isInteractingWithPlayer = true;
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		isInteractingWithPlayer = false;
	}
}
