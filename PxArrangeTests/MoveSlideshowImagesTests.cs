namespace PxArrange.Tests
{
	[TestClass()]
	public class MoveSlideshowImagesTests
	{
		public const string TestDirName = "PxArrangeTests";

		/// <summary>
		/// This method will be called before each MSTest test method
		/// </summary>
		[TestInitialize()]
		public void Setup()
		{
			var tempDir = Path.GetTempPath();
			var testDirPath = Path.Combine(tempDir, TestDirName);
			Console.WriteLine($"tempDir [{tempDir}] testDirPath [{testDirPath}]");
			if (Directory.Exists(testDirPath))
			{
				Directory.Delete(testDirPath, true);
			}
			Directory.CreateDirectory(testDirPath);

			// create the proper directory structure
			// make some fake images in the directories
			// make a fake database?
			// load a config file for testing with paths pointing at the temp directory
		}

		/// <summary>
		/// This method will be called after each MSTest test method has completed
		/// </summary>
		[TestCleanup()]
		public void Teardown()
		{
			var tempDir = Path.GetTempPath();
			var testDirPath = Path.Combine(tempDir, TestDirName);
			if (Directory.Exists(testDirPath))
			{
				Directory.Delete(testDirPath, true);
			}
		}

		[TestMethod()]
		public void MoveSlideshowImagesTest()
		{
			var moveSlideshowImages = new MoveSlideshowImages();
			Assert.IsNotNull(moveSlideshowImages);
		}

		//[TestMethod()]
		//public void RunTest()
		//{
		//	Assert.Fail();
		//}
	}
}
