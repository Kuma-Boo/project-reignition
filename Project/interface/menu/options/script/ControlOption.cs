using Godot;
using Godot.Collections;
using Microsoft.VisualBasic;
using Project.Core;

namespace Project.Interface.Menus
{
	public partial class ControlOption : Control
	{
		[Signal]
		public delegate void SwapMappingEventHandler(StringName id, InputEvent e); // Emitted when a remap results in a mapping swap

		[Export]
		public Label actionLabel;
		[Export]
		public Label inputLabel;
		[Export]
		public Control awaitingInput;
		[Export]
		public TextureRect keyTextureRect;
		[Export]
		public TextureRect axisTextureRect;
		[Export]
		public TextureRect buttonTextureRect;
		[Export]
		public ControllerSpriteResource[] controllerResources;

		[Export]
		public StringName inputID;

		public bool IsReady => State == RemapState.READY;

		private RemapState State;
		private enum RemapState
		{
			READY,
			LISTENING,
			REBINDING,
		}

		public override void _Ready()
		{
			actionLabel.Text = Tr(inputID.ToString());
			SaveConfig();
			RedrawBinding();
			Runtime.Instance.Connect(Runtime.SignalName.ControllerChanged, new(this, MethodName.ControllerChanged));
			Runtime.Instance.Connect(Runtime.SignalName.EventInputed, new(this, MethodName.ReceiveInput));
		}


		public void StartListening()
		{
			State = RemapState.LISTENING;
			Input.ActionRelease("button_jump");
			RedrawBinding();
		}


		private async void StopListening()
		{
			await ToSignal(GetTree().CreateTimer(PhysicsManager.physicsDelta, false), SceneTreeTimer.SignalName.Timeout);
			State = RemapState.READY;
			RedrawBinding();
		}


		public void ReceiveInput(InputEvent e) => ReceiveInput(e, false);
		public void ReceiveInput(InputEvent e, bool swapInput)
		{
			if (!swapInput)
			{
				if (State != RemapState.LISTENING) return;
				if (!e.IsPressed() || e.IsEcho()) return; // Only listen for press
				if (!(e is InputEventKey || e is InputEventJoypadButton || e is InputEventJoypadMotion)) return; // Only listen for keys and button presses.

				if (!FilterInput(e)) return;
			}

			RemapInput(e);
			State = RemapState.REBINDING;
			StopListening();
			//CallDeferred(MethodName.StopListening);
		}


		/// <summary> Remaps the target input to the given input event's binding. </summary>
		private void RemapInput(InputEvent e)
		{
			if (e is InputEventJoypadMotion)
			{
				InputEventJoypadMotion denoisedEvent = e as InputEventJoypadMotion;
				denoisedEvent.AxisValue = Mathf.Sign(denoisedEvent.AxisValue);
				e = denoisedEvent;
			}

			// Check for conflicting input mappings
			Array<StringName> actionList = InputMap.GetActions();
			StringName swapAction = null;

			for (int i = 0; i < actionList.Count; i++)
			{
				if (!SaveManager.Config.inputConfiguration.ContainsKey(actionList[i]))
					continue;

				if (InputMap.ActionHasEvent(actionList[i], e))
				{
					if (actionList[i] == inputID)
					{
						EmitSignal(SignalName.SwapMapping, string.Empty, new());
						return; // Nothing changed
					}

					// Store conflict for a swap later
					swapAction = actionList[i];
				}
			}

			Array<InputEvent> eventList = InputMap.ActionGetEvents(inputID);

			for (int i = 0; i < eventList.Count; i++)
			{
				if (e.GetType() != eventList[i].GetType())
					continue;

				InputMap.ActionEraseEvent(inputID, eventList[i]); // Erase the old action

				// Resolve the mapping conflict by swapping input mapping with this menu option's mapping
				if (swapAction != null)
					EmitSignal(SignalName.SwapMapping, swapAction, eventList[i]);

				InputMap.ActionAddEvent(inputID, e); // Add the new action
				break;
			}

			EmitSignal(SignalName.SwapMapping, inputID, new());
			SaveConfig();
		}


		private void SaveConfig()
		{
			Array<InputEvent> eventList = InputMap.ActionGetEvents(inputID); // Refresh event list

			// Construct the mapping string
			int[] mappingList = { (int)Key.None, (int)JoyAxis.Invalid, (int)JoyButton.Invalid };
			int axisSign = 0;
			foreach (InputEvent e in eventList)
			{
				if (e is InputEventKey)
					mappingList[0] = (int)(e as InputEventKey).Keycode;
				else if (e is InputEventJoypadMotion)
				{
					mappingList[1] = (int)(e as InputEventJoypadMotion).Axis;
					axisSign = Mathf.Sign((e as InputEventJoypadMotion).AxisValue);
				}
				else if (e is InputEventJoypadButton)
					mappingList[2] = (int)(e as InputEventJoypadButton).ButtonIndex;
			}
			string mappingString = $"{mappingList[0]}, {mappingList[1]}, {mappingList[2]}, {axisSign}";

			if (SaveManager.Config.inputConfiguration.ContainsKey(inputID))
				SaveManager.Config.inputConfiguration[inputID] = mappingString;
			else
				SaveManager.Config.inputConfiguration.Add(inputID, mappingString);
		}


		/// <summary> Checks whether the input can be remapped to the target binding. </summary>
		private bool FilterInput(InputEvent e)
		{
			if (e is InputEventJoypadButton) // Exclude certain buttons (such as guides)
			{
				switch ((e as InputEventJoypadButton).ButtonIndex)
				{
					case JoyButton.Guide:
						return false;
				}
			}

			if (e is InputEventJoypadMotion) // Only allow joystick axis in a certain range
			{
				switch ((e as InputEventJoypadMotion).Axis)
				{
					case JoyAxis.LeftX:
						return true;
					case JoyAxis.LeftY:
						return true;
					case JoyAxis.RightX:
						return true;
					case JoyAxis.RightY:
						return true;
					default:
						return false;
				}
			}

			if (e is InputEventKey) // Only allow keys in a certain range
			{
				switch ((e as InputEventKey).Keycode)
				{
					case Key.Shift:
						return false;
					case Key.Ctrl:
						return false;
					case Key.Alt:
						return false;
					case Key.Escape:
						return false;
					case Key.Meta:
						return false;
					case Key.Numlock:
						return false;
				}
			}

			return true;
		}


		private void ControllerChanged(int _) => RedrawBinding();


		public void RedrawBinding()
		{
			keyTextureRect.Modulate = Colors.Transparent;
			buttonTextureRect.Modulate = Colors.Transparent;
			axisTextureRect.Modulate = Colors.Transparent;
			awaitingInput.Visible = State == RemapState.LISTENING;

			if (!IsReady)
				return;

			Array<InputEvent> eventList = InputMap.ActionGetEvents(inputID);

			for (int i = 0; i < eventList.Count; i++)
			{
				if (eventList[i] is InputEventJoypadButton)
				{
					buttonTextureRect.Modulate = Colors.White;

					JoyButton button = (eventList[i] as InputEventJoypadButton).ButtonIndex;
					buttonTextureRect.Texture = controllerResources[(int)SaveManager.Config.controllerType].buttons[(int)button];
					continue;
				}

				if (eventList[i] is InputEventJoypadMotion)
				{
					axisTextureRect.Modulate = Colors.White;

					int axis = (int)(eventList[i] as InputEventJoypadMotion).Axis;
					axis = (axis * 2) + Mathf.Clamp(Mathf.RoundToInt((eventList[i] as InputEventJoypadMotion).AxisValue), 0, 1);

					axisTextureRect.Texture = controllerResources[(int)SaveManager.Config.controllerType].axis[axis];
					continue;
				}

				if (eventList[i] is InputEventKey)
				{
					keyTextureRect.Modulate = Colors.White;

					Key key = (eventList[i] as InputEventKey).Keycode;
					inputLabel.Text = Runtime.Instance.GetKeyLabel(key);
					int keySpriteIndex = inputLabel.Text.Length <= 3 ? 0 : 1;
					keyTextureRect.Texture = controllerResources[^1].buttons[keySpriteIndex]; // Last controller resource should be the keyboard sprites
					continue;
				}
			}
		}
	}
}
