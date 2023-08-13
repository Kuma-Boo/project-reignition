namespace Project.Core
{
	public static class CheatManager
	{
		/// <summary> Don't forget to set this to false for the final build. </summary>
		private static bool EnableCheats => true;

		/// <summary> Allow the player to jump in the air. </summary>
		public static bool EnableMoonJump => EnableCheats && false;

		/// <summary> Use a custom save. </summary>
		public static bool UseDebugSave => EnableCheats && true;
		/// <summary> Don't load skills from save data, use inspector values instead. </summary>
		public static bool UseEditorSkillValues => EnableCheats && true;

		/// <summary> Infinite soul gauge. </summary>
		public static bool InfiniteSoulGauge => EnableCheats && true;
		/// <summary> Infinite soul gauge. </summary>
		public static bool InfiniteRings => EnableCheats && true;
		/// <summary> Skip countdowns for faster debugging. </summary>
		public static bool SkipCountdown => EnableCheats && true;

		/// <summary> Always keep stage geometry visible. </summary>
		public static bool DisableStageCulling => EnableCheats && false;

		/// <summary> Have all stages unlocked </summary>
		public static bool UnlockAllStages => EnableCheats && true;
	}
}
