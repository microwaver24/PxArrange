#define ENABLE_LOG

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace PxArrange
{
	public class MoveSlideshowImages
	{
		private static readonly IComparer<string> _stringComparer = new WindowsFileExplorerCompare();

		public bool DoDryRun;
		public const int MinImagesPerSlideshow = 300;

		public MoveSlideshowImages(bool doDryRun)
		{
			DoDryRun = doDryRun;
		}

		public void Run(int slideshowIndex)
		{
			Log("MoveSlideshowImages", slideshowIndex);

			Directory.CreateDirectory(PxPaths.SlideshowDirPath);

			ReadFromFile(slideshowIndex);
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

		private List<string> ReadFromFile(int slideshowIndex)
		{
			var regex = new Regex(@"\((\d+)\)");
			var dryRunMessage = DoDryRun ? "DryRun: " : string.Empty;
			var imagePathList = new List<string>();

			var slideshowFilePath = PxPaths.SlideshowPath(slideshowIndex);
			if (!File.Exists(slideshowFilePath))
			{
				Error($"Slideshow for index [{slideshowIndex}] file does not exist [{slideshowFilePath}]");
				return imagePathList;
			}

			using var reader = new StreamReader(slideshowFilePath, Encoding.Unicode);
			var filesMoved = 0;
			var filesInvalid = 0;
			var filesMissing = 0;
			var filePath = string.Empty;

			while ((filePath = reader.ReadLine()) != null)
			{
				if (!filePath.Contains(Path.DirectorySeparatorChar))
				{
					//Log($"Not a file [{filePath}]");
					continue;
				}

				if (!File.Exists(filePath))
				{
					Log($"File does not exist [{filePath}]");
					++filesMissing;
					continue;
				}

				var fileName = Path.GetFileName(filePath);
				var directoryIterator = Path.GetDirectoryName(filePath);
				var artistDirectoryPath = string.Empty;

				// todo: Maybe use the image id from the filename instead and check the database for the proper location?
				while (directoryIterator is not null)
				{
					var directoryName = Path.GetFileName(directoryIterator);
					var match = regex.Match(directoryName);
					if (match.Success)
					{
						artistDirectoryPath = directoryIterator;
						break;
					}
					directoryIterator = Path.GetDirectoryName(directoryIterator);
				}

				if (string.IsNullOrEmpty(artistDirectoryPath))
				{
					Error($"Couldn't find an artist folder for image file [{filePath}]");
					++filesInvalid;
					continue;
				}

				var artistDirectoryName = Path.GetFileName(artistDirectoryPath);
				var newFilePath = Path.Combine(PxPaths.AllPath, artistDirectoryName, fileName);

				Log($"{dryRunMessage}Move image file [{filePath}] to [{newFilePath}]");

				// todo: also move zip files and json files

				if (!DoDryRun)
				{
					try
					{
						File.Move(filePath, newFilePath, overwrite: false);
					}
					catch (Exception ex)
					{
						Error($"Failure moving file [{filePath}] {ex}");
						continue;
					}
				}
				++filesMoved;
			}

			var filesTotal = filesMoved + filesMissing + filesInvalid;
			Log(
				$"{dryRunMessage}Files moved [{filesMoved}] missing [{filesMissing}] invalid [{filesInvalid}] total [{filesTotal}]"
			);

			return imagePathList;
		}
	}
}
