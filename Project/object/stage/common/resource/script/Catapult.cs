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
		private readonly float POWER_RESET_SPEED = .8f; //How fast to adjust the power

		[Export]
		public NodePath launchNode;
		public Node3D _launchNode;
		[Export]
		public NodePath armNode;
		public Node3D _armNode;
		[Export]
		public NodePath animator;
		public AnimationPlayer _animator;

		public LaunchData GetLaunchData()
		{
			Vector3 launchPoint = GetLaunchPosition();
			float distance = Mathf.Lerp(closeDistance, farDistance, launchPower);
			float midHeight = Mathf.Lerp(closeMidHeight, farMidHeight, launchPower);
			float endHeight = Mathf.Lerp(closeEndHeight, farEndHeight, launchPower);
			return LaunchData.Create(launchPoint, launchPoint + this.Forward() * distance + Vector3.Up * endHeight, midHeight);
		}

		private readonly float CLOSE_WINDUP_ANGLE = Mathf.DegToRad(-45f);
		private Vector3 GetLaunchPosition() => GlobalPosition + this.Up() * 3.5f;

		private bool isEnteringCatapult;
		private bool isEjectingPlayer;
		private bool isControllingPlayer;

		public override void _EnterTree()
		{
			_armNode = GetNode<Node3D>(armNode);
			_launchNode = GetNode<Node3D>(launchNode);
			_animator = GetNode<AnimationPlayer>(animator);
		}

		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint())
			{
				/*
				//preview power in the editor?
				 * 
					if (_armNode != null)
						_armNode.RotationDegrees = Vector3.Right * Mathf.Lerp(CLOSE_WINDUP_ANGLE, 0, launchPower);
				*/

				return;
			}

			Vector3 targetRotation = Vector3.Zero;
			if (isControllingPlayer)
			{
				if (isEjectingPlayer)
				{

				}
				else //Update Controls
				{
					float targetLaunchPower = .5f - (Controller.verticalAxis.value * .5f);
					launchPower = ExtensionMethods.SmoothDamp(launchPower, targetLaunchPower, ref launchPowerVelocity, POWER_ADJUSTMENT_SPEED);
					targetRotation = Vector3.Right * Mathf.Lerp(CLOSE_WINDUP_ANGLE, 0, launchPower);

					if (Controller.jumpButton.wasPressed) //Cancel
						EjectPlayer(true);
					else if (Controller.actionButton.wasPressed) //Launch
						EjectPlayer(false);

					_armNode.Rotation = _armNode.Rotation.Lerp(targetRotation, POWER_SMOOTHING_SPEED);
				}
			}
			else
				_armNode.Rotation = _armNode.Rotation.Lerp(targetRotation, POWER_RESET_SPEED);
		}

		private void OnEnteredCatapult()
		{
			isControllingPlayer = true;
			isEnteringCatapult = false;
			Character.StartExternal(_launchNode, true);
			launchPower = 0f;
			launchPowerVelocity = 0f;
		}

		private void EjectPlayer(bool isCancel)
		{
			isEjectingPlayer = true;
			_animator.Play(isCancel ? "Cancel" : "Launch");
		}

		private void LaunchPlayer()
		{
			isControllingPlayer = false;
			//Character.Animator.ResetLocalRotation();
			Character.StartLauncher(GetLaunchData(), null, true);
		}

		private void CancelCatapult()
		{
			if (!isControllingPlayer) return;

			isControllingPlayer = false;

			Vector3 destination = this.Back().RemoveVertical() * 2f + Vector3.Down * 2f;
			Character.JumpTo(Character.GlobalPosition + destination, 1f);
		}

		public void PlayerEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			if (isEnteringCatapult) return; //Already entering catapult

			isEjectingPlayer = false; //Reset
			isEnteringCatapult = true;

			Character.Skills.IsSpeedBreakEnabled = Character.Skills.IsTimeBreakEnabled = false; //Disable break skills
			Character.JumpTo(_launchNode.GlobalPosition, 2f);
			Character.Connect(CharacterController.SignalName.LauncherFinished, new Callable(this, MethodName.OnEnteredCatapult), (uint)ConnectFlags.OneShot);
		}
	}
}
