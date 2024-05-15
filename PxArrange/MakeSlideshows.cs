#define ENABLE_LOG

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace PxArrange
{
	public class MakeSlideshows
	{
		private static readonly IComparer<string> _stringComparer = new WindowsFileExplorerCompare();

		public bool DoDryRun;
		public const int MinImagesPerSlideshow = 300;

		public MakeSlideshows(bool doDryRun)
		{
			DoDryRun = doDryRun;
		}

		public void Run()
		{
			var targetDirectories = GetTargetDirectories(PxPaths.FreshPath);
			//Log("targetDirectories", targetDirectories);

			// Delete existing slideshow files.
			// todo: Maybe back them up somewhere?
			Directory.CreateDirectory(PxPaths.SlideshowDirPath);
			if (!DoDryRun)
			{
				var deletedCount = 0;
				foreach (var file in Directory.EnumerateFiles(PxPaths.SlideshowDirPath, "slideshow*.txt"))
				{
					//Log($"Deleting existing slideshow file [{file}]");
					File.Delete(file);
					++deletedCount;
				}
				Log($"Deleted [{deletedCount}] existing slideshow files");
			}

			FindFiles(targetDirectories);
		}

		[Conditional("ENABLE_LOG")]
		private void Log(params object[] args)
		{
			Logger.Instance.Log(args);
		}

		[Conditional("ENABLE_LOG")]
		private void Error(params object[] args)
		{
			Logger.Instance.Error(args);
		}

		private List<string> GetTargetDirectories(string directoryRootPath)
		{
			var outputDirectories = Directory.EnumerateDirectories(directoryRootPath);
			var directoryLookup = new List<string>();
			var regex = new Regex(@"\((\d+)\)");

			//Log($"outputDirectories", outputDirectories);

			foreach (var directoryPath in outputDirectories)
			{
				var directoryName = Path.GetFileName(directoryPath);
				if (directoryName is null)
				{
					Error($"Directory path is not actually a directory [{directoryPath}]");
					continue;
				}

				var match = regex.Match(directoryName);
				if (!match.Success)
				{
					Error($"Unexpected format for directory name [{directoryName}]");
					continue;
				}

				var memberIdString = match.Groups[1].Value;
				var memberIdIsValid = int.TryParse(memberIdString, out int memberId);
				if (!memberIdIsValid)
				{
					Error($"Couldn't parse a MemberId from the directory name [{directoryName}]");
					continue;
				}

				directoryLookup.Add(directoryPath);
			}

			directoryLookup.Sort(_stringComparer);

			return directoryLookup;
		}

		private void FindFiles(List<string> targetDirectories)
		{
			var regex = new Regex(@"^(\d+)_.+");
			//var regex = new Regex(@"^(\d+)_p(\d+)");
			var imagePathList = new List<string>();
			var slideshowIndex = 0;

			foreach (var directoryPath in targetDirectories)
			{
				if (imagePathList.Count >= MinImagesPerSlideshow)
				{
					WriteToFile(ref slideshowIndex, imagePathList);
				}

				var filePaths = Directory.EnumerateFileSystemEntries(directoryPath);
				if (filePaths.Count() >= MinImagesPerSlideshow)
				{
					WriteToFile(ref slideshowIndex, imagePathList);
				}

				foreach (var filePath in filePaths)
				{
					if (Directory.Exists(filePath))
					{
						var subDirectories = GetTargetDirectories(filePath);
						FindFiles(subDirectories);
						continue;
					}

					var fileName = Path.GetFileName(filePath);
					if (fileName is null)
					{
						Error($"File path does not exist [{filePath}]");
						continue;
					}

					var fileExtension = Path.GetExtension(filePath);
					if (fileExtension is null)
					{
						Error($"File path has no extension [{filePath}]");
						continue;
					}
					if (!PxPaths.ValidImageExtensions.Contains(fileExtension))
					{
						// This one isn't an error. We expect it to happen.
						if (!PxPaths.OtherExtensions.Contains(fileExtension))
						{
							Error($"File is an unexpected file type [{filePath}]");
						}
						else
						{
							//Log($"File is not a known image type [{filePath}]");
						}
						continue;
					}

					var match = regex.Match(fileName);
					if (!match.Success)
					{
						Error($"Unexpected format for file name [{fileName}]");
						continue;
					}

					var imageIdString = match.Groups[1].Value;
					var imageIdIsValid = int.TryParse(imageIdString, out int imageId);
					if (!imageIdIsValid)
					{
						Error($"Couldn't parse an ImageId from the file name [{fileName}]");
						continue;
					}

					//var pageNumberString = match.Groups[2].Value;
					//var pageNumberIsValid = int.TryParse(pageNumberString, out int pageNumber);
					//if (!pageNumberIsValid)
					//{
					//	Error($"Couldn't parse a page number from the file name [{fileName}]");
					//	continue;
					//}

					imagePathList.Add(filePath);
				}
			}

			if (imagePathList.Count > 0)
			{
				WriteToFile(ref slideshowIndex, imagePathList);
			}
		}

		/// <summary>
		/// Write the specified image paths to a slideshow file with the specified index,
		/// then increment the index and clear the list.
		/// </summary>
		/// <param name="slideshowIndex"></param>
		/// <param name="imagePathList"></param>
		private void WriteToFile(ref int slideshowIndex, List<string> imagePathList)
		{
			imagePathList.Sort(_stringComparer);

			var imageCount = imagePathList.Count;
			var slideshowFilePath = PxPaths.SlideshowPath(slideshowIndex); //, imageCount);

			var dryRunMessage = DoDryRun ? "DryRun: " : string.Empty;
			Log($"{dryRunMessage}Write slideshow file [{slideshowFilePath}] with [{imageCount}] images");

			if (!DoDryRun)
			{
				using var writer = new StreamWriter(slideshowFilePath, false, Encoding.Unicode);

				// This is the header that IrfanView writes to the slideshows when it saves them.
				writer.WriteLine(@"; UNICODE FILE - edit with care ;-)");
				writer.WriteLine();

				foreach (var filePath in imagePathList)
				{
					writer.WriteLine(filePath);
				}
			}

			imagePathList.Clear();
			++slideshowIndex;
		}
	}
}
