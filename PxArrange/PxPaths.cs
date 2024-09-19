namespace PxArrange
{
	// todo: allow all of these to be set from a command line arg or settings file.
	public static class PxPaths
	{
		public static readonly string PxRootPath = Path.Combine(@"D:", "H", "Px");

		public static readonly string PixivPath = Path.Combine(PxRootPath, "pixiv");

		public static readonly string BinPath = Path.Combine(PxRootPath, "bin");

		public static readonly string BadPath = Path.Combine(PixivPath, "bad");

		public static readonly string FreshPath = Path.Combine(PixivPath, "fresh");

		public static readonly string AllPath = Path.Combine(PixivPath, "all");

		public static readonly string SlideshowDirPath = Path.Combine(PixivPath, "slideshow");

		/// <summary>
		/// Folders listed here contain folder that are artist names.
		/// </summary>
		public static readonly string[] ArtistPaths = new string[] { FreshPath, AllPath };

		public static readonly string DatabasePath = Path.Combine(BinPath, "pixivutil20230105", DbFileName);

		public static readonly string IniFilePath = Path.Combine(
			BinPath,
			"IrfanViewPortable",
			"Data",
			"IrfanView",
			IniFileName
		);

		public const string DbFileName = "db.sqlite";
		public const string IniFileName = "i_view32.ini";
		public const string SlideshowFileNamePattern = "slideshow{0:D3}.txt";
		public const string SlideshowFileNamePatternWithCount = "slideshow{0:D3}_{1:D}.txt";

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

		public static readonly HashSet<string> ExpectedNonImageExtensions = new HashSet<string> { ".zip", };
	}
}
