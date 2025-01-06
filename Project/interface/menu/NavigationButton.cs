using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface;

public partial class NavigationButton : Control
{
	[Export] public StringName ActionKey { get; set; }
	[Export] private StringName inputKey;

	/// <summary> Set this to something if you only want to display a particular keyboard input. </summary>
	[Export] private Key overrideKey = Key.None;

	[Export(PropertyHint.ArrayType, "ControllerSpriteResource")]
	private ControllerSpriteResource[] controllerResources;

	[ExportCategory("Components")]
	[Export(PropertyHint.NodePathValidTypes, "Label")]
	private NodePath buttonLabel;
	private Label ButtonLabel { get; set; }
	[Export(PropertyHint.NodePathValidTypes, "TextureRect")]
	private NodePath buttonTextureRect;
	private TextureRect ButtonTextureRect { get; set; }
	[Export(PropertyHint.NodePathValidTypes, "Label")]
	private NodePath actionLabel;
	private Label ActionLabel { get; set; }

	public override void _Ready()
	{
		ButtonLabel = GetNodeOrNull<Label>(buttonLabel);
		ButtonTextureRect = GetNodeOrNull<TextureRect>(buttonTextureRect);
		ActionLabel = GetNodeOrNull<Label>(actionLabel);

		Runtime.Instance.ControllerChanged += Redraw;
		SaveManager.Instance.ConfigApplied += Redraw;
		Redraw();
	}

	public override void _ExitTree()
	{
		Runtime.Instance.ControllerChanged -= Redraw;
		SaveManager.Instance.ConfigApplied -= Redraw;
	}

	private void Redraw(int _) => Redraw();
	private void Redraw()
	{
		ActionLabel.Text = Tr(ActionKey);

		if (overrideKey != Key.None)
		{
			RedrawAsKeyboard(overrideKey);
			return;
		}

		Array<InputEvent> eventList = InputMap.ActionGetEvents(inputKey);

		InputEventKey key = null;
		InputEventJoypadButton button = null;
		InputEventJoypadMotion motion = null;

		for (int i = 0; i < eventList.Count; i++)
		{
			if (eventList[i] is InputEventKey)
				key = eventList[i] as InputEventKey;
			else if (eventList[i] is InputEventJoypadButton)
				button = eventList[i] as InputEventJoypadButton;
			else if (eventList[i] is InputEventJoypadMotion)
				motion = eventList[i] as InputEventJoypadMotion;
		}

		if (Runtime.Instance.IsUsingController)
		{
			if (button == null && motion == null) return;
			ButtonLabel.Visible = false;

			int controllerIndex = (int)Runtime.Instance.GetActiveControllerType() - 1;

			if (button != null) // Prioritize using buttons over axis icons
			{
				ButtonTextureRect.Texture = controllerResources[controllerIndex].buttons[(int)button.ButtonIndex];
				return;
			}

			int axis = Runtime.Instance.ControllerAxisToIndex(motion);
			ButtonTextureRect.Texture = controllerResources[controllerIndex].axis[axis];
			return;
		}

		if (key == null) return;
		RedrawAsKeyboard(key.Keycode);
	}

	private void RedrawAsKeyboard(Key keycode)
	{
		ButtonLabel.Visible = true;
		ButtonLabel.Text = Runtime.Instance.GetKeyLabel(keycode);
		int keySpriteIndex = ButtonLabel.Text.Length <= 3 ? 0 : 1;
		ButtonTextureRect.Texture = controllerResources[^1].buttons[keySpriteIndex]; // Last controller resource should be the keyboard sprites
	}
}