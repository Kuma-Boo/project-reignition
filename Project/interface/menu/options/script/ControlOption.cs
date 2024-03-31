using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus
{
	public partial class ControlOption : Control
	{
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
		private StringName inputID;
		private bool isListeningForInputs;


		public override void _Ready()
		{
			actionLabel.Text = Tr(inputID.ToString());
			RedrawBinding();
			Runtime.Instance.Connect(Runtime.SignalName.ControllerChanged, new(this, MethodName.RedrawBinding));
		}


		public void StartListening()
		{
			isListeningForInputs = true;

			inputLabel.Text = "...";
			inputLabel.Visible = true;
			spriteRect.Visible = false;
		}


		private void StopListening()
		{
			isListeningForInputs = false;
			RedrawBinding();
		}


		public override void _Input(InputEvent e)
		{
			if (!isListeningForInputs) return;
			if (!e.IsPressed()) return; // Only listen for PRESSES
			if (!(e is InputEventKey || e is InputEventJoypadButton)) return; // Only listen for keys and button presses.

			if (!ValidateInput(e))
				StopListening();
		}


		/// <summary> Remaps the target input to the given input event's binding. </summary>
		private void RemapInput(InputEvent inputEvent)
		{
			// Check for conflicting input mappings
			Array<StringName> actionList = InputMap.GetActions();
			Array<InputEvent> eventList = InputMap.ActionGetEvents(inputID);

			for (int i = 0; i < actionList.Count; i++)
			{
				if (!InputMap.ActionHasEvent(actionList[i], inputEvent))
					continue;

				// Resolve the mapping conflict by swapping input mapping with this menu option's mapping
				foreach (InputEvent e in eventList)
				{
					if (inputEvent.GetType() != e.GetType())
						continue;

					InputMap.ActionEraseEvent(actionList[i], inputEvent);
					InputMap.ActionAddEvent(actionList[i], e);
					break;
				}
				break;
			}


			for (int i = 0; i < eventList.Count; i++)
			{
				if (inputEvent.GetType() != eventList[i].GetType())
					continue;

				InputMap.ActionEraseEvent(inputID, eventList[i]); // Erase the old action
				InputMap.ActionAddEvent(inputID, inputEvent); // Add the new action
				eventList = InputMap.ActionGetEvents(inputID); // Refresh event list
				break;
			}

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
			SaveManager.Config.inputConfiguration[inputID] = mappingString;
			RedrawBinding();
		}


		/// <summary> Checks whether the input can be remapped to the target binding. </summary>
		private bool ValidateInput(InputEvent e)
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


		private void RedrawBinding()
		{
			isListeningForInputs = false;
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
					Key key = (eventList[i] as InputEventKey).PhysicalKeycode;
					inputLabel.Text = Runtime.Instance.GetKeyLabel(key);
					spriteRect.Texture = inputLabel.Text.Length > 3 ? keyWideSprite : keySprite;
					break;
				}
			}
		}
	}
}
