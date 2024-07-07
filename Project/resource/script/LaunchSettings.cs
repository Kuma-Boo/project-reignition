using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary> Contains data related to launched objects. </summary>
public struct LaunchSettings
{
	// Character settings
	/// <summary> Play jump FX? </summary>
	public bool IsJump { get; set; }
	/// <summary> Automatically align player's orientation? </summary>
	public bool UseAutoAlign { get; set; }
	/// <summary> Allow the player to jumpdash after launch is completed? </summary>
	public bool AllowJumpDash { get; set; }

	// Physics settings
	public Vector3 launchDirection;
	public Vector3 endPosition;
	public Vector3 startPosition;

	public float distance;
	public float middleHeight;
	public float finalHeight;

	public Vector3 InitialVelocity { get; private set; }
	public float HorizontalVelocity { get; private set; } // Horizontal velocity remains constant throughout the entire launch
	public float InitialVerticalVelocity { get; private set; }
	public float FinalVerticalVelocity { get; private set; }

	public float FirstHalfTime { get; private set; }
	public float SecondHalfTime { get; private set; }
	public float TotalTravelTime { get; private set; }

	/// <summary> Was this launch settings initialized? </summary>
	public bool IsInitialized { get; private set; }
	public bool IsLauncherFinished(float t) => t + PhysicsManager.physicsDelta >= TotalTravelTime;
	private static float Gravity => -Runtime.GRAVITY; // Use the same gravity as the character controller

	/// <summary>
	/// Get the current position, using t -> [0 <-> 1]. Lerps when launch data is invalid.
	/// </summary>
	public Vector3 InterpolatePositionRatio(float t)
	{
		if (Mathf.IsZeroApprox(TotalTravelTime) && !Mathf.IsZeroApprox(distance)) // Invalid launch data, use a lerp
			return startPosition.Lerp(endPosition, t);

		return InterpolatePositionTime(t * TotalTravelTime);
	}
	/// <summary>
	/// Get the current position, using t -> current time, in seconds. Relatively unsafe due to errors during invalid launch paths.
	/// </summary>
	public Vector3 InterpolatePositionTime(float t)
	{
		Vector3 displacement = (InitialVelocity * t) + (Vector3.Up * Gravity * t * t / 2f);
		return startPosition + displacement;
	}

	public void Initialize()
	{
		if (middleHeight <= finalHeight) // Ignore middle
			middleHeight = finalHeight;

		FirstHalfTime = Mathf.Sqrt(-2 * middleHeight / Gravity);
		SecondHalfTime = Mathf.Sqrt(-2 * (middleHeight - finalHeight) / Gravity);
		TotalTravelTime = FirstHalfTime + SecondHalfTime;

		HorizontalVelocity = distance / TotalTravelTime;
		InitialVerticalVelocity = Mathf.Sqrt(-2 * Gravity * middleHeight);
		FinalVerticalVelocity = Gravity * SecondHalfTime;

		InitialVelocity = launchDirection.RemoveVertical().Normalized() * HorizontalVelocity;
		InitialVelocity += Vector3.Up * InitialVerticalVelocity;
		IsInitialized = true;
	}

	/// <summary>
	/// Creates new launch data and calculates it. Modify the return value for extra control.
	/// </summary>
	/// <param name="s"> The launch's starting position. </param>
	/// <param name="e"> The launch's ending position. </param>
	/// <param name="h"> The highest height of the launch. </param>
	/// <param name="relativeToEnd"> Determines whether h is calculated based on the start or end. </param>
	public static LaunchSettings Create(Vector3 s, Vector3 e, float h, bool relativeToEnd = false)
	{
		Vector3 delta = e - s;
		LaunchSettings data = new()
		{
			startPosition = s,
			endPosition = e,
			launchDirection = delta.Normalized(),

			distance = delta.Flatten().Length(),
			middleHeight = h,
			finalHeight = delta.Y,
		};

		if (relativeToEnd)
		{
			data.middleHeight += delta.Y;
			if (data.middleHeight < 0)
				data.middleHeight = 0;
		}

		data.Initialize();
		return data;
	}
}