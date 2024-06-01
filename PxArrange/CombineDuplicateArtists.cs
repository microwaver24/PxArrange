#define ENABLE_LOG

using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace PxArrange
{
	public class CombineDuplicateArtists
	{
		public bool DoDryRun;

		public CombineDuplicateArtists(bool doDryRun)
		{
			DoDryRun = doDryRun;
		}

		public void Run()
		{
			foreach (var rootDirPath in PxPaths.ArtistPaths)
			{
				if (!Directory.Exists(rootDirPath))
				{
					Error($"Root path [{rootDirPath}] is not a directory.");
					continue;
				}

				var targetDirectories = GetTargetDirectories(rootDirPath);
				//Log("targetDirectories", targetDirectories);


				DatabaseStuff(targetDirectories);
			}
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

		private void DatabaseStuff(
			//Dictionary<int, List<string>> badFiles,
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

			try
			{
				connection.Open();
				using var command = new SqliteCommand()
				{
					Connection = connection,
					CommandText = "SELECT * FROM pixiv_master_image",
				};

				//command.Parameters.AddWithValue("@table", "pixiv_master_image");

				//SqlDataAdapter adapter = new SqlDataAdapter();
				//adapter.SelectCommand = new SqlCommand(
				//	queryString, connection);
				//adapter.Fill(dataset);
				//return dataset;

				//command.Parameters.AddWithValue("@id", column.Id);
				//command.Parameters.AddWithValue("@boardId", column.BoardId);
				//command.Parameters.AddWithValue("@columnName", column.ColumnName);
				//command.Parameters.AddWithValue("@maxTaskLimit", column.MaxTaskLimit);
				//command.CommandText = "UPDATE tblColumns SET ColumnName=@columnName,MaxTaskLimit=@maxTaskLimit WHERE Id=@id AND BoardID=@boardId";


				using var dbReader = command.ExecuteReader();
				var imageIdOrdinal = dbReader.GetOrdinal("image_id");
				var memberIdOrdinal = dbReader.GetOrdinal("member_id");
				var lastUpdateDateOrdinal = dbReader.GetOrdinal("last_update_date");

				//var schema = dbReader.GetSchemaTable();
				var columns = new List<string>();
				//for (int i = 0; i < schema.Columns.Count; ++i)
				for (int i = 0; i < dbReader.VisibleFieldCount; ++i)
				{
					var name = dbReader.GetName(i);
					columns.Add($"[{i}] name [{name}]");
					//var column = schema.Columns[i];
					//columns.Add($"[{i}] ordinal [{column.Ordinal}] name [{column.ColumnName}]");
				}
				Log("columns", columns);

				//Log("dbReader", dbReader);

				while (dbReader.Read())
				{
					var imageId = dbReader.GetInt32(imageIdOrdinal);
					var memberId = dbReader.GetInt32(memberIdOrdinal);

					if (targetDirectories.ContainsKey(memberId))
					{
						foreach (var filePath in targetDirectories[memberId])
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
						}
					}

					//var sb = new StringBuilder();
					////sb.Append($"read database: ");

					//for (int i = 0; i < dbReader.VisibleFieldCount; ++i)
					//{
					//	sb.Append(dbReader.GetName(i));
					//	sb.Append(": ");
					//	sb.Append(dbReader[i].ToString());
					//	//sb.Append(dbReader.GetString(i));
					//	if (i < dbReader.VisibleFieldCount - 1)
					//	{
					//		sb.Append(", ");
					//	}
					//}

					//Console.WriteLine(sb.ToString());
				}
			}
			finally
			{
				connection.Close();
			}
		}

		private Dictionary<int, List<string>> GetTargetDirectories(string rootDirPath)
		{
			var outputDirectories = Directory.EnumerateDirectories(rootDirPath);
			var directoryLookup = new Dictionary<int, List<string>>();
			var regex = new Regex(@"\((\d+)\)");
			var duplicates = new HashSet<int>();

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
				else
				{
					duplicates.Add(memberId);
				}
				directoryLookup[memberId].Add(directoryPath);
			}

			if (duplicates.Count > 0)
			{
				var duplicatesOutput = new Dictionary<int, List<string>>();
				foreach (var memberId in duplicates)
				{
					duplicatesOutput.Add(memberId, directoryLookup[memberId]);
				}
				Log($"Found members who changed their name.", duplicatesOutput);
			}

			return directoryLookup;
		}
	}
}
