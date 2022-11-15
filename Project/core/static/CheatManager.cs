namespace Project.Core
{
	public static class CheatManager
	{
		private static bool EnableCheats => false;

		public static bool InfiniteSoulGauge => EnableCheats && true;
		public static bool SkipCountdown => EnableCheats && true;
	}
}
