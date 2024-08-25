using Godot;
using System;

namespace Project.Gameplay;
public partial class LandState : PlayerState
{
	[Export]
	private PlayerState runState;
	[Export]
	private PlayerState idleState;
	[Export]
	private PlayerState backstepState;

	public override PlayerState ProcessPhysics()
	{
		if (Mathf.IsZeroApprox(Controller.MoveSpeed))
			return idleState;

		if (Controller.MoveSpeed > 0)
			return runState;

		return backstepState;
	}
}
