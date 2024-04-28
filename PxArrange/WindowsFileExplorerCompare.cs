#define ENABLE_LOG

using System.Runtime.InteropServices;

namespace PxArrange
{
	public class WindowsFileExplorerCompare : IComparer<string>
	{
		[DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
		private static extern int StrCmpLogicalW(String x, String y);

		public int Compare(string? x, string? y)
		{
			if (x is null)
			{
				if (y is null)
				{
					return 0;
				}
				return 1;
			}
			if (y is null)
			{
				return -1;
			}

			return StrCmpLogicalW(x, y);
		}
	}
}
