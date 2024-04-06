using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	[Tool]
	public partial class GasTank : Area3D
	{

		[Export]
		private float height;
		[Export]
		private Vector3 endPosition;
		private Vector3 startPosition;

		[Export]
		private AnimationPlayer animator;

		private bool wasDetonated;
		private bool isInteractingWithPlayer;
		private bool isTraveling;
		private float travelTime;
		private const float TIME_SCALE = .5f;

		private CharacterController Character => CharacterController.instance;
		private Vector3 StartPosition => Engine.IsEditorHint() ? GlobalPosition : startPosition;
		private Vector3 EndPosition => StartPosition + GlobalBasis * endPosition;
		public LaunchSettings GetLaunchSettings() => LaunchSettings.Create(StartPosition, EndPosition, height);


		public override void _Ready()
		{
			startPosition = GlobalPosition;
			StageSettings.instance.ConnectRespawnSignal(this);
		}


		private void Respawn()
		{
			travelTime = 0;
			isTraveling = false;
			GlobalPosition = StartPosition;
			wasDetonated = false;
			animator.Play("RESET");
		}


		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint()) return;

			if (wasDetonated) return;
			if (!isTraveling && !CheckInteraction()) return;

			LaunchSettings launchSettings = GetLaunchSettings();

			if (launchSettings.IsLauncherFinished(travelTime))
			{
				if (!wasDetonated)
					Detonate();
				return;
			}

			travelTime = Mathf.MoveToward(travelTime, launchSettings.TotalTravelTime, PhysicsManager.physicsDelta * TIME_SCALE);
			GlobalPosition = launchSettings.InterpolatePositionTime(travelTime);
			GD.Print(launchSettings.TotalTravelTime, travelTime);
		}


		private bool CheckInteraction()
		{
			if (!isInteractingWithPlayer) return false;

			// TODO Check for stomp
			if (Character.Skills.IsSpeedBreakActive)
			{
				Detonate(); // Detonate instantly
				return false;
			}

			if (Character.ActionState != CharacterController.ActionStates.JumpDash) return false;

			Character.Lockon.StartBounce();
			isTraveling = true;
			animator.Play("strike");
			return true;
		}


		private void Detonate()
		{
			wasDetonated = true;
			isTraveling = false;
			animator.Play("detonate");
		}


		private void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			isInteractingWithPlayer = true;
		}


		private void OnExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			isInteractingWithPlayer = false;
		}
	}
}
