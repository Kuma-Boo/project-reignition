using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class Race : Control
{
	private StageSettings Stage => StageSettings.Instance;
	[ExportGroup("Race")]
	[Export] private Control raceBarSize;
	[Export] private Control raceUhu;
	[Export] private Control racePlayer;
	private float uhuVelocity;
	private float playerVelocity;
	private float raceEndPoint;
	private readonly float RaceSmoothing = 20.0f;
	public void InitializeRace()
	{
		if (Stage == null)
			return;

		Visible = Stage.Data.MissionType == LevelDataResource.MissionTypes.Race;
		raceEndPoint = raceBarSize.Size.X - raceUhu.Size.X;
		raceUhu.Position = new Vector2(0, raceUhu.Position.Y);
		racePlayer.Position = new Vector2(0, racePlayer.Position.Y);
	}

	public void UpdateRace(float playerRatio, float uhuRatio)
	{
		float uhuPosition = raceUhu.Position.X;
		float playerPosition = racePlayer.Position.X;
		uhuPosition = ExtensionMethods.SmoothDamp(uhuPosition, Mathf.Lerp(0, raceEndPoint, uhuRatio), ref uhuVelocity, RaceSmoothing * PhysicsManager.physicsDelta);
		playerPosition = ExtensionMethods.SmoothDamp(playerPosition, Mathf.Lerp(0, raceEndPoint, playerRatio), ref playerVelocity, RaceSmoothing * PhysicsManager.physicsDelta);

		raceUhu.Position = new(uhuPosition, raceUhu.Position.Y);
		racePlayer.Position = new(playerPosition, racePlayer.Position.Y);

		raceUhu.ZIndex = playerRatio >= uhuRatio ? 0 : 1;
		racePlayer.ZIndex = playerRatio >= uhuRatio ? 1 : 0;
	}
}
