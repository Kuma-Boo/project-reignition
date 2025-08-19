using Godot;

namespace Project.Interface.VirtualKeyboard;

public partial class VirtualKeyboard : CanvasLayer
{
	[Signal]
	public delegate void SubmittedEventHandler();
	[Signal]
	public delegate void KeySetChangeEventHandler();
	public static readonly char[][] LanguageEN = [
		['1', '1', '1'],
		['2', '2', '2'],
		['3', '3', '3'],
		['4', '4', '4'],
		['5', '5', '5'],
		['6', '6', '6'],
		['7', '7', '7'],
		['8', '8', '8'],
		['9', '9', '9'],
		['0', '0', '0'],
		['@', '@', '-'],
		['q', 'Q', '!'],
		['w', 'W', '@'],
		['e', 'E', '#'],
		['r', 'R', '$'],
		['t', 'T', '%'],
		['y', 'Y', '^'],
		['u', 'U', '&'],
		['i', 'I', '*'],
		['o', 'O', '('],
		['p', 'P', ')'],
		['+', '=', '_'],
		['a', 'A', '~'],
		['s', 'S', '`'],
		['d', 'D', '='],
		['f', 'F', '\\'],
		['g', 'G', '+'],
		['h', 'H', '{'],
		['j', 'J', '}'],
		['k', 'K', '|'],
		['l', 'L', '['],
		['_', '&', ']'],
		[':', ';', '\0'],
		['z', 'Z', '<'],
		['x', 'X', '>'],
		['c', 'C', ':'],
		['v', 'V', ';'],
		['b', 'B', '"'],
		['n', 'N', '\''],
		['m', 'M', ','],
		[',', '*', '.'],
		['.', '#', '?'],
		['-', '!', '/'],
		['/', '?', '\0'],
	];
	[Export]
	public PackedScene VirtualKeyScene;
	private AnimationPlayer _animations;
	private GridContainer _keys;
	public override void _EnterTree()
	{
		_animations = GetNode<AnimationPlayer>("%Animations");
		_keys = GetNode<GridContainer>("%Keys");
		InitializeKeys();
	}
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey eventKey)
		{
			GD.Print(eventKey);
		}
	}
	public void MoveToBottom()
	{
		var parent = GetParent();
		var childCount = parent.GetChildCount();
		parent.MoveChild(this, childCount);
	}

	private void InitializeKeys()
	{
		foreach (var key in LanguageEN)
		{
			var node = VirtualKeyScene.Instantiate<VirtualKey>();
			node.Key = key;
			node.ParentKeyboard = this;
			node.Connect(VirtualKey.SignalName.VirtualKeyPress, Callable.From<char>(HandleKeypress));
			_keys.AddChild(node);
		}
	}

	#region visibility functions
	private bool _visible = false;
	private LineEdit _inputTarget;
	public void ShowKeyboard(Control target)
	{
		if (_visible) return;
		_visible = true;
		_inputTarget = target as LineEdit;
		MoveToBottom();
		Show();
		_animations.Play("Animate");
	}
	public void HideKeyboard()
	{
		if (!_visible) return;
		_visible = false;
		_animations.PlayBackwards("Animate");
		Hide();
	}
	public void ToggleKeyboard()
	{
		if (_visible)
		{
			Hide();
		}
		else
		{
			Show();
		}
	}
	#endregion
	#region shift functions
	public enum KeySet
	{
		Lower,
		Upper,
		CapsLock,
		Symbols
	}
	private KeySet _keySet = KeySet.Lower;
	private void UpdateKeySetState(KeySet set)
	{
		_keySet = set;
		EmitSignal(SignalName.KeySetChange, (int)_keySet);
	}
	public void CycleShift()
	{
		UpdateKeySetState((KeySet)Mathf.Min((int)_keySet + 1 % 3, (int)KeySet.Upper));
	}
	#endregion
	private void HandleKeypress(char character)
	{
		_inputTarget.Text += character.ToString();
		_inputTarget.CaretColumn = _inputTarget.Text.Length;
		if (_keySet == KeySet.Upper)
		{
			UpdateKeySetState(KeySet.Lower);
		}
	}
	public void HandleSpaceKey()
	{
		HandleKeypress(' ');
	}
	public void HandleShiftKey()
	{
		if (_keySet != KeySet.Symbols)
		{
			CycleShift();
		}
	}
	public void HandleSymbolsKey()
	{
		UpdateKeySetState(KeySet.Symbols);
	}
	public void HandleBackspaceKey()
	{
		_inputTarget.Text = _inputTarget.Text.Remove(-1, 1);
	}
	public void HandleReturnKey()
	{

	}
	public void HandleSubmit()
	{
		HideKeyboard();
		EmitSignal(SignalName.Submitted);
	}
	public void HandleCaretMove(bool left = true)
	{
		if (left)
		{
			_inputTarget.CaretColumn--;
		}
		else
		{
			_inputTarget.CaretColumn++;
		}
	}
}
