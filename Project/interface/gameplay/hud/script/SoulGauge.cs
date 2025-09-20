using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class SoulGauge : Control
{
	[Export] private NinePatchRect soulGaugeRect;
	[Export] private Control soulGaugeFill;
	[Export] private Control soulGaugeBackground;
	[Export] private AnimationPlayer soulGaugeAnimator;

	public override void _PhysicsProcess(double _) => UpdateSoulGauge(); // Animate the soul gauge
	/// <summary>
	/// Set soul gauge size based on player's level.
	/// </summary>
	public void InitializeSoulGauge()
	{
		soulGaugeBackground = soulGaugeFill.GetParent<Control>();

		// Resize the soul gauge
		float soulGaugeRatio = 0f;
		if (!SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.LockedSoulGauge))
			soulGaugeRatio = SaveManager.ActiveGameData.CalculateSoulGaugeLevelRatio();

		float maxMovementAmount = soulGaugeRect.Position.Y;
		float maxSize = soulGaugeRect.RegionRect.Size.Y + (maxMovementAmount / soulGaugeRect.Scale.Y);
		soulGaugeRect.Size = new Vector2(
			soulGaugeRect.Size.X,
			Mathf.Lerp(soulGaugeRect.RegionRect.Size.Y, maxSize, soulGaugeRatio)
		);

		soulGaugeRect.Position = new Vector2(
			soulGaugeRect.Position.X,
			Mathf.Lerp(maxMovementAmount, 0, soulGaugeRatio)
		);
	}

	public void ModifySoulGauge(float ratio, bool isCharged)
	{
		targetSoulGaugeRatio = ratio;
		UpdateSoulGaugeColor(isCharged);
	}

	private float targetSoulGaugeRatio;
	private Vector2 soulGaugeVelocity;
	private const int SoulGaugeChargePoint = 320;
	private const int SoulGaugeFillOffset = 15;
	private void UpdateSoulGauge()
	{
		float chargePoint = soulGaugeBackground.Size.Y - SoulGaugeChargePoint;
		float targetPosition;
		if (isSoulGaugeCharged)
			targetPosition = Mathf.Lerp(chargePoint, 0, targetSoulGaugeRatio);
		else
			targetPosition = Mathf.Lerp(soulGaugeBackground.Size.Y + SoulGaugeFillOffset, chargePoint, targetSoulGaugeRatio);

		soulGaugeFill.Position = soulGaugeFill.Position.SmoothDamp(Vector2.Down * targetPosition, ref soulGaugeVelocity, 0.1f);
	}

	private bool isSoulGaugeCharged;
	public void UpdateSoulGaugeColor(bool isCharged)
	{
		if (!isSoulGaugeCharged && isCharged)
		{
			// Play animation
			isSoulGaugeCharged = true;
			soulGaugeAnimator.Play("charged");
		}
		else if (!isCharged)
		{
			// Lost charge
			isSoulGaugeCharged = false;
			soulGaugeAnimator.Play("RESET"); // Revert to blue
		}
	}
}
