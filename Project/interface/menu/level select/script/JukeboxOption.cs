using Godot;
using System;

namespace Project.Interface.Menus;

public partial class JukeboxOption : Control
{

	///<summary> Reference to the target BGM Resource. </summary>
	[Export] public BGMResource Bgm { get; private set; }

	[Export] private Label name;
	[Export] private AnimationPlayer animator;

	public bool Equipped { get; private set; }

	public void SetBgmResource(BGMResource resource)
	{
		Bgm = resource;
		if (string.IsNullOrEmpty(Bgm.SongName))
			name.Text = string.Empty;
		else
			name.Text = Bgm.SongName.GetBaseName();
	}

	public void Equip()
	{
		animator.Play("equip");
		Equipped = true;
	}

	public void Unequip()
	{
		animator.Play("unequip");
		Equipped = false;
	}
}
