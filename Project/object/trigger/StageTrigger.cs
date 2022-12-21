using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Extended Area3D node that can determine the direction the player enters.
	/// Automatically sets up signals for children that inherit from StageTriggerModule.
	/// </summary>
	public partial class StageTrigger : Area3D
	{
		[Export]
		public ActivationMode activationMode;
		public enum ActivationMode
		{
			Always, //Always activate this trigger
			Oneshot, //Only activate once per level load
			OneshotRespawnable, //Activate once each respawn
		}
		private bool wasTriggered; //For oneshot triggers

		[Export]
		public TriggerMode triggerMode; //How should this area be activated?
		public enum TriggerMode
		{
			OnEnter, //Activate on enter
			OnExit, //Activate on exit
			OnStay, //Activate on enter, Deactivate on exit.
		}

		[Export]
		public InteractionMode enterMode;
		[Export]
		public InteractionMode exitMode;
		public enum InteractionMode
		{
			BothWays,
			MovingForward,
			MovingBackward,
		}

		[Signal]
		public delegate void ActivatedEventHandler();
		[Signal]
		public delegate void DeactivatedEventHandler();
		private CharacterPathFollower PathFollower => CharacterController.instance.PathFollower;

		public override void _Ready()
		{
			//Connect child modules
			for (int i = 0; i < GetChildCount(); i++)
			{
				StageTriggerModule module = GetChildOrNull<StageTriggerModule>(i);
				if (module == null) continue;

				//Connect signals
				Connect(SignalName.Activated, new Callable(module, MethodName.Activate));
				Connect(SignalName.Deactivated, new Callable(module, MethodName.Deactivate));
			}

			if (activationMode == ActivationMode.OneshotRespawnable)
				StageSettings.instance.ConnectRespawnSignal(this);
		}

		public void Respawn() => wasTriggered = false;

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			//Determine whether activation is successful
			if (triggerMode == TriggerMode.OnExit)
				return;

			if (enterMode != InteractionMode.BothWays)
			{
				bool isEnteringForward = !PathFollower.IsAheadOfPoint(GlobalPosition);
				if ((enterMode == InteractionMode.MovingForward && !isEnteringForward) || (enterMode == InteractionMode.MovingBackward && isEnteringForward))
					return;
			}

			Activate();
		}

		public void OnExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			//Determine whether deactivation is successful
			if (triggerMode == TriggerMode.OnEnter)
				return;

			if (exitMode != InteractionMode.BothWays)
			{
				bool isExitingForward = PathFollower.IsAheadOfPoint(GlobalPosition);
				if ((exitMode == InteractionMode.MovingForward && !isExitingForward) || (exitMode == InteractionMode.MovingBackward && isExitingForward))
					return;
			}

			Deactivate();
		}

		private void Activate()
		{
			if (wasTriggered) return;

			if (activationMode != ActivationMode.Always)
				wasTriggered = true;

			GD.Print($"Activated {Name}");
			EmitSignal(SignalName.Activated);
		}

		private void Deactivate()
		{
			GD.Print($"Deactivated {Name}");
			EmitSignal(SignalName.Deactivated);
		}
	}
}