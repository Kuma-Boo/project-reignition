using Godot;
using System;

namespace Project.Gameplay;

/// <summary> Controls the behavior of the flying scrap used in the Ifrit Golem boss fight. </summary>
public partial class TraversalScrap : Area3D
{
	private bool isFalling;
	private bool isInteractingWithPlayer;
	private PlayerController Player => StageSettings.Player;

	public override void _Process(double _)
	{
		if (!isInteractingWithPlayer)
			return;

		if (!Player.IsJumpDashOrHomingAttack)
			return;

		Player.StartBounce();

	}
}
