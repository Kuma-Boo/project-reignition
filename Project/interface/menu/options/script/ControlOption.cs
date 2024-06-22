using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus;

public partial class ControlOption : Control
{
	[Signal]
	public delegate void SwapMappingEventHandler(StringName id, InputEvent e); // Emitted when a remap results in a mapping swap

	[Export]
	private Label actionLabel;
	[Export]
	private Label inputLabel;
	[Export]
	private Control awaitingInput;
	[Export]
	private TextureRect keyTextureRect;
	[Export]
	private TextureRect axisTextureRect;
	[Export]
	private TextureRect buttonTextureRect;
	[Export(PropertyHint.ArrayType, "ControllerSpriteResource")]
	private ControllerSpriteResource[] controllerResources;

	[Export]
	public StringName InputId { get; private set; }

	public bool IsReady => state == RemapState.Ready;

	private RemapState state;
	private enum RemapState
	{
		Ready,
		Listening,
		Rebinding,
	}

	public override void _Ready()
	{
		actionLabel.Text = Tr(InputId.ToString());
		SaveConfig();
		RedrawBinding();
		Runtime.Instance.Connect(Runtime.SignalName.EventInputed, new(this, MethodName.ReceiveInput));
		Runtime.Instance.Connect(Runtime.SignalName.ControllerChanged, new(this, MethodName.ControllerChanged));
		SaveManager.Instance.Connect(SaveManager.SignalName.ConfigApplied, new(this, MethodName.RedrawBinding));
	}

	public override void _ExitTree()
	{
		Runtime.Instance.Disconnect(Runtime.SignalName.EventInputed, new(this, MethodName.ReceiveInput));
		Runtime.Instance.Disconnect(Runtime.SignalName.ControllerChanged, new(this, MethodName.ControllerChanged));
		SaveManager.Instance.Disconnect(SaveManager.SignalName.ConfigApplied, new(this, MethodName.RedrawBinding));
	}

	public void StartListening()
	{
		state = RemapState.Listening;
		Input.ActionRelease("button_jump");
		RedrawBinding();
	}

	private async void StopListening()
	{
		await ToSignal(GetTree().CreateTimer(PhysicsManager.physicsDelta, false), SceneTreeTimer.SignalName.Timeout);
		state = RemapState.Ready;
		RedrawBinding();
	}

	public void ReceiveInput(InputEvent e) => ReceiveInput(e, false);
	public void ReceiveInput(InputEvent e, bool isSwappedInput)
	{
		if (!isSwappedInput) // Only filter remaps, not swaps
		{
			if (state != RemapState.Listening) return;
			if (!e.IsPressed() || e.IsEcho()) return; // Only listen for press
			if (e is not (InputEventKey or InputEventJoypadButton or InputEventJoypadMotion)) return; // Only listen for keys and button presses.
			if (!FilterInput(e)) return;
		}

		RemapInput(e, isSwappedInput);
		state = RemapState.Rebinding;
		StopListening();
	}

	/// <summary> Remaps the target input to the given input event's binding. </summary>
	private void RemapInput(InputEvent e, bool isSwappedInput)
	{
		if (e is InputEventJoypadMotion denoisedEvent) // Snap sign
		{
			denoisedEvent.AxisValue = Mathf.Sign(denoisedEvent.AxisValue);
			e = denoisedEvent;
		}

		StringName swapAction = null;
		if (!isSwappedInput) // Only look for conflicts when not swapping for inputs
		{
			// Check for conflicting input mappings
			Array<StringName> actionList = InputMap.GetActions();

			for (int i = 0; i < actionList.Count; i++)
			{
				if (!SaveManager.Config.inputConfiguration.ContainsKey(actionList[i]))
					continue;

				if (!InputMap.ActionHasEvent(actionList[i], e)) continue;

				if (actionList[i] == InputId)
				{
					EmitSignal(SignalName.SwapMapping, string.Empty, new());
					return; // Nothing changed
				}

				// Store conflict for a swap later
				swapAction = actionList[i];
			}
		}

		Array<InputEvent> eventList = InputMap.ActionGetEvents(InputId);
		InputEvent swapEvent = null;

		GD.Print(e);
		for (int i = 0; i < eventList.Count; i++)
		{
			if (e.GetType() != eventList[i].GetType())
				continue;

			InputMap.ActionEraseEvent(InputId, eventList[i]); // Erase the old action

			if (e is InputEventKey key && key.Keycode == Key.None)
				break;
			else if (e is InputEventJoypadMotion motion && motion.Axis == JoyAxis.Max)
				break;
			else if (e is InputEventJoypadButton button && button.ButtonIndex == JoyButton.Max)
				break;

			swapEvent = eventList[i];
			InputMap.ActionAddEvent(InputId, e); // Add the new action
			break;
		}

		// Resolve mapping conflict by swapping input mapping with this menu option's mapping
		if (!isSwappedInput)
		{
			if (swapAction != null)
			{
				if (swapEvent == null)
				{
					if (e is InputEventKey)
					{
						swapEvent = new InputEventKey()
						{
							Keycode = Key.None
						};
					}
					else if (e is InputEventJoypadMotion)
					{
						swapEvent = new InputEventJoypadMotion()
						{
							Axis = JoyAxis.Max
						};
					}
					else if (e is InputEventJoypadButton)
					{
						swapEvent = new InputEventJoypadButton()
						{
							ButtonIndex = JoyButton.Max
						};
					}
				}

				EmitSignal(SignalName.SwapMapping, swapAction, swapEvent);
			}

			if (!InputMap.ActionHasEvent(InputId, e)) // Failed to add the new action
				InputMap.ActionAddEvent(InputId, e); // Add the new action anyway
		}

		SaveConfig();
	}

	private void SaveConfig()
	{
		Array<InputEvent> eventList = InputMap.ActionGetEvents(InputId); // Refresh event list

		// Construct the mapping string
		int[] mappingList = [(int)Key.None, (int)JoyAxis.Invalid, (int)JoyButton.Invalid];
		int axisSign = 0;
		foreach (var e in eventList)
		{
			if (e is InputEventKey key)
			{
				mappingList[0] = (int)key.Keycode;
			}
			else if (e is InputEventJoypadMotion motion)
			{
				mappingList[1] = (int)motion.Axis;
				axisSign = Mathf.Sign(motion.AxisValue);
			}
			else if (e is InputEventJoypadButton button)
			{
				mappingList[2] = (int)button.ButtonIndex;
			}
		}
		string mappingString = $"{mappingList[0]}, {mappingList[1]}, {mappingList[2]}, {axisSign}";

		if (SaveManager.Config.inputConfiguration.ContainsKey(InputId))
			SaveManager.Config.inputConfiguration[InputId] = mappingString;
		else
			SaveManager.Config.inputConfiguration.Add(InputId, mappingString);
	}

	/// <summary> Checks whether the input can be remapped to the target binding. </summary>
	private bool FilterInput(InputEvent e)
	{
		if (e is InputEventJoypadButton button) // Exclude certain buttons (such as guides)
		{
			switch (button.ButtonIndex)
			{
				case JoyButton.Guide:
					return false;
			}
		}

		if (e is InputEventJoypadMotion motion) // Only allow joystick axis in a certain range
		{
			if (Mathf.Abs(motion.AxisValue) < SaveManager.Config.deadZone)
				return false;

			switch (motion.Axis)
			{
				case JoyAxis.LeftX:
				case JoyAxis.LeftY:
				case JoyAxis.RightX:
				case JoyAxis.RightY:
				case JoyAxis.TriggerLeft:
				case JoyAxis.TriggerRight:
					return true;
				default:
					return false;
			}
		}

		if (e is InputEventKey key) // Only allow keys in a certain range
		{
			switch (key.Keycode)
			{
				case Key.Shift:
				case Key.Ctrl:
				case Key.Alt:
				case Key.Meta:
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
		awaitingInput.Visible = state == RemapState.Listening;

		if (!IsReady)
			return;

		Array<InputEvent> eventList = InputMap.ActionGetEvents(InputId);

		for (int i = 0; i < eventList.Count; i++)
		{
			if (eventList[i] is InputEventJoypadButton button1)
			{
				buttonTextureRect.Modulate = Colors.White;

				JoyButton button = button1.ButtonIndex;
				buttonTextureRect.Texture = controllerResources[(int)Runtime.Instance.GetActiveControllerType() - 1].buttons[(int)button];
				continue;
			}

			if (eventList[i] is InputEventJoypadMotion motion)
			{
				axisTextureRect.Modulate = Colors.White;
				int axis = Runtime.Instance.ControllerAxisToIndex(motion);
				axisTextureRect.Texture = controllerResources[(int)Runtime.Instance.GetActiveControllerType() - 1].axis[axis];
				continue;
			}

			if (eventList[i] is InputEventKey key)
			{
				keyTextureRect.Modulate = Colors.White;

				inputLabel.Text = Runtime.Instance.GetKeyLabel(key.Keycode);
				int keySpriteIndex = inputLabel.Text.Length <= 3 ? 0 : 1;
				keyTextureRect.Texture = controllerResources[^1].buttons[keySpriteIndex]; // Last controller resource should be the keyboard sprites
			}
		}
	}
}