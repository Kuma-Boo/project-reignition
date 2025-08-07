using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface;

public partial class NavigationButton : Control
{
	[Export] public StringName ActionKey { get; set; }
	[Export] private StringName inputKey;
	[Export] private StringName fallbackKey; // Mostly used for menu navigation

	/// <summary> Set this to something if you only want to display a particular keyboard input. </summary>
	[Export] private Key overrideKey = Key.None;

	[Export(PropertyHint.ArrayType, "ControllerSpriteResource")]
	private ControllerSpriteResource[] controllerResources;

	[Export(PropertyHint.ArrayType, "ControllerSpriteResource")]
	private ControllerSpriteResource[] controllerResourcesStyle2;

	private ControllerSpriteResource GetActiveSpriteResource(int controllerIndex)
	{
		if (SaveManager.Config.buttonStyle == SaveManager.ButtonStyle.Style1)
			return controllerResources[controllerIndex];

		return controllerResourcesStyle2[controllerIndex];
	}


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

	[Export] private LabelSettings[] keyboardLabelSettings;

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

		if (RedrawAs(inputKey))
			return;

		// Failed to draw -- try again with fallback
		RedrawAs(fallbackKey);
	}

	private bool RedrawAs(StringName eventKey)
	{
		Array<InputEvent> eventList = InputMap.ActionGetEvents(eventKey);

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
			if (button == null && motion == null)
				return false;

			ButtonLabel.Visible = false;
			int controllerIndex = (int)Runtime.Instance.GetActiveControllerType() - 1;

			if (button != null) // Prioritize using buttons over axis icons
			{
				ButtonTextureRect.Texture = GetActiveSpriteResource(controllerIndex).buttons[(int)button.ButtonIndex];
				return true;
			}

			int axis = Runtime.Instance.ControllerAxisToIndex(motion);
			ButtonTextureRect.Texture = GetActiveSpriteResource(controllerIndex).axis[axis];
			return true;
		}

		if (key == null)
			return false;

		RedrawAsKeyboard(key.Keycode);
		return true;
	}

	private void RedrawAsKeyboard(Key keycode)
	{
		ButtonLabel.LabelSettings = keyboardLabelSettings[(int)SaveManager.Config.buttonStyle];
		ButtonLabel.Visible = true;
		ButtonLabel.Text = Runtime.Instance.GetKeyLabel(keycode);
		bool isShortButton = ButtonLabel.Text.Length <= 1;
		int keySpriteIndex = isShortButton ? 0 : 1;
		ButtonLabel.Scale = isShortButton ? Vector2.One * 1.5f : Vector2.One;
		ButtonTextureRect.Texture = GetActiveSpriteResource(controllerResources.Length - 1).buttons[keySpriteIndex]; // Last controller resource should be the keyboard sprites
	}
}
