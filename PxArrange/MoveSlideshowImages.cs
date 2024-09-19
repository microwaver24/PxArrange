#define ENABLE_LOG

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace PxArrange
{
	public class MoveSlideshowImages
	{
		public static class KeySpace
		{
			public const string Deleted = "Deleted";
			public const string Errors = "Errors";
			public const string Invalid = "Invalid";
			public const string Moved = "Moved";
			public const string MovedImages = "Moved (Images)";
			public const string NotFound = "Not Found";
			public const string SkippedNotEmpty = "Skipped (Not Empty)";
			public const string Total = "Total";
		}

		public bool DoDryRun;

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
			var imagesMoved = 0;
			var filesInvalid = 0;
			var filesMissing = 0;
			var directoriesVisitedSet = new HashSet<string>();
			var filePath = string.Empty;

			while ((filePath = reader.ReadLine()) != null)
			{
				if (string.IsNullOrWhiteSpace(filePath))
				{
					continue;
				}

				var directoryPath = Path.GetDirectoryName(filePath);
				if (!string.IsNullOrEmpty(directoryPath))
				{
					VisitDirectory(directoryPath, directoriesVisitedSet);
				}

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
					Error($"Couldn't find an artist directory for image file [{filePath}]");
					++filesInvalid;
					continue;
				}

				// If an image is in a subdirectory, then directoryPath and artistDirectoryPath might be different.
				VisitDirectory(artistDirectoryPath, directoriesVisitedSet);

				var artistDirectoryName = Path.GetFileName(artistDirectoryPath);
				var newDirectory = Path.Combine(PxPaths.AllPath, artistDirectoryName);
				var newFilePath = Path.Combine(PxPaths.AllPath, artistDirectoryName, fileName);

				var filesToMove = new Dictionary<string, string> { { filePath, newFilePath }, };

				// handle image json file, ugoira zip file, ugoira js file
				foreach (var extension in PxPaths.OtherExtensions)
				{
					var otherFileOldPath = Path.ChangeExtension(filePath, extension);
					var otherFileNewPath = Path.ChangeExtension(newFilePath, extension);
					if (File.Exists(otherFileOldPath))
					{
						filesToMove.Add(otherFileOldPath, otherFileNewPath);
					}
				}

				foreach (var kvp in filesToMove)
				{
					var oldPath = kvp.Key;
					var newPath = kvp.Value;

					try
					{
						if (!DoDryRun)
						{
							if (!Directory.Exists(newDirectory))
							{
								Directory.CreateDirectory(newDirectory);
							}

							File.Move(oldPath, newPath, overwrite: false);
						}
						Log($"{dryRunMessage}Move file [{oldPath}] to [{newPath}]");
					}
					catch (Exception ex)
					{
						Error($"Failure moving file [{oldPath}] {ex}");
						continue;
					}

					++filesMoved;
				}

				++imagesMoved;
			}

			var filesTotal = filesMoved + filesMissing + filesInvalid;
			var logObject = new
			{
				Files = new Dictionary<string, dynamic>()
				{
					{ KeySpace.Moved, filesMoved },
					{ KeySpace.MovedImages, imagesMoved },
					{ KeySpace.NotFound, filesMissing },
					{ KeySpace.Invalid, filesInvalid },
					{ KeySpace.Total, filesTotal },
				},
				Directories = new Dictionary<string, dynamic>()
				{
					{ KeySpace.Deleted, 0 },
					{ KeySpace.SkippedNotEmpty, 0 },
					{ KeySpace.NotFound, 0 },
					{ KeySpace.Errors, 0 },
					{ KeySpace.Total, directoriesVisitedSet.Count() },
				},
			};
			foreach (var directoryPath in directoriesVisitedSet)
			{
				DeleteDirectoryIfEmpty(directoryPath, dryRunMessage, logObject.Directories);
			}

			Log($"{dryRunMessage}Results {Logger.ToJsonString(logObject)}");

			return imagePathList;
		}

		private void VisitDirectory(string directoryPath, HashSet<string> directoriesVisitedSet)
		{
			directoriesVisitedSet.Add(directoryPath);
		}

		private void DeleteDirectoryIfEmpty(
			string directoryPath,
			string dryRunMessage,
			Dictionary<string, dynamic> logObject
		)
		{
			if (!Directory.Exists(directoryPath))
			{
				++logObject[KeySpace.NotFound];
				return;
			}

			var remainingFiles = Directory.EnumerateFiles(directoryPath);
			if (remainingFiles.Count() <= 0)
			{
				try
				{
					if (!DoDryRun)
					{
						Directory.Delete(directoryPath, true);
					}
				}
				catch (Exception ex)
				{
					Error($"Failure deleting directory [{directoryPath}] {ex}");
					++logObject[KeySpace.Errors];
					return;
				}

				Log($"{dryRunMessage}Delete directory [{directoryPath}] because it's empty");
				++logObject[KeySpace.Deleted];
			}
			else
			{
				Log($"Directory [{directoryPath}] not deleted because it's not empty");
				++logObject[KeySpace.SkippedNotEmpty];
			}
		}
	}
}
