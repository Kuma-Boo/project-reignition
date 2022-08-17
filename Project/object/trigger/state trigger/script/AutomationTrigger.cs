using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	public class AutomationTrigger : Area
	{
		[Export]
		public float distanceToTravel; //How far to travel. Set at 0 to travel the entire path
		[Export]
		public float minimumSpeedRatio = 1f;
		[Export]
		public NodePath automationPath;
		private Path _automationPath;
		[Export]
		public CameraSettingsResource cameraSettings;
		[Export]
		public float cameraBlend;

		private bool isEntered;
		private bool isProcessing;

		private float startingOffset;
		private float DistanceTraveled => Mathf.Abs(Character.PathFollower.Offset - startingOffset);
		private bool IsFinished => (!Mathf.IsZeroApprox(distanceToTravel) && DistanceTraveled >= distanceToTravel) || (_automationPath != null && Character.PathFollower.ActivePath != _automationPath);
		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			_automationPath = GetNodeOrNull<Path>(automationPath);
		}

		public override void _PhysicsProcess(float _)
		{
			if(isProcessing)
			{
				if(IsFinished)
					Deactivate();

				if (Character.MoveSpeed < Character.moveSettings.speed)
					Character.MoveSpeed = Character.moveSettings.speed;
				Character.PathFollower.Offset += Character.MoveSpeed * PhysicsManager.physicsDelta;
				return;
			}

			if (!isEntered) return;

			if (IsActivationValid())
				Activate();
		}

		private bool IsActivationValid()
		{
			if (!Character.IsOnGround || Character.SpeedRatio < minimumSpeedRatio) return false;
			return true;
		}

		private void Activate()
		{
			Character.PathFollower.SetActivePath(_automationPath);
			Character.StartExternal(Character.PathFollower, false);
			
			startingOffset = Character.PathFollower.Offset;
			isProcessing = true;

			UpdateCamera();
		}

		public void UpdateCamera()
		{
			if (cameraSettings != null)
				Character.Camera.SetCameraData(cameraSettings, cameraBlend);
		}

		private void Deactivate()
		{
			isProcessing = false;
			Character.CancelMovementState(CharacterController.MovementStates.External);
		}

		public void OnEntered(Area _) => isEntered = true;
		public void OnExited(Area _) => isEntered = false;
	}
}
