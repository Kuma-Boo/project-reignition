using Godot;
using Godot.Collections;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Extended Area node that can determine the direction the player enters.
	/// Automatically sets up signals for children that inherit from StageTriggerModule.
	/// </summary>
	public class StageTrigger : Area
	{
		[Export]
		public bool isOneShot; //Disables this trigger after being activated (Trigger mode must be set to OnEnter to function properly)
		private bool isTriggered; //For isOneShot

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
		public delegate void Activated();
		[Signal]
		public delegate void Deactivated();
		private CharacterPathFollower PathFollower => CharacterController.instance.PathFollower;

		public override void _EnterTree()
		{
			//Connect child modules
			Array children = GetChildren();
			for (int i = 0; i < children.Count; i++)
			{
				if (children[i] is StageTriggerModule)
				{
					StageTriggerModule module = children[i] as StageTriggerModule;

					//Connect signals
					Connect(nameof(Activated), module, nameof(StageTriggerModule.Activate));
					Connect(nameof(Deactivated), module, nameof(StageTriggerModule.Deactivate));

					//Register respawnable modules
					if (module.IsRespawnable())
						StageSettings.instance.RegisterRespawnableObject(module);
				}
			}
		}

		public void OnEnter() //Called from player
		{
			//Determine whether activation is successful
			if (triggerMode == TriggerMode.OnExit)
				return;

			if (enterMode != InteractionMode.BothWays)
			{
				bool isEnteringForward = !PathFollower.IsAheadOfPoint(GlobalTranslation) || CharacterController.instance.MoveSpeed > 0;
				if ((enterMode == InteractionMode.MovingForward && !isEnteringForward) || (enterMode == InteractionMode.MovingBackward && isEnteringForward))
					return;
			}

			Activate();
		}

		public void OnExit()
		{
			if (!PathFollower.IsInsideTree()) return;

			//Determine whether deactivation is successful
			if (triggerMode == TriggerMode.OnEnter)
				return;

			if (exitMode != InteractionMode.BothWays)
			{
				bool isExitingForward = PathFollower.IsAheadOfPoint(GlobalTranslation) || CharacterController.instance.MoveSpeed > 0;
				if ((exitMode == InteractionMode.MovingForward && !isExitingForward) || (exitMode == InteractionMode.MovingBackward && isExitingForward))
					return;
			}

			Deactivate();
		}

		private void Activate()
		{
			if (isTriggered) return;
			isTriggered = isOneShot;

			EmitSignal(nameof(Activated));
		}

		private void Deactivate()
		{
			EmitSignal(nameof(Deactivated));
		}
	}
}