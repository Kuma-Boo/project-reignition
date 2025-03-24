using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class ZiplineState : PlayerState
{
	public Zipline Trigger { get; set; }

	private float animationVelocity;
	private readonly float AnimationSmoothing = 2f;

	public override void EnterState()
	{
		Player.Animator.StartZipline();
		Player.Animator.SetZiplineBlend(0f);
		Player.StartExternal(Trigger, Trigger.FollowObject, .5f);
	}

	public override void ExitState()
	{
		Player.StopExternal();
		Trigger.StopZipline();
		Trigger = null;
	}

	public override PlayerState ProcessPhysics()
	{
		// TODO Have the controller take the camera into account?
		float input = Player.Controller.InputHorizontal;
		Trigger.SetInput(input);
		Trigger.ProcessZipline();

		float animationBlend = Player.Animator.GetZiplineBlend();
		animationBlend = ExtensionMethods.SmoothDamp(animationBlend, input, ref animationVelocity, AnimationSmoothing * PhysicsManager.physicsDelta);
		Player.Animator.SetZiplineBlend(animationBlend);

		Player.CallDeferred(PlayerController.MethodName.UpdateExternalControl, false);
		return null;
	}
}
