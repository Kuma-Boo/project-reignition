using Godot;
using Project.Core;
using System;

namespace Project.Interface.Menus
{
	public partial class ControlOption : Control
	{
		[Export]
		public TextureRect sprite;
		[Export]
		public Label inputLabel;

		[Export]
		private StringName inputID;

		public override void _Ready()
		{
			GD.Print(InputMap.ActionGetEvents(inputID));

			InputEventKey e = new()
			{
				Keycode = Key.O
			};

			InputMap.ActionAddEvent(inputID, e);
		}


		public override void _Process(double _)
		{
			if (Input.IsActionJustPressed(inputID))
				GD.Print("Pressed!");
		}



		public void UpdateInput(InputEvent e)
		{
			InputMap.ActionEraseEvent(inputID, e);
		}


		public void Redraw()
		{
		}


		private string GetKeyLabel(Key key)
		{
			switch (key)
			{

				default:
					return key.ToString();
			}
		}
	}
}
