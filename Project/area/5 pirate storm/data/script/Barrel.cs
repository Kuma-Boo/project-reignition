using Godot;
using Project.Core;

namespace Project.Gameplay.Objects;

[Tool]
public partial class Barrel : Launcher
{
	/// <summary> How long to remain on the surface. </summary>
	[Export] private float floatTime = 3f;
	/// <summary> How long to remain beneath the surface. Set to 0 if the barrel should never sink. </summary>
	[Export(PropertyHint.Range, "0,2,0.1")] private float sinkTime;
	[Export] private AnimationPlayer animator;

	private float floatTimer;
	private bool isFloating = true;
	private bool isInteractingWithPlayer;

	public override LaunchSettings GetLaunchSettings()
	{
		LaunchSettings settings = base.GetLaunchSettings();
		settings.AllowInterruption = true;
		return settings;
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint())
			return;

		ProcessFloating();

		if (!isInteractingWithPlayer)
			return;

		ProcessInteraction();
	}

	private void ProcessFloating()
	{
		if (Mathf.IsZeroApprox(sinkTime) && isFloating) // Barrel never sinks
			return;

		if (isFloating)
		{
			floatTimer = Mathf.MoveToward(floatTimer, floatTime, PhysicsManager.physicsDelta);

			if (Mathf.IsEqualApprox(floatTimer, floatTime))
				SinkBelowSurface();

			return;
		}

		// Process rising back to the surface
		floatTimer = Mathf.MoveToward(floatTimer, sinkTime, PhysicsManager.physicsDelta);
		if (Mathf.IsEqualApprox(floatTimer, sinkTime))
			ReturnToSurface();
	}

	private void SinkBelowSurface()
	{
		// Sink below the surface
		isFloating = false;
		animator.Play("sink");
		floatTimer = -(float)animator.CurrentAnimationLength;
	}

	private void ReturnToSurface()
	{
		isFloating = true;
		animator.Play("float");
		floatTimer = -(float)animator.CurrentAnimationLength;
	}


	private void ProcessInteraction()
	{
		if (Player.IsJumpDashOrHomingAttack)
		{
			Activate();
			isFloating = false;
			animator.Play("strike");
			floatTimer = -(float)animator.CurrentAnimationLength;
		}
	}

	public override void Activate()
	{
		base.Activate();
		Player.Animator.StartSpin(5f);
		Player.Effect.StartSpinFX();
	}

	private void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		isInteractingWithPlayer = true;
	}

	private void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		isInteractingWithPlayer = false;
	}
}
