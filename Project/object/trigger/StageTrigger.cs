using Godot;
using Godot.Collections;

namespace Project.Gameplay.Triggers
{
	public class StageTrigger : Area
	{
		[Export]
		public bool isOneShot; //Only trigger once?
		private bool isTriggered;

		[Export]
		public TriggerMode triggerMode;
		public enum TriggerMode
		{
			OnEnter, //Activate on enter
			OnExit, //Activate on exit
			OnStay, //Enable on enter, disable on exit.
		}

		public enum InteractionMode
		{
			BothWays,
			MovingForward,
			MovingBackward,
		}

		[Export]
		public InteractionMode enterMode;
		[Export]
		public InteractionMode exitMode;
		private readonly Array<StageTriggerModule> _stageTriggerObjects = new Array<StageTriggerModule>();

		public override void _Ready()
		{
			//TODO reset oneshot triggers when the stage is reloaded.

			//Get all stage trigger objects that are children of this node
			Array children = GetChildren();
			for (int i = 0; i < children.Count; i++)
			{
				if (children[i] is StageTriggerModule)
					_stageTriggerObjects.Add(children[i] as StageTriggerModule);
			}
		}

		public void OnEntered(Area a)
		{
			if (!a.IsInGroup("player")) return;

			if (triggerMode == TriggerMode.OnExit)
				return;

			if (enterMode != InteractionMode.BothWays)
			{
				bool isEnteringForward = !CharacterController.instance.PathFollower.IsAheadOfPoint(GlobalTranslation);
				if (enterMode == InteractionMode.MovingForward && !isEnteringForward)
					return;

				if (enterMode == InteractionMode.MovingBackward && isEnteringForward)
					return;
			}

			Activate();
		}

		public void OnExited(Area a)
		{
			if (!a.IsInGroup("player") || !CharacterController.instance.PathFollower.IsInsideTree()) return;

			bool isExitingForward = CharacterController.instance.PathFollower.IsAheadOfPoint(GlobalTranslation);

			switch (triggerMode)
			{
				case TriggerMode.OnExit:
					Activate();
					break;
				case TriggerMode.OnEnter: //Do Nothing
					break;
				default:
					if (exitMode == InteractionMode.MovingForward && !isExitingForward)
						break;
					
					if (exitMode == InteractionMode.MovingBackward && isExitingForward)
						break;

					Deactivate(isExitingForward);
					break;
			}
		}

		private void Activate()
		{
			if (isTriggered) return;
			isTriggered = isOneShot;

			for (int i = 0; i < _stageTriggerObjects.Count; i++)
				_stageTriggerObjects[i].Activate();
		}

		private void Deactivate(bool isMovingForward)
		{
			for (int i = 0; i < _stageTriggerObjects.Count; i++)
				_stageTriggerObjects[i].Deactivate(isMovingForward);
		}
	}
}