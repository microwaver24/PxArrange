namespace PxArrange
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			Log("start");

			var arrangeInFolders = new ArrangeInFolders(doDryRun: true);
			arrangeInFolders.Run();

			Log("end");
		}

		private static void Log(params object[] args)
		{
			Logger.Instance.Log(args);
		}
	}
}
