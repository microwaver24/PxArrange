using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace PxArrange
{
	internal class Program
	{
		private const string _badPath = @"D:\H\Px\pixiv\bad";
		private const string _freshPath = @"D:\H\Px\pixiv\fresh";
		private const string _databasePath = @"D:\H\Px\bin\pixivutil20230105\db.sqlite";
		private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
		{
			WriteIndented = true,
		};

		private static void Main(string[] args)
		{
			Console.WriteLine("start");

			var badFiles = GetBadFiles();
			//{
			//	var json = JsonSerializer.Serialize(badFiles, _jsonOptions);
			//	Console.WriteLine(json);
			//}

			var targetDirectories = GetTargetDirectories();
			//{
			//	var json = JsonSerializer.Serialize(targetDirectories, _jsonOptions);
			//	Console.WriteLine(json);
			//}

			DatabaseStuff(badFiles, targetDirectories);

			Console.WriteLine($"end");
		}

		private static void DatabaseStuff(
			Dictionary<int, List<string>> badFiles,
			Dictionary<int, List<string>> targetDirectories
		)
		{
			var connectionStringBuilder = new SqliteConnectionStringBuilder()
			{
				DataSource = _databasePath,
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
				//var schema = dbReader.GetSchemaTable();
				var imageIdOrdinal = dbReader.GetOrdinal("image_id");
				var memberIdOrdinal = dbReader.GetOrdinal("member_id");

				using StreamWriter logFile = new StreamWriter($"output.log");
				while (dbReader.Read())
				{
					var imageId = dbReader.GetInt32(imageIdOrdinal);
					var memberId = dbReader.GetInt32(memberIdOrdinal);

					if (badFiles.ContainsKey(imageId) && targetDirectories.ContainsKey(memberId))
					{
						foreach (var filePath in badFiles[imageId])
						{
							var fileName = Path.GetFileName(filePath);
							var outputDirectoryPath = targetDirectories[memberId][0];
							var outputFilePath = Path.Combine(outputDirectoryPath, fileName);

							logFile.WriteLine($"Move [{filePath}] to [{outputFilePath}]");
							//Console.WriteLine($"Gonna move [{filePath}] to [{outputFilePath}]");

							File.Move(filePath, outputFilePath);
						}
						badFiles.Remove(imageId);
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

		private static Dictionary<int, List<string>> GetBadFiles()
		{
			var enumerationOptions = new EnumerationOptions() { RecurseSubdirectories = true, };
			var originalFiles = Directory.EnumerateFiles(_badPath, "*", enumerationOptions);

			var fileLookup = new Dictionary<int, List<string>>();

			foreach (var filePath in originalFiles)
			{
				var fileName = Path.GetFileNameWithoutExtension(filePath);
				var parts = fileName.Split('_');
				var imageIdString = parts[0];
				var imageIdIsValid = int.TryParse(imageIdString, out int imageId);
				if (!imageIdIsValid)
				{
					Console.Error.WriteLine($"Couldn't parse an ImageId from the filename [{filePath}]");
					//throw new FormatException($"Couldn't parse an ImageId from the filename [{file}]");
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

		private static Dictionary<int, List<string>> GetTargetDirectories()
		{
			var outputDirectories = Directory.EnumerateDirectories(_freshPath);

			//{
			//	var json = JsonSerializer.Serialize(outputDirectories, _jsonOptions);
			//	Console.WriteLine(json);
			//}

			var directoryLookup = new Dictionary<int, List<string>>();
			var regex = new Regex(@"\((\d+)\)");

			foreach (var directoryPath in outputDirectories)
			{
				var directoryName = Path.GetFileName(directoryPath);
				if (directoryName is null)
				{
					Console.Error.WriteLine($"Directory path is not actually a directory [{directoryPath}]");
					continue;
				}

				var match = regex.Match(directoryName);
				if (!match.Success)
				{
					Console.Error.WriteLine($"Unexpected format for directory name [{directoryName}]");
					continue;
				}

				var memberIdString = match.Groups[1].Value;
				var memberIdIsValid = int.TryParse(memberIdString, out int memberId);
				if (!memberIdIsValid)
				{
					Console.Error.WriteLine($"Couldn't parse a MemberId from the directory name [{directoryName}]");
					continue;
				}

				if (!directoryLookup.ContainsKey(memberId))
				{
					directoryLookup.Add(memberId, new List<string>());
				}
				else
				{
					Console.WriteLine($"Found a member who changed their name. [{memberId}]");
				}
				directoryLookup[memberId].Add(directoryPath);
			}

			return directoryLookup;
		}
	}
}
