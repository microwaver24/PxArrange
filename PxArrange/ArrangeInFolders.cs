#define ENABLE_LOG

using System.Diagnostics;
using System.Text.RegularExpressions;
using ManyConsole;

namespace PxArrange
{
	public class ArrangeInFolders : ConsoleCommand
	{
		public bool DoDryRun;

		//private int _imageIdOrdinal = -1;
		//private int _memberIdOrdinal = -1;

		public ArrangeInFolders()
		{
			DoDryRun = true;

			IsCommand("Organize", "Arrange image files into folders.");
			HasLongDescription("Arrange image files into the correct artist folders using the database as a guide.");

			HasOption("d|dry-run", "Do a dry run to see what will be changed.", b => DoDryRun = b != null);
		}

		public override int Run(string[] remainingArguments)
		{
			var badFiles = GetBadFiles();
			//Log("badFiles", badFiles);

			var targetDirectories = GetTargetDirectories();
			//Log("targetDirectories", targetDirectories);

			//DatabaseStuff(badFiles, targetDirectories);

			var imageTable = new ImageTable();
			imageTable.Run("SELECT * FROM pixiv_master_image", ProcessRow);

			void ProcessRow(ImageTableReader tableReader)
			{
				var imageId = tableReader.ImageId;
				var memberId = tableReader.MemberId;

				if (badFiles.ContainsKey(imageId) && targetDirectories.ContainsKey(memberId))
				{
					foreach (var filePath in badFiles[imageId])
					{
						var fileName = Path.GetFileName(filePath);
						var outputDirectoryPath = targetDirectories[memberId][0];
						var outputFilePath = Path.Combine(outputDirectoryPath, fileName);

						var dryRunMessage = DoDryRun ? "DryRun: " : string.Empty;
						Log($"{dryRunMessage}Move [{filePath}] to [{outputFilePath}]");

						if (!DoDryRun)
						{
							File.Move(filePath, outputFilePath);
						}

						//++filesMoved;
					}
					badFiles.Remove(imageId);
				}
			}

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

		/*
		private void DatabaseStuff(
		    Dictionary<int, List<string>> badFiles,
		    Dictionary<int, List<string>> targetDirectories
		)
		{
		    var connectionStringBuilder = new SqliteConnectionStringBuilder()
		    {
		        DataSource = PxPaths.DatabasePath,
		        Mode = SqliteOpenMode.ReadWriteCreate,
		    };
		    string connectionString = connectionStringBuilder.ToString();
		    using var connection = new SqliteConnection(connectionString);
		    var filesMoved = 0;

		    try
		    {
		        connection.Open();
		        using var command = new SqliteCommand()
		        {
		            Connection = connection,
		            CommandText = "SELECT * FROM pixiv_master_image",
		        };

		        using var dbReader = command.ExecuteReader();

		        _imageIdOrdinal = dbReader.GetOrdinal(ImageTable.ColumnNames.ImageId);
		        _memberIdOrdinal = dbReader.GetOrdinal(ImageTable.ColumnNames.MemberId);

		        while (dbReader.Read())
		        {
		            ProcessRow(dbReader, badFiles, targetDirectories, ref filesMoved);
		        }
		    }
		    finally
		    {
		        connection.Close();
		    }

		    Log($"Files Moved:", filesMoved);
		}

		private void ProcessRow(
		    SqliteDataReader dbReader,
		    Dictionary<int, List<string>> badFiles,
		    Dictionary<int, List<string>> targetDirectories,
		    ref int filesMoved
		)
		{
		    var imageId = dbReader.GetInt32(_imageIdOrdinal);
		    var memberId = dbReader.GetInt32(_memberIdOrdinal);

		    if (badFiles.ContainsKey(imageId) && targetDirectories.ContainsKey(memberId))
		    {
		        foreach (var filePath in badFiles[imageId])
		        {
		            var fileName = Path.GetFileName(filePath);
		            var outputDirectoryPath = targetDirectories[memberId][0];
		            var outputFilePath = Path.Combine(outputDirectoryPath, fileName);

		            var dryRunMessage = DoDryRun ? "DryRun: " : string.Empty;
		            Log($"{dryRunMessage}Move [{filePath}] to [{outputFilePath}]");

		            if (!DoDryRun)
		            {
		                File.Move(filePath, outputFilePath);
		            }

		            ++filesMoved;
		        }
		        badFiles.Remove(imageId);
		    }
		}

		private void ProcessRow(
		    ImageTableReader tableReader,
		    SqliteDataReader dbReader,
		    Dictionary<int, List<string>> badFiles,
		    Dictionary<int, List<string>> targetDirectories,
		    ref int filesMoved
		)
		{
		    var imageId = dbReader.GetInt32(_imageIdOrdinal);
		    var memberId = dbReader.GetInt32(_memberIdOrdinal);

		    if (badFiles.ContainsKey(imageId) && targetDirectories.ContainsKey(memberId))
		    {
		        foreach (var filePath in badFiles[imageId])
		        {
		            var fileName = Path.GetFileName(filePath);
		            var outputDirectoryPath = targetDirectories[memberId][0];
		            var outputFilePath = Path.Combine(outputDirectoryPath, fileName);

		            var dryRunMessage = DoDryRun ? "DryRun: " : string.Empty;
		            Log($"{dryRunMessage}Move [{filePath}] to [{outputFilePath}]");

		            if (!DoDryRun)
		            {
		                File.Move(filePath, outputFilePath);
		            }

		            ++filesMoved;
		        }
		        badFiles.Remove(imageId);
		    }
		}
		*/

		private Dictionary<int, List<string>> GetBadFiles()
		{
			var enumerationOptions = new EnumerationOptions() { RecurseSubdirectories = true, };
			var originalFiles = Directory.EnumerateFiles(PxPaths.BadPath, "*", enumerationOptions);

			var fileLookup = new Dictionary<int, List<string>>();

			foreach (var filePath in originalFiles)
			{
				var fileName = Path.GetFileNameWithoutExtension(filePath);
				var parts = fileName.Split('_');
				var imageIdString = parts[0];
				var imageIdIsValid = int.TryParse(imageIdString, out int imageId);
				if (!imageIdIsValid)
				{
					Error($"Couldn't parse an ImageId from the filename [{filePath}]");
					continue;
				}

				if (!fileLookup.ContainsKey(imageId))
				{
					fileLookup.Add(imageId, new List<string>());
				}
				fileLookup[imageId].Add(filePath);
			}

			return fileLookup;
		}

		private Dictionary<int, List<string>> GetTargetDirectories()
		{
			var outputDirectories = Directory.EnumerateDirectories(PxPaths.FreshPath);
			var directoryLookup = new Dictionary<int, List<string>>();
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

				if (!directoryLookup.ContainsKey(memberId))
				{
					directoryLookup.Add(memberId, new List<string>());
				}
				directoryLookup[memberId].Add(directoryPath);
			}

			return directoryLookup;
		}
	}
}
