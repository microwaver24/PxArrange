#define ENABLE_LOG

using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace PxArrange
{
	public class ImageTable
	{
		public static class ColumnNames
		{
			public const string ImageId = "image_id";
			public const string MemberId = "member_id";
			public const string Title = "title";

			/// <summary>The path including file name where the file is saved.</summary>
			public const string SaveName = "save_name";
			public const string CreatedDate = "created_date";
			public const string LastUpdateDate = "last_update_date";
			public const string IsManga = "is_manga";
		}

		public int ImageId;

		public SqliteConnection? Connection = null;

		public void Connect(string databasePath)
		{
			if (Connection is not null)
			{
				Logger.Instance.Error($"Database is already connected.");
				return;
			}

			var connectionStringBuilder = new SqliteConnectionStringBuilder()
			{
				DataSource = databasePath,
				Mode = SqliteOpenMode.ReadWriteCreate,
			};
			string connectionString = connectionStringBuilder.ToString();
			Connection = new SqliteConnection(connectionString);
		}

		public void Disconnect()
		{
			Connection?.Dispose();
			Connection = null;
		}

		public void Run(string commandText, Action<ImageTableReader> processRow)
		{
			if (Connection is null)
			{
				Logger.Instance.Error($"Database is not connected.");
				return;
			}

			try
			{
				Connection.Open();
				using var command = new SqliteCommand() { Connection = Connection, CommandText = commandText, };

				var tableReader = new ImageTableReader(command.ExecuteReader());

				while (tableReader.Read())
				{
					processRow(tableReader);
				}
			}
			finally
			{
				Connection.Close();
			}
		}
	}

	public class ImageTableReader
	{
		private SqliteDataReader _dbReader;

		private int _imageIdOrdinal;
		private int _memberIdOrdinal;
		private int _titleOrdinal;
		private int _saveNameOrdinal;
		private int _createdDateOrdinal;
		private int _lastUpdateDateOrdinal;
		private int _isMangaOrdinal;

		public int ImageId => _dbReader.GetInt32(_imageIdOrdinal);
		public int MemberId => _dbReader.GetInt32(_memberIdOrdinal);
		public string Title => _dbReader.GetString(_titleOrdinal);
		public string SaveName => _dbReader.GetString(_saveNameOrdinal);
		public DateTime CreatedDate => _dbReader.GetDateTime(_createdDateOrdinal);
		public DateTime LastUpdateDate => _dbReader.GetDateTime(_lastUpdateDateOrdinal);
		public bool IsManga => _dbReader.GetBoolean(_isMangaOrdinal);

		public ImageTableReader(SqliteDataReader dbReader)
		{
			if (dbReader is null)
			{
				throw new ArgumentException($"SqliteDataReader cannot be null.");
			}
			_dbReader = dbReader;

			_imageIdOrdinal = _dbReader.GetOrdinal(ImageTable.ColumnNames.ImageId);
			_memberIdOrdinal = _dbReader.GetOrdinal(ImageTable.ColumnNames.MemberId);
			_titleOrdinal = _dbReader.GetOrdinal(ImageTable.ColumnNames.Title);
			_saveNameOrdinal = _dbReader.GetOrdinal(ImageTable.ColumnNames.SaveName);
			_createdDateOrdinal = _dbReader.GetOrdinal(ImageTable.ColumnNames.CreatedDate);
			_lastUpdateDateOrdinal = _dbReader.GetOrdinal(ImageTable.ColumnNames.LastUpdateDate);
			_isMangaOrdinal = _dbReader.GetOrdinal(ImageTable.ColumnNames.IsManga);
		}

		public bool Read()
		{
			if (_dbReader is null)
			{
				throw new NullReferenceException($"ImageTableReader not initialized.");
			}
			return _dbReader.Read();
		}
	}

	public class Db
	{
		public const string DatabasePath = @"D:\H\Px\bin\pixivutil20230105\db.sqlite";

		public bool DoDryRun;
		private int _imageIdOrdinal = -1;
		private int _memberIdOrdinal = -1;

		public Db(bool doDryRun)
		{
			DoDryRun = doDryRun;
		}

		//public void Run()
		//{
		//	DatabaseStuff();
		//}

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

		//private void DatabaseStuff()
		//{
		//	var connectionStringBuilder = new SqliteConnectionStringBuilder()
		//	{
		//		DataSource = DatabasePath,
		//		Mode = SqliteOpenMode.ReadWriteCreate,
		//	};
		//	string connectionString = connectionStringBuilder.ToString();
		//	using var connection = new SqliteConnection(connectionString);
		//	var filesMoved = 0;

		//	try
		//	{
		//		connection.Open();
		//		using var command = new SqliteCommand()
		//		{
		//			Connection = connection,
		//			CommandText = "SELECT * FROM pixiv_master_image",
		//		};

		//		using var dbReader = command.ExecuteReader();

		//		_imageIdOrdinal = dbReader.GetOrdinal(ColumnNames.ImageId);
		//		_memberIdOrdinal = dbReader.GetOrdinal(ColumnNames.MemberId);

		//		while (dbReader.Read())
		//		{
		//			ProcessRow(dbReader, badFiles, targetDirectories, ref filesMoved);
		//		}
		//	}
		//	finally
		//	{
		//		connection.Close();
		//	}

		//	Log($"Files Moved:", filesMoved);
		//}

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
	}
}
