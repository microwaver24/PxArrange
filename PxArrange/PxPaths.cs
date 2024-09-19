namespace PxArrange
{
	public static class PxPaths
	{
		//public static string PixivPath => ConfigFile.Instance.ImagesRootPath;

		//public static string BadPath => Path.Combine(PixivPath, ConfigFile.Instance.ImagesSubdirectories.Bad);

		//public static string FreshPath => Path.Combine(PixivPath, ConfigFile.Instance.ImagesSubdirectories.Fresh);

		//public static string AllPath => Path.Combine(PixivPath, ConfigFile.Instance.ImagesSubdirectories.All);

		//public static string SlideshowDirPath => Path.Combine(PixivPath, ConfigFile.Instance.ImagesSubdirectories.Slideshow);

		public static readonly string PixivPath = ConfigFile.Instance.ImagesRootPath;
		public static readonly string BadPath = Path.Combine(PixivPath, ConfigFile.Instance.ImagesSubdirectories.Bad);
		public static readonly string FreshPath = Path.Combine(
			PixivPath,
			ConfigFile.Instance.ImagesSubdirectories.Fresh
		);
		public static readonly string AllPath = Path.Combine(PixivPath, ConfigFile.Instance.ImagesSubdirectories.All);
		public static readonly string SlideshowDirPath = Path.Combine(
			PixivPath,
			ConfigFile.Instance.ImagesSubdirectories.Slideshow
		);

		/// <summary>
		/// Folders listed here contain folder that are artist names.
		/// </summary>
		public static readonly string[] ArtistPaths = new string[] { FreshPath, AllPath };
		public static readonly string DatabasePath = Path.Combine(ConfigFile.Instance.PixivUtil2RootPath, DbFileName);
		public static readonly string IniFilePath = Path.Combine(
			ConfigFile.Instance.IrfanViewRootPath,
			"Data",
			"IrfanView",
			IniFileName
		);

		public const string DbFileName = "db.sqlite";
		public const string IniFileName = "i_view32.ini";
		public const string SlideshowFileNamePattern = "slideshow{0:D3}.txt";
		public const string SlideshowFileNamePatternWithCount = "slideshow{0:D3}_{1:D}.txt";
		public const string ConfigFileName = "config.json";

		public static string SlideshowPath(int index, int? imageCount = null)
		{
			string fileName;
			if (imageCount is null)
			{
				fileName = string.Format(SlideshowFileNamePattern, index);
			}
			else
			{
				fileName = string.Format(SlideshowFileNamePatternWithCount, index, imageCount);
			}

			return Path.Combine(SlideshowDirPath, fileName);
		}

		public static readonly HashSet<string> ValidImageExtensions = new HashSet<string>
		{
			".png",
			".jpg",
			".jpeg",
			".gif",
			".webm",
			".webp",
		};

		public static readonly string[] OtherExtensionsWithoutDot;

		public static readonly HashSet<string> OtherExtensions = new HashSet<string> { ".zip", ".json", ".zip.js", };

		static PxPaths()
		{
			OtherExtensionsWithoutDot = new string[OtherExtensions.Count];
			int i = 0;
			foreach (var extension in OtherExtensions)
			{
				OtherExtensionsWithoutDot[i] = extension.Substring(1);
				++i;
			}
		}
	}
}
