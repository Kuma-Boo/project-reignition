using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Gameplay;

public partial class BonusManager : VBoxContainer
{
	public static BonusManager instance;
	private StageSettings Stage => StageSettings.Instance;
	private PlayerController Player => StageSettings.Player;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		instance = this;
		Stage.Connect(nameof(StageSettings.LevelCompleted), new Callable(this, MethodName.OnLevelCompleted)); //Hide interface
	}

	public override void _PhysicsProcess(double _)
	{
		if (bonusQueue.Count != 0)
			PlayBonus();

		if (enemyChain != 0)
			UpdateEnemyChain();
	}

	private void UpdateQueuedScore()
	{
		QueuedScore = 0;

		if (ringChain >= 10)
		{
			BonusData ringBonus = new(BonusType.Ring, ringChain);
			QueuedScore += ringBonus.CalculateBonusPoints();
		}

		if (enemyChain >= 2)
		{
			BonusData enemyBonus = new(BonusType.Enemy, enemyChain);
			QueuedScore += enemyBonus.CalculateBonusPoints();
		}
	}

	/// <summary> Score amount of bonuses that are currently processing. </summary>
	public int QueuedScore { get; private set; }
	private int bonusesActive;
	private readonly Queue<BonusData> bonusQueue = new();

	/// <summary> Queues a bonus to be played. </summary>
	public void QueueBonus(BonusData bonus) => bonusQueue.Enqueue(bonus);

	/// <summary> Actually plays a bonus from the queue. </summary>
	private void PlayBonus()
	{
		if (bonusesActive == GetChildCount()) return;

		BonusData bonusData = bonusQueue.Dequeue();
		Bonus bonus = GetChildOrNull<Bonus>(GetChildCount() - 1);
		if (!bonus.IsConnected(Bonus.SignalName.BonusFinished, new(this, MethodName.BonusFinished))) // Connect signal if needed
			bonus.Connect(Bonus.SignalName.BonusFinished, new(this, MethodName.BonusFinished));

		MoveChild(bonus, 0); // Re-order to appear first
		bonus.ShowBonus(bonusData, bonusData.CalculateBonusPoints()); // Activate bonus
		bonusesActive++;

		if (bonusData.Type == BonusType.EXP)
		{
			StageSettings.Instance.CurrentEXP += bonusData.Amount;
			return;
		}

		// Update score
		StageSettings.Instance.UpdateScore(bonusData.CalculateBonusPoints(), StageSettings.MathModeEnum.Add);
		UpdateQueuedScore();
	}

	private void BonusFinished() => bonusesActive--;

	private int ringChain;
	/// <summary> Increases the current ring chain. </summary>
	public void AddRingChain()
	{
		ringChain++;

		if (ringChain >= 50)
			FinishRingChain(true); // Force ring chains to finish when going over 50

		if (Stage.Data.MissionType == LevelDataResource.MissionTypes.Chain)
			Stage.IncrementObjective();

		UpdateQueuedScore();
	}

	/// <summary> Ends the current ring chain. </summary>
	private void FinishRingChain() => FinishRingChain(false);
	private void FinishRingChain(bool forceFinish)
	{
		if (ringChain >= 10)
			QueueBonus(new(BonusType.Ring, ringChain));

		ringChain = 0; // Reset ring chain

		if (forceFinish) // Don't check for level completion when comboing more than 50 rings
			return;

		if (Stage.Data.MissionType == LevelDataResource.MissionTypes.Chain &&
			Stage.LevelState == StageSettings.LevelStateEnum.Ingame)
		{
			if (Stage.CurrentObjectiveCount >= Stage.Data.MissionObjectiveCount)
				Stage.FinishLevel(true);
			else
				Stage.ResetObjective();
		}
	}

	private int enemyChain;
	private float enemyChainTimer;
	/// <summary> "Grace time" to allow player to chain attacks from a speed-break. </summary>
	private readonly float EnemyChainBuffer = .5f;
	private readonly List<Node> activeEnemyComboExtenders = [];

	/// <summary> Increases the enemy chain. </summary>
	public void AddEnemyChain()
	{
		enemyChain++;
		UpdateQueuedScore();
	}

	/// <summary> Erases the current enemy chain. </summary>
	public void ResetEnemyChain()
	{
		enemyChain = 0;
		enemyChainTimer = 0;
	}

	/// <summary> Checks whether the enemy chain should end. </summary>
	public void UpdateEnemyChain()
	{
		if (!Player.IsOnGround) return; // Chain is never counted when the player is in the air
		if (Player.AllowLandingGrind || Player.IsGrinding) return; // Ignore Grindrails
		if (Player.ExternalController != null) return; // Chains only end during normal movement
		if (Player.IsBouncing) return;

		if (Player.Skills.IsSpeedBreakActive)
		{
			enemyChainTimer = EnemyChainBuffer;
			return; // Chain continues during speedbreak
		}

		enemyChainTimer = Mathf.MoveToward(enemyChainTimer, 0, PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(enemyChainTimer) && activeEnemyComboExtenders.Count == 0)
			FinishEnemyChain();
	}

	/// <summary> Ends the current enemy chain. </summary>
	public void FinishEnemyChain()
	{
		GD.Print("Enemy Chain Ended");
		if (enemyChain >= 2)
			QueueBonus(new(BonusType.Enemy, enemyChain));

		enemyChain = 0; // Reset enemy chain
	}

	public bool IsEnemyComboExtenderRegistered(Node n) => activeEnemyComboExtenders.Contains(n);
	public void RegisterEnemyComboExtender(Node n)
	{
		if (activeEnemyComboExtenders.Contains(n))
			return;

		activeEnemyComboExtenders.Add(n);
	}
	public void UnregisterEnemyComboExtender(Node n) => activeEnemyComboExtenders.Remove(n);

	/// <summary> Called when the level is completed. Forces all bonuses to be counted. </summary>
	private void OnLevelCompleted()
	{
		FinishRingChain();
		FinishEnemyChain();
		while (bonusQueue.Count != 0)
			PlayBonus(); // Count all bonuses immediately
	}

	public void CancelBonuses()
	{
		ringChain = 0;
		enemyChain = 0;
		enemyChainTimer = 0;
	}
}

public enum BonusType
{
	Ring,
	Enemy,
	Boss,
	Drift,
	Grindstep,
	GrindShuffle,
	EXP,
}

public readonly struct BonusData(BonusType type, int amount = 0)
{
	public BonusType Type => type;
	public StringName Key => GetKeyLabel(type).ToString().Replace("0", Amount.ToString());
	public int Amount => amount;

	public int CalculateBonusPoints()
	{
		switch (Type)
		{
			case BonusType.Ring:
				if (Amount == 50) // Perfect ring bonus; Chain of 50
					return 1500;
				if (Amount >= 40)
					return 1000;
				else if (Amount >= 30)
					return 500;
				if (Amount >= 20)
					return 300;
				// 10 rings -> 100 pts
				return 100;
			case BonusType.Enemy:
				return Mathf.Min(Amount - 1, 10) * 200; // +200 pts per extra enemy up to 11 (2000 pts)
			case BonusType.Drift:
				return 500;
			case BonusType.Grindstep:
				return 200;
			case BonusType.GrindShuffle:
				return 50;
			default:
				return Amount;
		}
	}

	private static StringName GetKeyLabel(BonusType type)
	{
		switch (type)
		{
			case BonusType.Ring:
				return "bonus_ring";
			case BonusType.Enemy:
			case BonusType.Boss:
				return "bonus_enemy";
			case BonusType.Drift:
				return "bonus_drift";
			case BonusType.Grindstep:
				return "bonus_grindstep";
			case BonusType.GrindShuffle:
				return "bonus_grind_shuffle";
			case BonusType.EXP:
				return TranslationServer.Translate("bonus_exp"); // TODO Replace with localized string
			default:
				return string.Empty;
		}
	}
}