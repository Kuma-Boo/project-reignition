using Godot;
using System;

public partial class EventResizer : Control
{
	[Export] private VideoStreamPlayer player;
	private readonly Vector2 baseSize = new(1920, 1080);

	public override void _Ready()
	{
		CallDeferred(MethodName.ResizeVideoPlayer);
		Resized += ResizeVideoPlayer;
	}

	private void ResizeVideoPlayer()
	{
		if (Size.X > baseSize.X)
		{
			player.SetDeferred("size", baseSize);
			player.Position = Vector2.Right * (Size.X - baseSize.X) * 0.5f;
			return;
		}

		Vector2 targetPlayerSize = (baseSize * Size.X / baseSize.X).Ceil();
		player.SetDeferred("size", targetPlayerSize);
		player.Position = Vector2.Right * (targetPlayerSize.X - Size.X) + Vector2.Down * (Size.Y - targetPlayerSize.Y);
		player.Position *= 0.5f;
	}
}
