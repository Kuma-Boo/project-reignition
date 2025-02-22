using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus;

public partial class ControlOption : Control
{
	[Signal]
	public delegate void SwapMappingEventHandler(StringName id, InputEvent e); // Emitted when a remap results in a mapping swap

	[Export] public StringName InputId { get; private set; }

	[ExportGroup("Components")]
	[Export(PropertyHint.NodePathValidTypes, "Label")]
	private NodePath actionLabel;
	private Label ActionLabel { get; set; }
	[Export(PropertyHint.NodePathValidTypes, "Label")]
	private NodePath inputLabel;
	private Label InputLabel { get; set; }
	[Export(PropertyHint.NodePathValidTypes, "Control")]
	private NodePath awaitingInput;
	private Control AwaitingInput { get; set; }
	[Export(PropertyHint.NodePathValidTypes, "TextureRect")]
	private NodePath keyTextureRect;
	private TextureRect KeyTextureRect { get; set; }
	[Export(PropertyHint.NodePathValidTypes, "TextureRect")]
	private NodePath axisTextureRect;
	private TextureRect AxisTextureRect { get; set; }
	[Export(PropertyHint.NodePathValidTypes, "TextureRect")]
	private NodePath buttonTextureRect;
	private TextureRect ButtonTextureRect { get; set; }
	[Export(PropertyHint.ArrayType, "ControllerSpriteResource")]
	private ControllerSpriteResource[] controllerResources;

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
		ActionLabel = GetNodeOrNull<Label>(actionLabel);
		InputLabel = GetNodeOrNull<Label>(inputLabel);
		AwaitingInput = GetNodeOrNull<Control>(awaitingInput);
		KeyTextureRect = GetNodeOrNull<TextureRect>(keyTextureRect);
		AxisTextureRect = GetNodeOrNull<TextureRect>(axisTextureRect);
		ButtonTextureRect = GetNodeOrNull<TextureRect>(buttonTextureRect);

		ActionLabel.Text = Tr(InputId.ToString());

		SaveConfig();
		RedrawBinding();

		Runtime.Instance.EventInputed += ReceiveInput;
		Runtime.Instance.ControllerChanged += ControllerChanged;
		SaveManager.Instance.ConfigApplied += RedrawBinding;
	}

	public override void _ExitTree()
	{
		Runtime.Instance.EventInputed -= ReceiveInput;
		Runtime.Instance.ControllerChanged -= ControllerChanged;
		SaveManager.Instance.ConfigApplied -= RedrawBinding;
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
	public void ReceiveInput(InputEvent e, bool isSwappingInput)
	{
		if (!isSwappingInput) // Only filter remaps, not swaps
		{
			if (state != RemapState.Listening) return;
			if (!e.IsPressed() || e.IsEcho()) return; // Only listen for press
			if (e is not (InputEventKey or InputEventJoypadButton or InputEventJoypadMotion)) return; // Only listen for keys and button presses.

			// Allow user to cancel remapping if ESC is pressed or the action already has the target event
			if ((e is InputEventKey && (e as InputEventKey).Keycode == Key.Escape) ||
				InputMap.ActionHasEvent(InputId, e))
			{
				StopListening();
				return;
			}

			if (!FilterInput(e)) return;
		}

		RemapInput(e, isSwappingInput);
		state = RemapState.Rebinding;
		StopListening();
	}

	/// <summary> Remaps the target input to the given input event's binding. </summary>
	private void RemapInput(InputEvent e, bool isSwappingInput)
	{
		if (e is InputEventJoypadMotion denoisedEvent) // Snap sign
		{
			denoisedEvent.AxisValue = Mathf.Sign(denoisedEvent.AxisValue);
			e = denoisedEvent;
		}

		StringName swapAction = GetActionConflict(isSwappingInput, e);
		InputEvent swapEvent = RemapEvent(e);

		// Resolve mapping conflict by swapping input mapping with this menu option's mapping
		if (!isSwappingInput)
			ResolveMappingConflicts(swapAction, swapEvent, e);

		SaveConfig();
	}

	private StringName GetActionConflict(bool isSwappingInput, InputEvent e)
	{
		// Only look for conflicts when not swapping inputs
		if (isSwappingInput)
			return null;

		// Check for conflicting input mappings
		foreach (StringName actionId in InputMap.GetActions())
		{
			if (!SaveManager.Config.inputConfiguration.ContainsKey(actionId) || !InputMap.ActionHasEvent(actionId, e))
				continue;

			// Store conflict for a swap later
			return actionId;
		}

		return null;
	}

	/// <summary> Remaps the InputAction's event and returns the InputEvent for possible swapping. </summary>
	private InputEvent RemapEvent(InputEvent e)
	{
		Array<InputEvent> eventList = InputMap.ActionGetEvents(InputId);
		for (int i = 0; i < eventList.Count; i++)
		{
			if (e.GetType() != eventList[i].GetType())
				continue;

			InputMap.ActionEraseEvent(InputId, eventList[i]); // Erase the old action

			if ((e is InputEventKey key && key.Keycode == Key.None) ||
				(e is InputEventJoypadMotion motion && motion.Axis == JoyAxis.Max) ||
				(e is InputEventJoypadButton button && button.ButtonIndex == JoyButton.Max))
			{
				break;
			}

			InputMap.ActionAddEvent(InputId, e); // Add the new action
			return eventList[i];
		}

		return null;
	}

	private void ResolveMappingConflicts(StringName swapAction, InputEvent swapEvent, InputEvent e)
	{
		if (swapAction != null)
		{
			if (swapEvent == null)
			{
				if (e is InputEventKey)
					swapEvent = new InputEventKey() { Keycode = Key.None };
				else if (e is InputEventJoypadMotion)
					swapEvent = new InputEventJoypadMotion() { Axis = JoyAxis.Max };
				else if (e is InputEventJoypadButton)
					swapEvent = new InputEventJoypadButton() { ButtonIndex = JoyButton.Max };
			}

			EmitSignal(SignalName.SwapMapping, swapAction, swapEvent);
		}

		if (!InputMap.ActionHasEvent(InputId, e)) // Failed to add the new action
			InputMap.ActionAddEvent(InputId, e); // Add the new action anyway
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

		SaveManager.ApplyConfig();
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
				case Key.Alt:
				case Key.Meta:
				case Key.Escape:
				case Key.Space:
				case Key.Numlock:
					return false;
			}
		}

		return true;
	}

	private void ControllerChanged(int _) => RedrawBinding();

	public void RedrawBinding()
	{
		KeyTextureRect.Modulate = Colors.Transparent;
		ButtonTextureRect.Modulate = Colors.Transparent;
		AxisTextureRect.Modulate = Colors.Transparent;
		AwaitingInput.Visible = state == RemapState.Listening;

		if (!IsReady)
			return;

		Array<InputEvent> eventList = InputMap.ActionGetEvents(InputId);

		for (int i = 0; i < eventList.Count; i++)
		{
			if (eventList[i] is InputEventJoypadButton button1)
			{
				ButtonTextureRect.Modulate = Colors.White;

				JoyButton button = button1.ButtonIndex;
				ButtonTextureRect.Texture = controllerResources[(int)Runtime.Instance.GetActiveControllerType() - 1].buttons[(int)button];
				continue;
			}

			if (eventList[i] is InputEventJoypadMotion motion)
			{
				AxisTextureRect.Modulate = Colors.White;
				int axis = Runtime.Instance.ControllerAxisToIndex(motion);
				AxisTextureRect.Texture = controllerResources[(int)Runtime.Instance.GetActiveControllerType() - 1].axis[axis];
				continue;
			}

			if (eventList[i] is InputEventKey key)
			{
				KeyTextureRect.Modulate = Colors.White;

				InputLabel.Text = Runtime.Instance.GetKeyLabel(key.Keycode);
				int keySpriteIndex = InputLabel.Text.Length <= 3 ? 0 : 1;
				KeyTextureRect.Texture = controllerResources[^1].buttons[keySpriteIndex]; // Last controller resource should be the keyboard sprites
			}
		}
	}
}