using Godot;
using System;

namespace Project.Interface.Menus;

public partial class TimeAttackLeaderboardOptionMain : Menu
{
	[Export] private Label placement;
	[Export] private Label time;

	public void SetPlacement(int place) => placement.Text = place.ToString() + ".";
	//public void SetTime(float thisTime) => time.Text = ExtensionMethods.FormatTime(thisTime);
	public void SetTime(float seconds)
	{
		seconds = Mathf.RoundToInt(seconds * 100) * 0.01f;
		TimeSpan span = TimeSpan.FromSeconds(seconds);
		time.Text = span.ToString(@"hh\:mm\:ss\.ff");
	}

	public void TimeVisible(bool isVisible) => time.Visible = isVisible;
}
