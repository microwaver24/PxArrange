#define ENABLE_LOG

using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ManyConsole;

namespace PxArrange
{
	public class MoveSlideshowImages : ConsoleCommand
	{
		public bool DoDryRun;
		private int _slideshowIndex;
		private static readonly string _outputDirectoryRootPath_Default = PxPaths.AllPath;
		private string _outputDirectoryRootPath;

		public MoveSlideshowImages()
		{
			DoDryRun = true;
			_slideshowIndex = 0;
			_outputDirectoryRootPath = _outputDirectoryRootPath_Default;

			IsCommand("MoveSlideshow", "Move the images of a slideshow to a new folder.");
			HasLongDescription("Take the images in a slideshow in the fresh folder and move them to the all folder.");

			HasOption(
				"d|dry-run:",
				"Do a dry run to see what will be changed.",
				b => DoDryRun = b == null ? true : bool.Parse(b)
			);

			HasOption(
				"i|index=",
				"Index of the slideshow to move, starting at 0.",
				s => _slideshowIndex = int.Parse(s)
			);

			HasOption(
				"o|output-path=",
				$"Root directory to move the images to. Default is [{_outputDirectoryRootPath_Default}]",
				s => _outputDirectoryRootPath = s ?? _outputDirectoryRootPath_Default
			);

			if (_slideshowIndex < 0)
			{
				throw new Exception($"Argument 'index' must be >= 0");
			}

			// Maybe validate _outputDirectoryRootPath?
		}

		public override int Run(string[] remainingArguments)
		{
			Log(
				$"MoveSlideshowImages: slideshow index [{_slideshowIndex}] output directory root [{_outputDirectoryRootPath}]"
			);

			Directory.CreateDirectory(PxPaths.SlideshowDirPath);

			ReadFromFile();

			return Program.Success;
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

		private List<string> ReadFromFile()
		{
			var regex = new Regex(@"\((\d+)\)");
			var dryRunMessage = DoDryRun ? "DryRun: " : string.Empty;
			var imagePathList = new List<string>();

			var resultLogData = new ResultLogData_All() { OutputDirectoryRoot = _outputDirectoryRootPath, };

			var slideshowFilePath = PxPaths.SlideshowPath(_slideshowIndex);
			if (!File.Exists(slideshowFilePath))
			{
				Error($"Slideshow for index [{_slideshowIndex}] file does not exist [{slideshowFilePath}]");
				return imagePathList;
			}

			using var reader = new StreamReader(slideshowFilePath, Encoding.Unicode);
			var directoriesVisitedSet = new HashSet<string>();
			var filePath = string.Empty;

			while ((filePath = reader.ReadLine()) != null)
			{
				if (string.IsNullOrWhiteSpace(filePath))
				{
					continue;
				}

				++resultLogData.Files.ImagesInSlideshow;

				var directoryPath = Path.GetDirectoryName(filePath);
				if (!string.IsNullOrEmpty(directoryPath))
				{
					VisitDirectory(directoryPath, directoriesVisitedSet);
				}

				if (!File.Exists(filePath))
				{
					Log($"File does not exist [{filePath}]");
					++resultLogData.Files.NotFound;
					continue;
				}

				//if (!filePath.Contains(Path.DirectorySeparatorChar))
				//{
				//	Log($"Not a file [{filePath}]");
				//	++resultLogData.Files.Invalid;
				//	continue;
				//}

				var fileName = Path.GetFileName(filePath);
				var directoryIterator = Path.GetDirectoryName(filePath);
				var artistDirectoryPath = string.Empty;

				// todo: Maybe check the database for the proper artist directory name using the image id from the filename instead?
				// or maybe use the json file for the image if there is one?
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
					++resultLogData.Files.Invalid;
					continue;
				}

				// If an image is in a subdirectory, then directoryPath and artistDirectoryPath might be different.
				VisitDirectory(artistDirectoryPath, directoriesVisitedSet);

				var artistDirectoryName = Path.GetFileName(artistDirectoryPath);
				var newDirectory = Path.Combine(_outputDirectoryRootPath, artistDirectoryName);
				var newFilePath = Path.Combine(_outputDirectoryRootPath, artistDirectoryName, fileName);

				var filesToMove = new Dictionary<string, string> { { filePath, newFilePath }, };

				// handle image json file, ugoira zip file, ugoira js file
				foreach (var extension in PxPaths.OtherExtensionsWithoutDot)
				{
					// todo: Parse image id from path, look for all other images with matching id. It looks like the json file has a pageNumber = imageCount-1 instead of p0.
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

						var fileExtension = Path.GetExtension(newPath);
						if (PxPaths.ValidImageExtensions.Contains(fileExtension))
						{
							++resultLogData.Files.Moved.Total;
							++resultLogData.Files.Moved.Images;
						}
						else if (PxPaths.OtherExtensions.Contains(fileExtension))
						{
							++resultLogData.Files.Moved.Total;
							++resultLogData.Files.Moved.Other;
						}
						else
						{
							++resultLogData.Files.Skipped;
						}
					}
					catch (Exception ex)
					{
						Error($"Failure moving file [{oldPath}] {ex}");
						++resultLogData.Files.Errors;
						continue;
					}

					//++resultLogData.Files.Moved;
				}

				//++resultLogData.Files.MovedImages;
			}

			resultLogData.Files.ComputeTotal();
			resultLogData.Directories.Total = directoriesVisitedSet.Count();

			foreach (var directoryPath in directoriesVisitedSet)
			{
				DeleteDirectoryIfEmpty(directoryPath, dryRunMessage, resultLogData.Directories);
			}

			var jsonString = Logger.ToJsonString(resultLogData);
			Log(
				$"{dryRunMessage}Completed successfully"
					+ $"\nOutput Directory Root Path [{_outputDirectoryRootPath}]"
					+ $"\n{jsonString}"
			);

			return imagePathList;
		}

		private void VisitDirectory(string directoryPath, HashSet<string> directoriesVisitedSet)
		{
			directoriesVisitedSet.Add(directoryPath);
		}

		private void DeleteDirectoryIfEmpty(
			string directoryPath,
			string dryRunMessage,
			ResultLogData_Directories resultLogData
		)
		{
			if (!Directory.Exists(directoryPath))
			{
				++resultLogData.NotFound;
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
					++resultLogData.Errors;
					return;
				}

				Log($"{dryRunMessage}Delete directory [{directoryPath}] because it's empty");
				++resultLogData.Deleted;
			}
			else
			{
				Log($"Directory [{directoryPath}] not deleted because it's not empty");
				++resultLogData.Skipped;
			}
		}

		//private void UpdateDatabase()
		//{
		//	var imageTable = new ImageTable();
		//	imageTable.Run("SELECT * FROM pixiv_master_image", ProcessRow);

		//	void ProcessRow(ImageTableReader tableReader)
		//	{
		//		var imageId = tableReader.ImageId;
		//		var memberId = tableReader.MemberId;

		//		tableReader.SaveName =

		//		if (badFiles.ContainsKey(imageId) && targetDirectories.ContainsKey(memberId))
		//		{
		//			foreach (var filePath in badFiles[imageId])
		//			{
		//				var fileName = Path.GetFileName(filePath);
		//				var outputDirectoryPath = targetDirectories[memberId][0];
		//				var outputFilePath = Path.Combine(outputDirectoryPath, fileName);

		//				var dryRunMessage = DoDryRun ? "DryRun: " : string.Empty;
		//				Log($"{dryRunMessage}Move [{filePath}] to [{outputFilePath}]");

		//				if (!DoDryRun)
		//				{
		//					File.Move(filePath, outputFilePath);
		//				}

		//				//++filesMoved;
		//			}
		//			badFiles.Remove(imageId);
		//		}
		//	}
		//}
	}

	public class ResultLogData_FilesMoved
	{
		public int Images { get; set; } = 0;
		public int Other { get; set; } = 0;
		public int Total { get; set; } = 0;
	}

	public class ResultLogData_Files
	{
		public ResultLogData_FilesMoved Moved { get; set; } = new();

		[JsonPropertyName("Skipped (Unexpected Type)")]
		public int Skipped { get; set; } = 0;

		[JsonPropertyName("Not Found")]
		public int NotFound { get; set; } = 0;

		public int Invalid { get; set; } = 0;

		public int Errors { get; set; } = 0;

		// todo: Maybe have a list of the error files?

		[JsonPropertyName("Images in Slideshow")]
		public int ImagesInSlideshow { get; set; } = 0;

		public int Total { get; set; } = 0;

		public void ComputeTotal()
		{
			Total = Moved.Total + Skipped + NotFound + Invalid + Errors;
		}
	}

	public class ResultLogData_Directories
	{
		public int Deleted { get; set; } = 0;

		[JsonPropertyName("Skipped (Not Empty)")]
		public int Skipped { get; set; } = 0;

		[JsonPropertyName("Not Found")]
		public int NotFound { get; set; } = 0;

		public int Errors { get; set; } = 0;

		// todo: Maybe have a list of the error files?

		public int Total { get; set; } = 0;
	}

	public class ResultLogData_All
	{
		public string OutputDirectoryRoot { get; set; } = string.Empty;
		public ResultLogData_Files Files { get; set; } = new();
		public ResultLogData_Directories Directories { get; set; } = new();
	}
}
