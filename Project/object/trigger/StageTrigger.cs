using Godot;
using Godot.Collections;

namespace Project.Gameplay
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

			//NOTE that these are calculated using the character's current travel direction. This can be disabled using "useAbsoluteDirection"
			DisableOnExit, //Enable on enter, disable on exit.
		}

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

			Activate();
		}

		public void OnExited(Area a)
		{
			if (!a.IsInGroup("player")) return;

			Path activePath = CharacterController.instance.ActivePath;
			Curve3D pathCurve = activePath.Curve;
			float characterOffset = pathCurve.GetClosestOffset(CharacterController.instance.GlobalTransform.origin - activePath.GlobalTransform.origin);
			float triggerOffset = pathCurve.GetClosestOffset(GlobalTransform.origin - activePath.GlobalTransform.origin);

			switch (triggerMode)
			{
				case TriggerMode.OnExit:
					Activate();
					break;
				case TriggerMode.OnEnter: //Do Nothing
					break;
				default:
					Deactivate(Mathf.Sign(characterOffset - triggerOffset) > 0);
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