using Godot;
using System;

namespace Project.Interface.Menus;

public partial class TimeAttackLeaderboardOptionSub : Menu
{
	[Export] private Label placement;
	[Export] private Label area;
	[Export] private Label level;
	[Export] private Label time;

	public void SetPlacement(int place) => placement.Text = place.ToString() + ".";
	public void SetArea(string thisArea) => area.Text = thisArea;
	public void SetLevel(string thisLevel) => level.Text = thisLevel;
	//public void SetTime(float thisTime) => time.Text = ExtensionMethods.FormatTime(thisTime);
	public void SetTime(float thisTime)
	{
		TimeSpan span = TimeSpan.FromSeconds(thisTime);
		time.Text = span.ToString(@"mm\:ss\.ff");
	}

	public void TimeVisible(bool isVisible) => time.Visible = isVisible;
}
