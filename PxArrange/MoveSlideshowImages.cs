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
		private static readonly Regex regexForImageId = new Regex(@"^(\d+)_.+");
		private static readonly Regex regexForArtistId = new Regex(@"\((\d+)\)");

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

			var imageIdToArtistDirectory = new Dictionary<string, string>();
			var potentiallyOrphanedFiles = new Dictionary<string, OrphanedFileList>();
			var filesToMoveSrcDst = new Dictionary<string, string>();

			while ((filePath = reader.ReadLine()) != null)
			{
				ProcessSingleFile(
					filePath,
					directoriesVisitedSet,
					imageIdToArtistDirectory,
					potentiallyOrphanedFiles,
					filesToMoveSrcDst,
					resultLogData
				);
			}

			ProcessPotentiallyOrphanedFiles2(
				imageIdToArtistDirectory,
				potentiallyOrphanedFiles,
				filesToMoveSrcDst,
				dryRunMessage,
				resultLogData
			);

			MoveFilesSrcDst(filesToMoveSrcDst, dryRunMessage, resultLogData);

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

		/// <summary>
		/// Figure out if a file exists and can be moved.
		/// If there are other files like json or ugoira, see if they exist.
		/// They will either need to be moved or they are orphaned and need to be deleted.
		/// </summary>
		private void ProcessSingleFile(
			string filePath,
			HashSet<string> directoriesVisitedSet,
			Dictionary<string, string> imageIdToArtistDirectory,
			Dictionary<string, OrphanedFileList> potentiallyOrphanedFiles,
			Dictionary<string, string> filesToMoveSrcDst,
			ResultLogData_All resultLogData
		)
		{
			if (string.IsNullOrWhiteSpace(filePath))
			{
				// Blank lines in slideshow are ok.
				return;
			}

			if (filePath.StartsWith(";"))
			{
				// This is a comment in the slideshow.
				return;
			}

			var imageIdString = GetImageIdFromPath(filePath, resultLogData);
			var imageIdIsValid = int.TryParse(imageIdString, out int imageId);
			if (!imageIdIsValid)
			{
				Error($"Couldn't parse an ImageId from the name of file [{filePath}]");
				++resultLogData.Files.Invalid;
				return;
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

				// handle image json file, ugoira zip file, ugoira js file, etc.
				foreach (var extension in PxPaths.OtherExtensionsWithoutDot)
				{
					var otherFilePath = Path.ChangeExtension(filePath, extension);
					if (!File.Exists(otherFilePath))
					{
						continue;
					}

					if (!potentiallyOrphanedFiles.ContainsKey(imageIdString))
					{
						potentiallyOrphanedFiles.Add(imageIdString, new OrphanedFileList());
					}
					potentiallyOrphanedFiles[imageIdString].Add(otherFilePath);
				}

				return;
			}

			//if (!filePath.Contains(Path.DirectorySeparatorChar))
			//{
			//	Log($"Not a file [{filePath}]");
			//	++resultLogData.Files.Invalid;
			//	continue;
			//}

			var artistDirectoryPath = GetArtistDirectoryPath(filePath, resultLogData);
			if (string.IsNullOrEmpty(artistDirectoryPath))
			{
				// An error was already printed, just return.
				return;
			}
			else
			{
				if (!imageIdToArtistDirectory.ContainsKey(imageIdString))
				{
					imageIdToArtistDirectory.Add(imageIdString, artistDirectoryPath);
				}
			}

			// If an image is in a subdirectory, then directoryPath and artistDirectoryPath might be different.
			VisitDirectory(artistDirectoryPath, directoriesVisitedSet);

			var newFilePath = GetNewFilePath(filePath, artistDirectoryPath);

			filesToMoveSrcDst.Add(filePath, newFilePath);

			// handle image json file, ugoira zip file, ugoira js file, etc.
			foreach (var extension in PxPaths.OtherExtensionsWithoutDot)
			{
				var otherFileOldPath = Path.ChangeExtension(filePath, extension);
				var otherFileNewPath = Path.ChangeExtension(newFilePath, extension);
				if (File.Exists(otherFileOldPath))
				{
					filesToMoveSrcDst.Add(otherFileOldPath, otherFileNewPath);
				}
			}
		}

		private string GetImageIdFromPath(string filePath, ResultLogData_All resultLogData)
		{
			var fileName = Path.GetFileName(filePath);
			var matchImageId = regexForImageId.Match(fileName);
			if (!matchImageId.Success)
			{
				Error($"Didn't find ImageId at start of name of file [{filePath}]");
				++resultLogData.Files.Invalid;
				return string.Empty;
			}

			return matchImageId.Groups[1].Value;
		}

		private void ProcessPotentiallyOrphanedFiles(
			Dictionary<string, string> imageIdToArtistDirectory,
			Dictionary<string, OrphanedFileList> potentiallyOrphanedFiles,
			Dictionary<string, string> filesToMoveSrcDst,
			string dryRunMessage,
			ResultLogData_All resultLogData
		)
		{
			foreach (var pair in potentiallyOrphanedFiles)
			{
				var imageId = pair.Key;
				var orphanedFiles = pair.Value;
				if (imageIdToArtistDirectory.ContainsKey(imageId))
				{
					var artistDirectoryPath = imageIdToArtistDirectory[imageId];
					foreach (var oldFilePath in orphanedFiles.FilePaths)
					{
						var newFilePath = GetNewFilePath(oldFilePath, artistDirectoryPath);
						filesToMoveSrcDst.Add(oldFilePath, newFilePath);
					}
				}
				else
				{
					foreach (var orphanedFilePath in orphanedFiles.FilePaths)
					{
						DeleteOrphanedFile(orphanedFilePath, dryRunMessage, resultLogData.Files);
					}
				}
			}
		}

		private void ProcessPotentiallyOrphanedFiles2(
			Dictionary<string, string> imageIdToArtistDirectory,
			Dictionary<string, OrphanedFileList> potentiallyOrphanedFiles,
			Dictionary<string, string> filesToMoveSrcDst,
			string dryRunMessage,
			ResultLogData_All resultLogData
		)
		{
			foreach (var pair in potentiallyOrphanedFiles)
			{
				var imageId = pair.Key;
				var orphanedFiles = pair.Value;

				var doMoveFiles = false;
				var artistDirectoryPath = string.Empty;

				if (imageIdToArtistDirectory.ContainsKey(imageId))
				{
					// In this case, we know that we have already moved files for this imageId into the artistDirectory.
					doMoveFiles = true;
					artistDirectoryPath = imageIdToArtistDirectory[imageId];
				}
				else
				{
					// In this case, we need to check if there are any files for the imageId in the artistDirectory.
					artistDirectoryPath =
						GetArtistDirectoryPath(orphanedFiles.FilePaths[0], resultLogData) ?? string.Empty;
					if (ArtistDirectoryContainsImageId(artistDirectoryPath, imageId))
					{
						doMoveFiles = true;
						imageIdToArtistDirectory.Add(imageId, artistDirectoryPath);
					}
				}

				if (doMoveFiles)
				{
					foreach (var oldFilePath in orphanedFiles.FilePaths)
					{
						var newFilePath = GetNewFilePath(oldFilePath, artistDirectoryPath);
						filesToMoveSrcDst.Add(oldFilePath, newFilePath);
					}
				}
				else
				{
					foreach (var orphanedFilePath in orphanedFiles.FilePaths)
					{
						DeleteOrphanedFile(orphanedFilePath, dryRunMessage, resultLogData.Files);
					}
				}
			}
		}

		private bool ArtistDirectoryContainsImageId(string artistDirectoryPath, string imageId)
		{
			if (string.IsNullOrEmpty(artistDirectoryPath))
			{
				return false;
			}
			if (string.IsNullOrEmpty(imageId))
			{
				return false;
			}

			foreach (var filePath in Directory.EnumerateFiles(artistDirectoryPath))
			{
				var fileName = Path.GetFileName(filePath);
				if (fileName.Contains(imageId))
				{
					return true;
				}
			}
			return false;
		}

		private string GetNewFilePath(string filePath, string artistDirectoryPath)
		{
			var artistDirectoryName = Path.GetFileName(artistDirectoryPath);
			var fileName = Path.GetFileName(filePath);
			var newFilePath = Path.Combine(_outputDirectoryRootPath, artistDirectoryName, fileName);
			return newFilePath;
		}

		private void MoveFilesSrcDst(
			Dictionary<string, string> filesToMoveSrcDst,
			string dryRunMessage,
			ResultLogData_All resultLogData
		)
		{
			foreach (var kvp in filesToMoveSrcDst)
			{
				var oldPath = kvp.Key;
				var newPath = kvp.Value;
				var newDirectory = Path.GetDirectoryName(newPath);

				if (string.IsNullOrWhiteSpace(newDirectory))
				{
					Error($"New file path doesn't have a directory [{newPath}]");
					++resultLogData.Files.Invalid;
					continue;
				}

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
			}
		}

		private string? GetArtistDirectoryPath(string filePath, ResultLogData_All resultLogData)
		{
			var directoryIterator = Path.GetDirectoryName(filePath);
			var artistDirectoryPath = string.Empty;

			// todo: Maybe check the database for the proper artist directory name using the image id from the filename instead?
			// or maybe use the json file for the image if there is one?
			while (directoryIterator is not null)
			{
				var directoryName = Path.GetFileName(directoryIterator);
				var match = regexForArtistId.Match(directoryName);
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
				return null;
			}

			return artistDirectoryPath;
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

		private void DeleteOrphanedFile(string filePath, string dryRunMessage, ResultLogData_Files resultLogData)
		{
			if (!File.Exists(filePath))
			{
				++resultLogData.NotFound;
				return;
			}

			try
			{
				if (!DoDryRun)
				{
					File.Delete(filePath);
				}
			}
			catch (Exception ex)
			{
				Error($"Failure deleting file [{filePath}] {ex}");
				++resultLogData.Errors;
				return;
			}

			Log($"{dryRunMessage}Delete orphaned file [{filePath}]");
			++resultLogData.Deleted;
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

		public int Deleted { get; set; } = 0;

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

	public class OrphanedFileList
	{
		public List<string> FilePaths = new();

		public void Add(string filePath)
		{
			FilePaths.Add(filePath);
		}
	}
}
