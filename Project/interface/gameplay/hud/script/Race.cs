using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class Race : Control
{
	private StageSettings Stage => StageSettings.Instance;
	[ExportGroup("Race")]
	[Export]
	private Control raceRoot;
	[Export]
	private Control raceUhu;
	[Export]
	private Control racePlayer;
	private float uhuVelocity;
	private float playerVelocity;
	private readonly float RaceEndPoint = 512;
	private readonly float RaceSmoothing = 20.0f;
	public void InitializeRace()
	{
		if (Stage == null)
			return;

		raceRoot.Visible = Stage.Data.MissionType == LevelDataResource.MissionTypes.Race;
	}

	public void UpdateRace(float playerRatio, float uhuRatio)
	{
		float uhuPosition = raceUhu.Position.X;
		float playerPosition = racePlayer.Position.X;
		uhuPosition = ExtensionMethods.SmoothDamp(uhuPosition, Mathf.Lerp(0, RaceEndPoint, uhuRatio), ref uhuVelocity, RaceSmoothing * PhysicsManager.physicsDelta);
		playerPosition = ExtensionMethods.SmoothDamp(playerPosition, Mathf.Lerp(0, RaceEndPoint, playerRatio), ref playerVelocity, RaceSmoothing * PhysicsManager.physicsDelta);

		raceUhu.Position = new(uhuPosition, raceUhu.Position.Y);
		racePlayer.Position = new(playerPosition, racePlayer.Position.Y);

		raceUhu.ZIndex = playerRatio >= uhuRatio ? 0 : 1;
		racePlayer.ZIndex = playerRatio >= uhuRatio ? 1 : 0;
	}
}
