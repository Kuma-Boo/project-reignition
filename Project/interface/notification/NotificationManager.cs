using Godot;
using Project.Core;
using Project.Gameplay;
using System.Collections.Generic;
using Project.Interface.Menus;

namespace Project.Interface;

/// <summary> The menu that handles notifications. </summary>
public partial class NotificationManager : Control
{
	public static NotificationManager Instance { get; private set; }
	private NotificationData CurrentNotification => NotificationList[0];
	private readonly List<NotificationData> NotificationList = [];
	public void AddNotification(NotificationType type, string description)
	{
		NotificationList.Add(new()
		{
			type = type,
			description = description
		});
	}

	[Export] private Menus.Description description;
	[Export] private AnimationPlayer animator;

	public enum NotificationType
	{
		Skill,
		Mission,
		Page,
		Party,
		World,
		WorldRing,
	}
	public struct NotificationData
	{
		public NotificationType type;
		public StringName description;

		// Compares two lockout resources based on their priority
		public class Sorter : IComparer<NotificationData>
		{
			int IComparer<NotificationData>.Compare(NotificationData x, NotificationData y) => x.type.CompareTo(y.type);
		}
	}

	[Export] private SpecialBookPage[] specialBookPages = [];

	private bool isProcessing;

	private static int startingSkillCount;
	private static int startingUnlockedPageCount;

	public override void _EnterTree()
	{
		Instance = this;
		CallDeferred(MethodName.UpdateCounters);
	}

	public override void _PhysicsProcess(double _)
	{
		if (!isProcessing)
			return;

		if (Runtime.Instance.IsActionJustPressed("sys_select", "ui_select"))
			ShowUnlock();
	}

	public void StartNotifications()
	{
		animator.Play("RESET");
		animator.Advance(0.0);
		ProcessMode = ProcessModeEnum.Inherit;

		// Loop through save data keys to see if their unlock status has changed
		int unlockedSkillCount = CalculateUnlockedSkillCount();
		if (unlockedSkillCount != startingSkillCount)
		{
			AddNotification(NotificationType.Skill, unlockedSkillCount > startingSkillCount + 1 ? "unlock_skill_multiple" : "unlock_skill");
			startingSkillCount = unlockedSkillCount;
		}

		int unlockedPageCount = CalculateUnlockedSpecialBookPages();
		if (startingUnlockedPageCount != unlockedPageCount)
		{
			AddNotification(NotificationType.Page, unlockedPageCount > startingUnlockedPageCount + 1 ? "unlock_page_multiple" : "unlock_page");
			startingUnlockedPageCount = unlockedPageCount;
		}

		if (NotificationList.Count != 0)
			NotificationList.Sort(new NotificationData.Sorter());

		// Connect transition signal
		TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionProcess, new Callable(this, MethodName.InitializeMenu), (uint)ConnectFlags.OneShot);
		TransitionManager.StartTransition(new()
		{
			color = Colors.Black,
			inSpeed = .2f,
			outSpeed = 0f,
		});
	}

	private void InitializeMenu()
	{
		animator.Play("init");
		animator.Advance(0);

		TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionFinish, new Callable(this, MethodName.ShowUnlock), (uint)ConnectFlags.OneShot);
		TransitionManager.FinishTransition();
	}

	private void HideMenu()
	{
		animator.Play("RESET");
		animator.Advance(0);
		ProcessMode = ProcessModeEnum.Disabled;
	}

	private void ShowUnlock()
	{
		isProcessing = false; // Stop processing inputs

		if (NotificationList.Count == 0) // Finished showing all notifications
		{
			TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionProcess, new Callable(this, MethodName.HideMenu), (uint)ConnectFlags.OneShot);

			// Connect the queued scene to transition signals
			TransitionManager.QueueSceneChange(TransitionManager.instance.QueuedScene);
			TransitionManager.StartTransition(new()
			{
				inSpeed = 0.5f,
				outSpeed = 0.5f,
				color = Colors.Black,
			});

			return;
		}

		description.Text = CurrentNotification.description;
		switch (CurrentNotification.type)
		{
			case NotificationType.WorldRing:
				animator.Play(CurrentNotification.description);
				break;
			case NotificationType.World:
				animator.Play("unlock_world");
				break;
			default:
				animator.Play($"unlock_{CurrentNotification.type.ToString().ToLower()}");
				break;
		}

		NotificationList.RemoveAt(0); // Clear the notification

		animator.Advance(0.0);
		animator.Play("unlock");
	}

	private void EnableProcessing() => isProcessing = true;

	private void UpdateCounters()
	{
		startingSkillCount = CalculateUnlockedSkillCount();
		startingUnlockedPageCount = CalculateUnlockedSpecialBookPages();
	}

	private int CalculateUnlockedSkillCount()
	{
		int count = 0;
		for (int i = 0; i < (int)SkillKey.Count; i++)
		{
			if (SaveManager.ActiveSkillRing.IsSkillUnlocked((SkillKey)i))
				count++;
		}

		return count;
	}

	private int CalculateUnlockedSpecialBookPages()
	{
		int count = 0;
		foreach (SpecialBookPage page in specialBookPages)
		{
			if (page.PageType == Menus.SpecialBookPage.PageTypeEnum.Achievement)
				continue; // Don't count achievements
			if (!page.IsUnlocked())
				continue;

			count++;
		}

		return count;
	}
}
