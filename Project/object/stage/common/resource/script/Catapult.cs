using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Launches the player a variable amount, using <see cref="launchPower"/> as the blend of close and far settings
	/// </summary>
	[Tool]
	public partial class Catapult : Node3D
	{
		[Export]
		public float closeDistance;
		[Export]
		public float closeMidHeight;
		[Export]
		public float closeEndHeight;
		[Export]
		public float farDistance;
		[Export]
		public float farMidHeight;
		[Export]
		public float farEndHeight;

		public CharacterController Character => CharacterController.instance;
		public InputManager.Controller Controller => Character.Controller;

		[Export(PropertyHint.Range, "0, 1")]
		public float launchPower; //0 <-> 1. Power of the shot. Exported for the editor
		private float launchPowerVelocity;
		private readonly float POWER_ADJUSTMENT_SPEED = .14f; //How fast to adjust the power
		private readonly float POWER_SMOOTHING_SPEED = .2f; //How fast to adjust the power

		[Export]
		private Node3D launchNode;
		[Export]
		private Node3D armNode;
		private Tween tweener;

		public LaunchSettings GetLaunchSettings()
		{
			Vector3 launchPoint = GetLaunchPosition();
			float distance = Mathf.Lerp(closeDistance, farDistance, launchPower);
			float midHeight = Mathf.Lerp(closeMidHeight, farMidHeight, launchPower);
			float endHeight = Mathf.Lerp(closeEndHeight, farEndHeight, launchPower);
			return LaunchSettings.Create(launchPoint, launchPoint + this.Forward() * distance + Vector3.Up * endHeight, midHeight);
		}

		private readonly float CLOSE_WINDUP_ANGLE = Mathf.DegToRad(45f);
		private Vector3 GetLaunchPosition() => GlobalPosition + this.Up() * 3.5f;

		private bool isEnteringCatapult;
		private bool isEjectingPlayer;
		private bool isControllingPlayer;
		private bool isProcessing;

		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint() || !isProcessing) return;

			if (isControllingPlayer)
			{
				if (isEjectingPlayer) //Launch the player at the right time
				{
					if (armNode.Rotation.X > Mathf.Pi * .5f)
						LaunchPlayer();
				}
				else //Update Controls
				{
					float targetLaunchPower = .5f + (Controller.verticalAxis.value * .5f);
					launchPower = ExtensionMethods.SmoothDamp(launchPower, targetLaunchPower, ref launchPowerVelocity, POWER_ADJUSTMENT_SPEED);

					if (Controller.jumpButton.wasPressed) //Cancel
						EjectPlayer(true);
					else if (Controller.actionButton.wasPressed) //Launch
						EjectPlayer(false);

					float targetRotation = Mathf.Lerp(CLOSE_WINDUP_ANGLE, 0, launchPower);
					armNode.Rotation = Vector3.Right * Mathf.Lerp(armNode.Rotation.X, targetRotation, POWER_SMOOTHING_SPEED);

					Character.UpdateExternalControl();
				}
			}
		}

		private void OnEnteredCatapult()
		{
			isControllingPlayer = true;
			isEnteringCatapult = false;
			Character.StartExternal(launchNode);
			launchPower = 1f;
			launchPowerVelocity = 0f;

			if (tweener != null)
				tweener.Kill();
		}

		private void EjectPlayer(bool isCancel)
		{
			isEjectingPlayer = true;

			tweener = CreateTween();

			if (isCancel)
			{
				tweener.TweenProperty(armNode, "rotation", Vector3.Zero, .2f * (1 - launchPower)).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Cubic);
				tweener.TweenCallback(new Callable(this, MethodName.CancelCatapult));
			}
			else
			{
				tweener.TweenProperty(armNode, "rotation", Vector3.Right * Mathf.Pi, .25f * (launchPower + 1)).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
				tweener.TweenProperty(armNode, "rotation", Vector3.Zero, .4f).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Cubic);
			}

			tweener.TweenCallback(new Callable(this, MethodName.StopProcessing));
		}

		private void StopProcessing() => isProcessing = false;

		private void LaunchPlayer()
		{
			isControllingPlayer = false;
			Character.StartLauncher(GetLaunchSettings(), null, true);
		}

		private void CancelCatapult()
		{
			if (!isControllingPlayer) return;
			isControllingPlayer = false;

			Vector3 destination = this.Back().RemoveVertical() * 2f + Vector3.Down * 2f;
			Character.JumpTo(new JumpSettings()
			{
				destination = Character.GlobalPosition + destination,
				jumpHeight = 1f,
			});
		}

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			isProcessing = true; //Start processing

			if (isEnteringCatapult) return; //Already entering catapult
			isEjectingPlayer = false; //Reset
			isEnteringCatapult = true;

			Character.Skills.IsSpeedBreakEnabled = Character.Skills.IsTimeBreakEnabled = false; //Disable break skills
			Character.Connect(CharacterController.SignalName.LaunchFinished, new Callable(this, MethodName.OnEnteredCatapult), (uint)ConnectFlags.OneShot);
			Character.JumpTo(new JumpSettings()
			{
				destination = launchNode.GlobalPosition,
				jumpHeight = 2f,
			});
		}
	}
}
