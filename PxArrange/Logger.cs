using System.Text;
using System.Text.Json;

namespace PxArrange
{
	internal class Logger
	{
		private static class Tags
		{
			public const string Info = "[INFO] ";
			public const string Warn = "[WARN] ";
			public const string Error = "[ERROR] ";
		}

		public static readonly Logger Instance = new Logger();

		public const string LogFileName = @"output.log";
		public static string LogFilePath => Path.GetFullPath(LogFileName);
		public readonly StreamWriter LogFile;
		private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
		{
			WriteIndented = true,
		};

		public Logger()
		{
			var logFilePath = LogFilePath;
			if (File.Exists(logFilePath))
			{
				File.Delete(logFilePath);
			}
			LogFile = new StreamWriter(logFilePath, true);

			Console.WriteLine($"Sending output to file [{logFilePath}]");
		}

		~Logger()
		{
			LogFile.Flush();
			LogFile.Close();
		}

		public void Log(params object[] args)
		{
			LogInner(Tags.Info, args);
		}

		public void Warn(params object[] args)
		{
			LogInner(Tags.Warn, args);
		}

		public void Error(params object[] args)
		{
			LogInner(Tags.Error, args);
		}

		private void LogInner(string tag, params object[] args)
		{
			var message = ArgsToStringBuilder(tag, args);
			LogFile.WriteLine(message);
			LogFile.Flush();
		}

		public StringBuilder ArgsToStringBuilder(string tag, params object[] args)
		{
			var sb = new StringBuilder();

			if (!string.IsNullOrEmpty(tag))
			{
				sb.Append(tag);
			}

			var isFirstLine = true;
			foreach (var arg in args)
			{
				if (isFirstLine)
				{
					isFirstLine = false;
				}
				else if (sb.Length > 0)
				{
					//sb.AppendLine();
					sb.Append(" ");
				}

				if (arg is string)
				{
					sb.Append(arg);
				}
				else if (arg.GetType().IsPrimitive)
				{
					sb.Append(arg.ToString());
				}
				else
				{
					var json = JsonSerializer.Serialize(arg, _jsonOptions);
					sb.Append(json.ToString());
				}
			}

			return sb;
		}
	}
}
