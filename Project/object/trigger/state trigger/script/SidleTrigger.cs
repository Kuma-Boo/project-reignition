using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Handles sidle behaviour.
	/// </summary>
	public partial class SidleTrigger : Area3D
	{
		[Export]
		private bool isFacingRight = true; //Which way to sidle?

		[Export]
		public MovementResource sidleSettings;

		private Node3D currentRailing;

		private bool isActive;
		private bool isInteractingWithPlayer;
		private CharacterController Character => CharacterController.instance;
		private InputManager.Controller Controller => InputManager.controller;

		public override void _PhysicsProcess(double _)
		{
			if (!isInteractingWithPlayer) return;

			if (isActive)
				UpdateSidle();
			else if (Character.IsOnGround && Character.ActionState == CharacterController.ActionStates.Normal)
				StartSidle();
		}

		private void StartSidle()
		{
			isActive = true;
			Character.StartExternal(Character.PathFollower, .2f);
			GD.PrintErr("Pathfollower may be inaccurate!!!");
		}

		private void UpdateSidle()
		{
			Character.MoveSpeed = sidleSettings.Interpolate(Character.MoveSpeed, isFacingRight ? Controller.MovementAxis.x : -Controller.MovementAxis.x);
			Character.PathFollower.Progress += Character.MoveSpeed * PhysicsManager.physicsDelta;
		}

		#region Sidle
		private void UpdateSidleDamage()
		{
		}

		private void UpdateSidleHang()
		{

		}
		#endregion

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			isInteractingWithPlayer = true;

			//Apply state
			Character.Skills.IsSpeedBreakEnabled = false;

			float dot = ExtensionMethods.DotAngle(Character.MovementAngle, Character.PathFollower.ForwardAngle);
			if (dot < 0)
			{
				Character.MoveSpeed = -Mathf.Abs(Character.MoveSpeed);
				Character.MovementAngle = Character.PathFollower.ForwardAngle;
				Character.PathFollower.Resync();
			}
		}

		public void OnExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			Character.MovementAngle = Character.MoveSpeed < 0 ? Character.PathFollower.BackAngle : Character.PathFollower.ForwardAngle;
			Character.MoveSpeed = Mathf.Abs(Character.MoveSpeed);

			isActive = false;
			isInteractingWithPlayer = false;
			Character.ResetMovementState();
		}
	}
}
