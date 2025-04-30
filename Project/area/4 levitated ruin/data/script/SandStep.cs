using Godot;
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
	}


	private void SetFlowingState(bool value)
	{
		isSandFlowing = value;
		UpdateFlowingState();
	}

	private void UpdateFlowingState()
	{
		particleParent?.SetEmitting(isSandFlowing);

		if (StageSettings.Player?.Skills.IsTimeBreakActive == true)
			EnableSandCollision();
		else
			DisableSandCollision();
	}

	private void EnableSandCollision()
	{
		particleParent?.SetSpeedScale(0);
		lockonTrigger.SetDeferred("monitorable", true);
		lockonTrigger.SetDeferred("monitoring", true);
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
