using Godot;

namespace Project.Gameplay
{
	public class SettingsTrigger : StageObject
	{
		[Export]
		public TriggerMode triggerMode;
		public enum TriggerMode
		{
			OnStay, //Enabled on enter, Disabled on exit

			//NOTE that these are calculated using the character's current travel direction. This can be disabled using "useAbsoluteDirection"
			DisableOnReverse, //Triggers when entering, but only disables when stage progress is NEGATIVE.
			DisableOnForward, //Triggers when entering, but only disables when stage progress is POSITIVE.

			//(A separate trigger needed to disable these two)
			OnEnter, //Only trigger when entering
			OnExit, //Only trigger when exiting
		}
		[Export]
		public bool useAbsoluteDirection; //Turn this on to make "DisableOnForward" & "DisableOnReverse" calculate using the path's global direction.

		[Export]
		public bool modifyControlLockout;
		[Export]
		public ControlLockoutResource lockoutData; //Leave empty to make this a RESET trigger.

		[Export]
		public bool modifyCamera;
		[Export]
		public CameraSettingsResource cameraData; //Leave empty to make this a RESET trigger.

		public override bool IsRespawnable() => false;
		public override void OnEnter()
		{
			if (triggerMode == TriggerMode.OnExit)
				return;

			Activate();
		}

		public override void OnExit()
		{
			if (triggerMode == TriggerMode.OnExit)
			{
				Activate();
				return;
			}

			if (triggerMode == TriggerMode.OnStay)
			{
				Deactivate();
				return;
			}

			if (triggerMode == TriggerMode.DisableOnReverse || triggerMode == TriggerMode.DisableOnForward)
			{
				int disableDirection = triggerMode == TriggerMode.DisableOnForward ? -1 : 1;
			}
		}

		private void Activate()
		{
			if (modifyControlLockout)
			{
				if (lockoutData == null)
					Character.ResetControlLockout();
				else
					Character.SetControlLockout(lockoutData);
			}

			if (modifyCamera)
			{
				if (cameraData == null)
					CameraController.instance.ResetCameraData();
				else
					CameraController.instance.SetCameraData(cameraData);
			}
		}

		private void Deactivate()
		{
			if (modifyControlLockout)
				Character.ResetControlLockout();
		}
	}
}
