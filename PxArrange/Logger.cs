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
		public static readonly List<StreamWriter> LogFiles = new();
		public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions()
		{
			WriteIndented = true,
			AllowTrailingCommas = true,
		};

		static Logger()
		{
			if (ConfigFile.Instance.WriteToLogFile)
			{
				var logFilePath = LogFilePath;
				if (File.Exists(logFilePath))
				{
					File.Delete(logFilePath);
				}
				LogFiles.Add(new StreamWriter(logFilePath, true));

				Console.WriteLine($"Sending output to file [{logFilePath}]");
			}
			if (ConfigFile.Instance.WriteToConsole)
			{
				LogFiles.Add(new StreamWriter(Console.OpenStandardOutput()));
			}
		}

		~Logger()
		{
			foreach (var logFile in LogFiles)
			{
				logFile.Flush();
				logFile.Close();
			}
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
			foreach (var logFile in LogFiles)
			{
				logFile.WriteLine(message);
				logFile.Flush();
			}
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
					sb.Append(ToJsonString(arg));
				}
			}

			return sb;
		}

		public static string ToJsonString(object obj)
		{
			return JsonSerializer.Serialize(obj, JsonOptions);
		}
	}
}
