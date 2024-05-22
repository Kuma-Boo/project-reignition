using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface
{
	public partial class NavigationButton : Node
	{
		[Export]
		private string actionKey;
		[Export]
		private string inputKey;
		[Export]
		private ControllerSpriteResource[] controllerResources;

		[ExportCategory("Components")]
		[Export]
		private Label buttonLabel;
		[Export]
		private TextureRect buttonTextureRect;
		[Export]
		private Label actionLabel;


		public override void _Ready()
		{
			Runtime.Instance.Connect(Runtime.SignalName.ControllerChanged, new(this, MethodName.Redraw));
			Redraw();
		}


		private void Redraw(int _) => Redraw();
		private void Redraw()
		{
			actionLabel.Text = Tr(actionKey);

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
				buttonLabel.Visible = false;

				if (motion == null)
				{
					buttonTextureRect.Texture = controllerResources[(int)SaveManager.Config.controllerType].buttons[(int)button.ButtonIndex];
					return;
				}

				int axis = (int)motion.Axis;
				axis = (axis * 2) + Mathf.Clamp(Mathf.RoundToInt(motion.AxisValue), 0, 1);

				buttonTextureRect.Texture = controllerResources[(int)SaveManager.Config.controllerType].axis[axis];
				return;
			}


			// Keyboard
			if (key == null) return;

			buttonLabel.Visible = true;
			buttonLabel.Text = Runtime.Instance.GetKeyLabel(key.Keycode);
			int keySpriteIndex = buttonLabel.Text.Length <= 3 ? 0 : 1;
			buttonTextureRect.Texture = controllerResources[^1].buttons[keySpriteIndex]; // Last controller resource should be the keyboard sprites
		}
	}
}
