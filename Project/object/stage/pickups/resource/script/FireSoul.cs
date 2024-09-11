using Godot;
using Project.Core;

namespace Project.Gameplay.Objects;

public partial class FireSoul : Pickup, ITriggeredCheckpointListener
{
	[Export(PropertyHint.Range, "1, 3")]
	public int fireSoulIndex = 1; // Which fire soul is this?
	private bool isCollected;
	private bool isCollectedInCheckpoint;
	private bool isCollectedInSaveFile;
	[Export(PropertyHint.NodePathValidTypes, "AnimationPlayer")]
	private NodePath animator;
	private AnimationPlayer Animator;

	protected override void SetUp()
	{
		Animator = GetNodeOrNull<AnimationPlayer>(animator);

		base.SetUp();

		// Check save data
		isCollectedInSaveFile = SaveManager.ActiveGameData.IsFireSoulCollected(Stage.Data.LevelID, fireSoulIndex);
		if (isCollectedInSaveFile)
		{
			Despawn();
			return;
		}

		UpdateLockon();
	}

	protected override void Collect()
	{
		if (isCollected)
			return;

		isCollected = true;
		Animator.Play("collect");
		HeadsUpDisplay.instance.CollectFireSoul();
		StageSettings.instance.ConnectTriggeredCheckpointSignal(this, (uint)ConnectFlags.OneShot);
	}

	public override void Respawn()
	{
		if (isCollectedInSaveFile || isCollectedInCheckpoint) // Was already collected
			return;

		isCollected = false;
		Animator.Play("RESET");
		Animator.Advance(0);
		UpdateLockon();
		Animator.Play("loop");

		if (StageSettings.instance.IsConnected(StageSettings.SignalName.TriggeredCheckpoint, new(this, MethodName.SaveCheckpoint)))
			StageSettings.instance.DisconnectTriggeredCheckpointSignal(this);

		base.Respawn();
	}

	private void UpdateLockon()
	{
		if (Character.Skills.IsSkillEquipped(SkillKey.FireSoulLockon))
		{
			Animator.Play("enable-lockon");
			Animator.Advance(0);
		}
	}
	public void TriggeredCheckpoint()
	{
		SaveCheckpoint();
	}
	private void SaveCheckpoint() => isCollectedInCheckpoint = true;

	public override void Unload()
	{
		if (isCollected && Stage.LevelState == StageSettings.LevelStateEnum.Success) // Write save data
			SaveManager.ActiveGameData.SetFireSoulCollected(Stage.Data.LevelID, fireSoulIndex, true);

		base.Unload();
	}
}