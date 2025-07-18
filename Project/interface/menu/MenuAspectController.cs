using Godot;
using System;

namespace Project.Interface;

/// <summary> Manages the aspect ratio of menus. </summary>
public partial class MenuAspectController : AspectRatioContainer
{
	public override void _Ready()
	{
		Resized += UpdateAspectRatio;
		UpdateAspectRatio();
	}

	private void UpdateAspectRatio()
	{
		Vector2I resolution = GetTree().Root.Size;
		Ratio = resolution.X / (float)resolution.Y;
	}
}
