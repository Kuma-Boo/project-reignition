using Godot;

namespace Project.Core
{
	public class InputManager : Node
	{
		/*
		 Handles all input related code.
		 Things like remapping, saving and loading.
		 Also contains static buttons references
		 allowing an alternative to Godots default Input class
		*/

		public static Button debugHud;
		public static Button debugPause;
		public static Button debugAdvance;
		public static Button debugRestart;

		public static bool ignoreInputs; //Flag to ignore inputs until the next frame
		public static Controller controller;

		public override void _Ready()
		{
			PauseMode = PauseModeEnum.Process;
		}

		public override void _PhysicsProcess(float delta)
		{
			ignoreInputs = false;
			controller.Update(delta);

			//Debug keys
			if (!OS.IsDebugBuild())
				return;

			debugHud.Update(Input.IsKeyPressed((int)KeyList.Tab));
			debugPause.Update(Input.IsKeyPressed((int)KeyList.P));
			debugAdvance.Update(Input.IsKeyPressed((int)KeyList.F1));
			debugRestart.Update(Input.IsKeyPressed((int)KeyList.F5));
		}

		public static void DefaultControls()
		{
			controller = new Controller();
			controller.DefaultControls();
		}

		public struct Controller
		{
			public Axis horizontalAxis;
			public Axis verticalAxis;
			public Vector2 MovementAxis => new Vector2(horizontalAxis.value, verticalAxis.value);

			public Button jumpButton;
			public Button combatButton;
			public Button actionButton;
			public Button shieldButton;

			public Button pauseButton;
			public Mapping mapping;

			public bool isMobile; //True if using touch controls
			public bool isUsingGamepad => mapping.activeGamepad != -1; //True if the player is using a gamepad.

			public void Update(float delta)
			{
				//Update all inputs.
				if (isUsingGamepad && mapping.horizontalBinding != -1)
					horizontalAxis.Update(AnalogAxisHeld(mapping.horizontalBinding), delta);
				else
					horizontalAxis.Update(DigitalAxisHeld(mapping.rightBinding, mapping.leftBinding), delta);

				if (isUsingGamepad && mapping.verticalBinding != -1)
					verticalAxis.Update(AnalogAxisHeld(mapping.verticalBinding), delta);
				else
					verticalAxis.Update(DigitalAxisHeld(mapping.upBinding, mapping.downBinding), delta);

				jumpButton.Update(ButtonHeld(mapping.jumpBinding));
				combatButton.Update(ButtonHeld(mapping.combatBinding));
				actionButton.Update(ButtonHeld(mapping.actionBinding));

				pauseButton.Update(ButtonHeld(mapping.pauseBinding));
			}

			//Returns the current physical value of an analog axis (Gamepad Only)
			private float AnalogAxisHeld(int axisCode) => Input.GetJoyAxis(mapping.activeGamepad, axisCode);
			//Combines two buttons as a single axis
			private float DigitalAxisHeld(int p, int n)
			{
				float r = 0;
				if (ButtonHeld(p))
					r++;
				if (ButtonHeld(n))
					r--;
				return r;
			}
			//Returns the physical state of a button
			private bool ButtonHeld(int buttonCode) =>
			isUsingGamepad ? Input.IsJoyButtonPressed(mapping.activeGamepad, buttonCode) :
			Input.IsKeyPressed(buttonCode);

			//TODO Consoles will require default controls for gamepads
			public void DefaultControls() => DefaultKeyboardControls();
			public void DefaultKeyboardControls()
			{
				//Default to keyboard
				mapping = new Mapping()
				{
					activeGamepad = -1,

					upBinding = (int)KeyList.Up,
					downBinding = (int)KeyList.Down,
					leftBinding = (int)KeyList.Left,
					rightBinding = (int)KeyList.Right,

					jumpBinding = (int)KeyList.C,
					actionBinding = (int)KeyList.X,
					combatBinding = (int)KeyList.Z,
					pauseBinding = (int)KeyList.Enter
				};
			}
		}

		public struct Button
		{
			public bool wasPressed;
			public bool isHeld;
			public bool wasReleased;

			public void Update(bool currentValue)
			{
				wasPressed = !isHeld && currentValue;
				wasReleased = isHeld && !currentValue;
				isHeld = currentValue;
			}
		}

		public struct Axis
		{
			public int sign;
			public float value;
			public bool tapped;
			public float tapTimer;

			private const float TAP_INTERVAL = .2f;

			public void Update(float currentValue, float timeDelta)
			{
				int oldDirection = sign;
				sign = Mathf.Sign(currentValue);
				tapped = sign != 0 && (oldDirection != sign || tapTimer == 0);

				if (tapped)
					tapTimer = TAP_INTERVAL;
				else if (tapTimer != 0)
					tapTimer = Mathf.MoveToward(tapTimer, 0, timeDelta);

				value = currentValue;
			}
		}

		[System.Serializable]
		public struct Mapping
		{
			//Contains a list of all the input bindings.
			public int activeGamepad; //The current gamepad being used. -1 for keyboard, indexing starts at 0.

			public int leftBinding;
			public int rightBinding;
			public int horizontalBinding; //For analog inputs, -1 if unused
			public int upBinding;
			public int downBinding;
			public int verticalBinding; //For analog inputs, -1 if unused
			public int jumpBinding;
			public int actionBinding;
			public int combatBinding;
			public int pauseBinding;

			public struct Binding
			{
				public enum BindingType
				{
					Digital,
					Analog
				}
				public BindingType type;
				public int binding;
			}
		}
	}
}