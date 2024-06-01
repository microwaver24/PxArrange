using ManyConsole;

namespace PxArrange
{
	internal class Program
	{
		public const int Success = 0;
		public const int Failure = 2;

		private static int Main(string[] args)
		{
			var commands = GetCommands();

			return ConsoleCommandDispatcher.DispatchCommand(commands, args, Console.Out);

			//var arrangeInFolders = new ArrangeInFolders(doDryRun: true);
			//arrangeInFolders.Run();

			//var makeSlideshows = new MakeSlideshows(doDryRun: false);
			//makeSlideshows.Run();

			//var moveSlideshowImages = new MoveSlideshowImages(doDryRun: true);
			//moveSlideshowImages.Run(slideshowIndex: 0, outputDirectoryRootPath: PxPaths.AllPath);

			//Log("end");
		}

		private static void Log(params object[] args)
		{
			Logger.Instance.Log(args);
		}

		public static IEnumerable<ConsoleCommand> GetCommands()
		{
			return ConsoleCommandDispatcher.FindCommandsInSameAssemblyAs(typeof(Program));
		}
	}
}
