using Godot;
using Project.Core;
using Project.Gameplay;
using System.Collections.Generic;

namespace Project.Interface;

/// <summary> The menu that handles notifications. </summary>
public partial class NotificationMenu : Control
{
	private static NotificationData CurrentNotification => NotificationList[0];
	private static readonly List<NotificationData> NotificationList = [];
	public static void AddNotification(NotificationType type, string description)
	{
		NotificationList.Add(new()
		{
			type = type,
			description = description
		});
	}

	[Export]
	private Menus.Description description;
	[Export]
	private AnimationPlayer animator;

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
		public string description;

		// Compares two lockout resources based on their priority
		public class Sorter : IComparer<NotificationData>
		{
			int IComparer<NotificationData>.Compare(NotificationData x, NotificationData y) => x.type.CompareTo(y.type);
		}
	}
	private readonly Dictionary<SkillKey, bool> skillUnlockStatus = [];

	private bool isProcessing;

	public override void _Ready()
	{
		// Cache unlock status for all skills
		for (int i = 0; i < (int)SkillKey.Max; i++)
		{
			SkillKey key = (SkillKey)i;
			skillUnlockStatus.TryAdd(key, SaveManager.ActiveSkillRing.IsSkillUnlocked(key));
		}
	}

	public override void _PhysicsProcess(double _)
	{
		if (!isProcessing)
			return;

		if (Input.IsActionJustPressed("button_jump"))
			ShowUnlock();
	}

	private void OnExperienceClosed()
	{
		// Loop through save data keys to see if their unlock status has changed
		for (int i = 0; i < (int)SkillKey.Max; i++)
		{
			SkillKey key = (SkillKey)i;
			// Update dictionary so true means the skill was just unlocked while false means its unlock status hasn't changed
			if (skillUnlockStatus[key] != SaveManager.ActiveSkillRing.IsSkillUnlocked(key))
			{
				NotificationList.Add(new NotificationData()
				{
					type = NotificationType.Skill,
					description = "unlock_skill"
				});
			}
		}

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

		TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionFinish, new Callable(this, MethodName.ShowUnlock));
		TransitionManager.FinishTransition();
	}

	private void ShowUnlock()
	{
		isProcessing = false; // Stop processing inputs

		if (NotificationList.Count == 0) // Finished showing all notifications
		{
			FinishMenu();
			return;
		}

		description.SetText(CurrentNotification.description);
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

	private void FinishMenu()
	{
		// Connect the queued scene to transition signals
		TransitionManager.QueueSceneChange(TransitionManager.instance.QueuedScene);
		// Load to the next scene
		TransitionManager.StartTransition(new()
		{
			color = Colors.Black,
			inSpeed = 0.5f,
			outSpeed = 0.5f,
			loadAsynchronously = true,
			disableAutoTransition = string.IsNullOrEmpty(TransitionManager.instance.QueuedScene),
		});
	}
}
