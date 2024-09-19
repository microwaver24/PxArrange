using System.Text.Json;

namespace PxArrange
{
	public class ConfigFile
	{
		public static readonly ConfigFile Instance;
		public string PixivUtil2RootPath { get; set; } = string.Empty;
		public string IrfanViewRootPath { get; set; } = string.Empty;
		public string ImagesRootPath { get; set; } = string.Empty;
		public ConfigFile_ImagesSubdirectories ImagesSubdirectories { get; set; } = new();
		public bool WriteToLogFile { get; set; } = true;
		public bool WriteToConsole { get; set; } = true;

		static ConfigFile()
		{
			var configFileName = PxPaths.ConfigFileName;
			var configFilePath = Path.GetFullPath(configFileName);

			if (!File.Exists(configFilePath))
			{
				throw new FileNotFoundException($"Config file not found at path [{configFilePath}].", configFilePath);
			}

			try
			{
				var jsonString = File.ReadAllText(configFilePath);
				var configFile = JsonSerializer.Deserialize<ConfigFile>(jsonString);

				if (configFile is null)
				{
					throw new Exception("`JsonSerializer.Deserialize` returned a null object.");
				}

				Instance = configFile;
			}
			catch (Exception ex)
			{
				Instance = new ConfigFile();

				Logger.Instance.Error($"Failed to read config file [{configFilePath}].", ex);
			}
		}

		public static void Init() { }
	}

	public class ConfigFile_ImagesSubdirectories
	{
		public string Fresh { get; set; } = string.Empty;
		public string All { get; set; } = string.Empty;
		public string Bad { get; set; } = string.Empty;
		public string Slideshow { get; set; } = string.Empty;
	}
}
