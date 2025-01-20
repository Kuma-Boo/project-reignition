using Godot;
using Project.Gameplay.Objects;

namespace Project.Gameplay;

public partial class IvyState : PlayerState
{
	public Ivy Trigger { get; set; }

	public override void EnterState()
	{
		Player.Lockon.IsMonitoring = false;
		Player.StartExternal(Trigger, Trigger.LaunchPoint, 0.2f);

		// TODO Figure out if the player is jump dashing and add extra swing power based on that
		Player.MoveSpeed = 0;
		Player.Animator.StartIvy();
	}

	public override void ExitState()
	{
		Player.StopExternal();
	}

	public override PlayerState ProcessPhysics()
	{
		Player.UpdateExternalControl();
		Player.Animator.SetIvyBlend(Trigger.LaunchRatio);

		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();
			Trigger.Activate();
			return null;
		}

		if (Player.Controller.IsActionBufferActive && !Player.Animator.IsIvySwingActive)
		{
			Player.Controller.ResetActionBuffer();
			Player.Animator.StartIvySwing();

			// TODO Actually add swing speeds
			GD.Print("Adding Swing Power!");
		}

		return null;
	}
}
