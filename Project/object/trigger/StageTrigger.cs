using Godot;
using Godot.Collections;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Extended Area3D node that can determine the direction the player enters.
	/// Automatically sets up signals for children that inherit from StageTriggerModule.
	/// </summary>
	[Tool]
	public partial class StageTrigger : Area3D
	{
		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = [ExtensionMethods.CreateProperty("OneShot", Variant.Type.Bool)];

			if (isOneShot)
				properties.Add(ExtensionMethods.CreateProperty("Respawn Mode", Variant.Type.Int, PropertyHint.Enum, respawnMode.EnumToString()));

			properties.Add(ExtensionMethods.CreateProperty("Trigger Mode", Variant.Type.Int, PropertyHint.Enum, triggerMode.EnumToString()));
			if (triggerMode != TriggerModes.OnExit)
				properties.Add(ExtensionMethods.CreateProperty("Enter Mode", Variant.Type.Int, PropertyHint.Enum, enterMode.EnumToString()));
			if (triggerMode != TriggerModes.OnEnter)
				properties.Add(ExtensionMethods.CreateProperty("Exit Mode", Variant.Type.Int, PropertyHint.Enum, exitMode.EnumToString()));

			return properties;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case "OneShot":
					return isOneShot;
				case "Respawn Mode":
					return (int)respawnMode;
				case "Trigger Mode":
					return (int)triggerMode;
				case "Enter Mode":
					return (int)enterMode;
				case "Exit Mode":
					return (int)exitMode;
			}

			return base._Get(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "OneShot":
					isOneShot = (bool)value;
					NotifyPropertyListChanged();
					break;
				case "Respawn Mode":
					respawnMode = (RespawnModes)(int)value;
					break;
				case "Trigger Mode":
					triggerMode = (TriggerModes)(int)value;
					NotifyPropertyListChanged();
					break;
				case "Enter Mode":
					enterMode = (ActivationMode)(int)value;
					break;
				case "Exit Mode":
					exitMode = (ActivationMode)(int)value;
					break;
				default:
					return false;
			}

			return true;
		}
		#endregion

		/// <summary> Only activate this StageTrigger once per respawn? </summary>
		private bool isOneShot;
		/// <summary> For keeping track of oneshot triggers. </summary>
		private bool wasTriggered;
		private RespawnModes respawnMode = RespawnModes.CheckpointBefore;
		private enum RespawnModes
		{
			CheckpointBefore, //Only respawn if the current checkpoint is BEFORE the object (Default)
			CheckpointAfter, //Only respawn if the current checkpoint AFTER the object
			Always, //Always respawn
			Disabled, //Never Respawn
		}

		private TriggerModes triggerMode = TriggerModes.OnEnter; //How should this area be activated?
		private enum TriggerModes
		{
			OnEnter, //Activate on enter
			OnExit, //Activate on exit
			OnStay, //Activate on enter, Deactivate on exit.
		}

		private ActivationMode enterMode = ActivationMode.MovingForward;
		private ActivationMode exitMode = ActivationMode.MovingBackward;
		private enum ActivationMode
		{
			BothWays, //Valid both ways
			MovingForward, //Only valid when the player leaves the trigger moving forward
			MovingBackward, //Only valid when the player leaves the trigger moving backward
		}

		[Signal]
		public delegate void ActivatedEventHandler();
		[Signal]
		public delegate void DeactivatedEventHandler();
		[Signal]
		public delegate void RespawnedEventHandler();
		private PlayerPathController PathFollower => StageSettings.Player.PathFollower;
		private bool isInteractingWithPlayer;

		public override void _Ready()
		{
			if (Engine.IsEditorHint()) return;

			//Connect child modules
			for (int i = 0; i < GetChildCount(); i++)
			{
				StageTriggerModule module = GetChildOrNull<StageTriggerModule>(i);
				if (module == null) continue;

				// Connect signals
				Activated += module.Activate;
				Deactivated += module.Deactivate;
				Respawned += module.Respawn;
			}

			if (respawnMode != RespawnModes.Disabled) //Connect respawn signal
				StageSettings.Instance.Respawned += Respawn;
		}

		public void Respawn()
		{
			if (isOneShot && respawnMode != RespawnModes.Always) //Validate respawn
			{
				//Compare the currentCheckpoint progress compared to this StageTrigger
				float eventPosition = PathFollower.GetProgress(GlobalPosition);
				float checkpointPosition = PathFollower.GetProgress(StageSettings.Instance.CurrentCheckpoint.GlobalPosition);
				bool isRespawningAhead = checkpointPosition > eventPosition;

				if ((respawnMode == RespawnModes.CheckpointBefore && isRespawningAhead) ||
				(respawnMode == RespawnModes.CheckpointAfter && !isRespawningAhead)) //Invalid Respawn
				{
					return;
				}
			}

			if (isInteractingWithPlayer)
				OnEntered();

			wasTriggered = false;
			EmitSignal(SignalName.Respawned);
		}

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player detection")) return;
			OnEntered();
		}

		public void OnExited(Area3D a)
		{
			if (!a.IsInGroup("player detection")) return;
			OnExited();
		}

		private void OnEntered()
		{
			isInteractingWithPlayer = true;

			//Determine whether activation is successful
			if (triggerMode == TriggerModes.OnExit)
				return;

			if (enterMode != ActivationMode.BothWays)
			{
				bool isEnteringForward = !PathFollower.IsAheadOfPoint(GlobalPosition);
				if ((enterMode == ActivationMode.MovingForward && !isEnteringForward) || (enterMode == ActivationMode.MovingBackward && isEnteringForward))
					return;
			}

			Activate();
		}

		private void OnExited()
		{
			isInteractingWithPlayer = false;

			//Determine whether deactivation is successful
			if (triggerMode == TriggerModes.OnEnter)
				return;

			if (exitMode != ActivationMode.BothWays)
			{
				bool isExitingForward = PathFollower.IsAheadOfPoint(GlobalPosition);
				if ((exitMode == ActivationMode.MovingForward && !isExitingForward) || (exitMode == ActivationMode.MovingBackward && isExitingForward))
					return;
			}

			if (triggerMode == TriggerModes.OnExit)
				Activate();
			else
				Deactivate();
		}

		private void Activate()
		{
			if (wasTriggered) return;

			if (isOneShot)
				wasTriggered = true;

			EmitSignal(SignalName.Activated);
		}

		private void Deactivate() => EmitSignal(SignalName.Deactivated);

		public void EnableMonitoring() => Monitoring = true;
		public void DisableMonitoring() => Monitoring = false;
	}
}
