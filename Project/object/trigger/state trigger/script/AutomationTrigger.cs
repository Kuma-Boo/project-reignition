using Godot;

namespace Project.Gameplay
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

		private bool isEntered;
		private bool isActive;

		private float startingOffset;
		private float DistanceTraveled => Mathf.Abs(Character.PathFollower.Offset - startingOffset);
		private bool IsFinished => (!Mathf.IsZeroApprox(distanceToTravel) && DistanceTraveled >= distanceToTravel) || (_automationPath != null && Character.ActivePath != _automationPath);
		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			_automationPath = GetNodeOrNull<Path>(automationPath);
		}

		public override void _PhysicsProcess(float _)
		{
			if(isActive)
			{
				if(IsFinished)
					Deactivate();

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
			Character.SetActivePath(_automationPath);
			Character.StartAutomation();
			
			startingOffset = Character.PathFollower.Offset;
			isActive = true;
		}

		private void Deactivate()
		{
			isActive = false;
			Character.StopAutomation();
		}

		public void OnEntered(Area _) => isEntered = true;
		public void OnExited(Area _) => isEntered = false;
	}
}
