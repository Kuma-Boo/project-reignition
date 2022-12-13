namespace Project.Core
{
	public static class CheatManager
	{
		private static bool EnableCheats => true; //Disable this in the final build

		public static bool UseEditorSkillValues => EnableCheats && true; //Don't load skills from save data, use inspector values instead

		public static bool InfiniteSoulGauge => EnableCheats && true; //Grants an infinite soul gauge
		public static bool SkipCountdown => EnableCheats && true; //Skip the countdown

		public static bool DisableStageCulling => EnableCheats && false; ///Don't cull stages
	}
}
