using Godot;

namespace Project.Interface;

/// <summary>
/// Contains a list of sprites for controller display purposes.
/// </summary>
[GlobalClass]
public partial class ControllerSpriteResource : Resource
{
	[Export]
	public Texture2D[] axis;
	[Export]
	public Texture2D[] buttons;
}