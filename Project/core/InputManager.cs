using Godot;

namespace Project.Core
{
	public partial class InputManager : Node
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
		public static Button debugTurbo;

		public static bool ignoreInputs; //Flag to ignore inputs until the next frame
		public static Controller controller;

		public override void _PhysicsProcess(double _)
		{
			ignoreInputs = false;
			controller.Update(PhysicsManager.physicsDelta);

			//Debug keys
			if (!OS.IsDebugBuild())
				return;

			debugHud.Update(Input.IsKeyPressed(Key.Tab));
			debugPause.Update(Input.IsKeyPressed(Key.P));
			debugAdvance.Update(Input.IsKeyPressed(Key.Equal));
			debugRestart.Update(Input.IsKeyPressed(Key.F5));
			debugTurbo.Update(Input.IsKeyPressed(Key.Tab));
		}

		public override void _Input(InputEvent e) //For Controller/Keyboard hotswitching
		{
			if (e is InputEventJoypadButton && !controller.IsUsingGamepad)
				controller.activeGamepad = e.Device;
			else if (e is InputEventKey && controller.IsUsingGamepad)
				controller.activeGamepad = -1;

			e.Dispose();
		}

		public static void DefaultControls()
		{
			controller = new Controller();
			controller.DefaultControls();
		}

		public struct Controller
		{
			/// <summary> Horizontal input axis. </summary>
			public Axis horizontalAxis;
			/// <summary> Vertical input axis. </summary>
			public Axis verticalAxis;
			/// <summary> Modifer button (for keyboards). </summary>
			public Button movementModifier;
			/// <summary> Move inputs, clamped to a length of 1. </summary>
			public Vector2 MovementAxis { get; private set; }
			/// <summary> How much is the stick being pressed? </summary>
			public float MovementAxisLength { get; private set; }
			/// <summary> Is the control stick currently not holding a direction? </summary>
			public bool IsHoldingNeutral { get; private set; }
			private const float DEADZONE = .4f;

			/// <summary> [Confirm, Jump] button. </summary>
			public Button jumpButton;
			/// <summary> [Cancel, Action] button. </summary>
			public Button actionButton;
			/// <summary> Time break button. </summary>
			public Button breakButton;
			/// <summary> Speed break button. </summary>
			public Button boostButton;

			/// <summary> Start/Pause button. </summary>
			public Button pauseButton;
			public Mapping keyboardMapping;
			public Mapping gamepadMapping;

			/// <summary> The current gamepad being used. -1 for keyboard, indexing starts at 0. </summary>
			public int activeGamepad;
			public Mapping ActiveMapping => IsUsingGamepad ? gamepadMapping : keyboardMapping;
			/// <summary> -1 if the player is using a keyboard. </summary>
			public bool IsUsingGamepad => activeGamepad != -1;

			public void Update(float delta)
			{
				movementModifier.Update(!IsUsingGamepad && ButtonHeld(ActiveMapping.movementModifierBinding));

				//Update all inputs.
				if (IsUsingGamepad && ActiveMapping.horizontalBinding != -1)
					horizontalAxis.Update(AnalogAxisHeld(ActiveMapping.horizontalBinding), delta);
				else
					horizontalAxis.Update(DigitalAxisHeld(ActiveMapping.rightBinding, ActiveMapping.leftBinding, movementModifier.isHeld), delta);

				if (IsUsingGamepad && ActiveMapping.verticalBinding != -1)
					verticalAxis.Update(AnalogAxisHeld(ActiveMapping.verticalBinding), delta);
				else
					verticalAxis.Update(DigitalAxisHeld(ActiveMapping.downBinding, ActiveMapping.upBinding, movementModifier.isHeld), delta);

				jumpButton.Update(ButtonHeld(ActiveMapping.jumpBinding));
				boostButton.Update(ButtonHeld(ActiveMapping.boostBinding));
				breakButton.Update(ButtonHeld(ActiveMapping.breakBinding));
				actionButton.Update(ButtonHeld(ActiveMapping.actionBinding));

				pauseButton.Update(ButtonHeld(ActiveMapping.pauseBinding));

				MovementAxis = new Vector2(horizontalAxis.value, verticalAxis.value).LimitLength(1f);
				MovementAxisLength = MovementAxis.Length();
				IsHoldingNeutral = MovementAxisLength < DEADZONE;
				if (IsHoldingNeutral)
				{
					MovementAxisLength = 0;
					MovementAxis = Vector2.Zero;
				}
			}

			//Returns true when any of the action buttons were pressed
			public bool AnyButtonPressed => jumpButton.wasPressed || boostButton.wasPressed || breakButton.wasPressed || actionButton.wasPressed || pauseButton.wasPressed;

			//Returns the current physical value of an analog axis (Gamepad Only)
			private float AnalogAxisHeld(int axisCode) => Input.GetJoyAxis(activeGamepad, (JoyAxis)axisCode);
			//Combines two buttons as a single axis
			private float DigitalAxisHeld(int p, int n, bool modify)
			{
				float r = 0;
				if (ButtonHeld(p))
					r++;
				if (ButtonHeld(n))
					r--;

				if (modify) //Modify results
					r *= .5f;
				return r;
			}
			//Returns the physical state of a button
			private bool ButtonHeld(int buttonCode) =>
			IsUsingGamepad ? Input.IsJoyButtonPressed(activeGamepad, (JoyButton)buttonCode) :
			Input.IsKeyPressed((Key)buttonCode);

			//TODO Consoles will require default controls for gamepads
			public void DefaultControls()
			{
				activeGamepad = -1; //Default to keyboard
				DefaultKeyboardControls();
				DefaultGamepadControls();
			}

			public void DefaultKeyboardControls()
			{
				//Default to keyboard
				keyboardMapping = new Mapping()
				{
					upBinding = (int)Key.I,
					downBinding = (int)Key.K,
					leftBinding = (int)Key.J,
					rightBinding = (int)Key.L,
					movementModifierBinding = (int)Key.Shift,

					//Disable joystick axis
					horizontalBinding = -1,
					verticalBinding = -1,

					jumpBinding = (int)Key.V,
					actionBinding = (int)Key.C,
					breakBinding = (int)Key.Z,
					boostBinding = (int)Key.X,
					pauseBinding = (int)Key.Enter
				};
			}

			public void DefaultGamepadControls()
			{

				//Default to keyboard
				gamepadMapping = new Mapping()
				{
					horizontalBinding = (int)JoyAxis.LeftX,
					verticalBinding = (int)JoyAxis.LeftY,

					jumpBinding = (int)JoyButton.A,
					actionBinding = (int)JoyButton.B,
					breakBinding = (int)JoyButton.X,
					boostBinding = (int)JoyButton.Y,
					pauseBinding = (int)JoyButton.Start
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
			public bool wasTapped;
			public bool WasTapBuffered => tapBuffer > 0;
			public float tapBuffer;

			private const float TAP_BUFFER_LENGTH = .2f;

			public void Update(float currentValue, float timeDelta)
			{
				int oldDirection = sign;
				sign = Mathf.Sign(currentValue);
				wasTapped = sign != 0 && oldDirection != sign;
				if (wasTapped)
					tapBuffer = TAP_BUFFER_LENGTH;

				if (tapBuffer != 0)
					tapBuffer = Mathf.MoveToward(tapBuffer, 0, timeDelta);

				value = Mathf.SmoothStep(0, 1, Mathf.Abs(currentValue)) * sign;
			}

			public void ResetTap() => tapBuffer = 0;
		}

		[System.Serializable]
		public struct Mapping
		{
			//Contains a list of all the input bindings.
			public int leftBinding;
			public int rightBinding;
			public int horizontalBinding; //For analog inputs. -1 if unused
			public int verticalBinding; //For analog inputs. -1 if unused
			public int upBinding;
			public int downBinding;
			public int movementModifierBinding;
			public int jumpBinding;
			public int actionBinding;
			public int breakBinding;
			public int boostBinding;
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