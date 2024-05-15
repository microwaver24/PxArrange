namespace PxArrange
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			Log("start");

			//var arrangeInFolders = new ArrangeInFolders(doDryRun: true);
			//arrangeInFolders.Run();

			//var makeSlideshows = new MakeSlideshows(doDryRun: false);
			//makeSlideshows.Run();

			var moveSlideshowImages = new MoveSlideshowImages(doDryRun: true);
			moveSlideshowImages.Run(slideshowIndex: 0, outputDirectoryRootPath: PxPaths.AllPath);

			Log("end");
		}

		private static void Log(params object[] args)
		{
			Logger.Instance.Log(args);
		}
	}
}
