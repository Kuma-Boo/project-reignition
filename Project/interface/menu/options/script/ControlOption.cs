using Godot;
using Godot.Collections;
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
		public TextureRect spriteRect;
		[Export]
		public Texture2D keySprite;
		[Export]
		public Texture2D keyWideSprite;
		[Export]
		public ControllerSpriteResource[] controllerResources;

		[Export]
		public StringName inputID;
		public bool IsListeningForInputs { get; private set; }

		public override void _Ready()
		{
			actionLabel.Text = Tr(inputID.ToString());
			SaveConfig();
			RedrawBinding();
			Runtime.Instance.Connect(Runtime.SignalName.ControllerChanged, new(this, MethodName.RedrawBinding));
			Runtime.Instance.Connect(Runtime.SignalName.EventInputed, new(this, MethodName.ReceiveInput));
		}


		public void StartListening()
		{
			IsListeningForInputs = true;
			Input.ActionRelease("button_jump");
			RedrawBinding();
		}


		private async void StopListening()
		{
			await ToSignal(GetTree().CreateTimer(PhysicsManager.physicsDelta, false), SceneTreeTimer.SignalName.Timeout);
			IsListeningForInputs = false;
			RedrawBinding();
		}


		public void ReceiveInput(InputEvent e) => ReceiveInput(e, false);
		public void ReceiveInput(InputEvent e, bool swapInput)
		{
			if (!swapInput)
			{
				if (!IsListeningForInputs) return;
				if (!e.IsPressed() || e.IsEcho()) return; // Only listen for press
				if (!(e is InputEventKey || e is InputEventJoypadButton)) return; // Only listen for keys and button presses.

				if (!FilterInput(e)) return;
			}

			RemapInput(e);
			CallDeferred(MethodName.StopListening);
		}


		/// <summary> Remaps the target input to the given input event's binding. </summary>
		private void RemapInput(InputEvent e)
		{
			// Check for conflicting input mappings
			Array<StringName> actionList = InputMap.GetActions();
			StringName swapAction = null;

			for (int i = 0; i < actionList.Count; i++)
			{
				if (!SaveManager.Config.inputConfiguration.ContainsKey(actionList[i]))
					continue;

				if (InputMap.ActionHasEvent(actionList[i], e))
				{
					if (actionList[i] == inputID) return; // Nothing changed

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

			SaveConfig();
		}


		private void SaveConfig()
		{
			Array<InputEvent> eventList = InputMap.ActionGetEvents(inputID); // Refresh event list

			// Construct the mapping string
			int[] mappingList = { (int)Key.None, (int)JoyAxis.Invalid, (int)JoyButton.Invalid };
			foreach (InputEvent e in eventList)
			{
				if (e is InputEventKey)
					mappingList[0] = (int)(e as InputEventKey).Keycode;
				else if (e is InputEventJoypadMotion)
					mappingList[1] = (int)(e as InputEventJoypadMotion).Axis;
				else if (e is InputEventJoypadButton)
					mappingList[2] = (int)(e as InputEventJoypadButton).ButtonIndex;
			}
			string mappingString = $"{mappingList[0]}, {mappingList[1]}, {mappingList[2]}";

			if (SaveManager.Config.inputConfiguration.ContainsKey(inputID))
				SaveManager.Config.inputConfiguration[inputID] = mappingString;
			else
				SaveManager.Config.inputConfiguration.Add(inputID, mappingString);
		}


		/// <summary> Checks whether the input can be remapped to the target binding. </summary>
		private bool FilterInput(InputEvent e)
		{
			if (e is InputEventJoypadButton) // Exclude certain buttons (such as guides and d-pads)
			{
				switch ((e as InputEventJoypadButton).ButtonIndex)
				{
					case JoyButton.Guide:
						return false;
					case JoyButton.DpadUp:
						return false;
					case JoyButton.DpadDown:
						return false;
					case JoyButton.DpadLeft:
						return false;
					case JoyButton.DpadRight:
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


		public void RedrawBinding()
		{
			if (IsListeningForInputs)
			{
				inputLabel.Text = "...";
				inputLabel.Visible = true;
				spriteRect.Visible = false;
				return;
			}


			spriteRect.Visible = true;
			Array<InputEvent> eventList = InputMap.ActionGetEvents(inputID);

			if (Runtime.Instance.IsUsingController)
			{
				inputLabel.Visible = false;

				for (int i = 0; i < eventList.Count; i++)
				{
					if (eventList[i] is InputEventJoypadButton)
					{
						int index = (int)(eventList[i] as InputEventJoypadButton).ButtonIndex;
						spriteRect.Texture = controllerResources[(int)SaveManager.Config.controllerType].buttons[index];
						return;
					}
				}
			}

			// Keyboard input
			inputLabel.Visible = true;
			for (int i = 0; i < eventList.Count; i++)
			{
				if (eventList[i] is InputEventKey)
				{
					Key key = (eventList[i] as InputEventKey).Keycode;
					inputLabel.Text = Runtime.Instance.GetKeyLabel(key);
					spriteRect.Texture = inputLabel.Text.Length > 3 ? keyWideSprite : keySprite;
					break;
				}
			}
		}
	}
}
