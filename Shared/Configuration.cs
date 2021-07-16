using System.IO;
using System.Globalization;
using Xamarin.Essentials;
using static Android.Manifest;


namespace StorageHistory.Shared
{

	static class Configuration
	{

		/// <summary>
		///  The number of seconds that must pass before a new snapshot is started, with 1 hour as the default.
		/// </summary>
		public const string MinSnapshotDuration_KEY= "minSnapshotDuration";
		public const int MinSnapshotDuration_DEFAULT=
			#if DEBUG
				1  // 1 second in debug mode
			#else
				3600  // 1 hour in release mode
			#endif
			;


		public const string EnableBackup_KEY= "enableBackup";
		public const bool EnableBackup_DEFAULT= true;


		public const string EnableStatistics_KEY= "enableStatistics";
		public const bool EnableStatistics_DEFAULT= true;


		/// <summary>
		///  The number of hours we should analyze in <see cref="AnalysisActivity"/>, with 1 month as the default.
		/// </summary>
		public const string AnalysisDuration_KEY= "analysisDuration";
		public const int AnalysisDuration_DEFAULT= 24 * 30;

		/// <summary>
		///  The average size of a monitored directory by directory count.
		/// </summary>
		public const string AvgDirectorySize_KEY= "avgDirectorySize";
		public const int AvgDirectorySize_DEFAULT= 100;



		public static readonly string BackupCache_FOLDER= GetFullPath(".backupCache");


		/// <summary>
		///  Information on how to access the lists of which directories to track and which files to ignore.
		/// </summary>
		public static readonly string DirectoryInclusionList_FILE= GetFullPath("directoryInclusions.bin");
		public static readonly string ExclusionList_FILE= GetFullPath("fileExclusions.bin");
		

		/// <summary>
		///  Information on how to read or update the dictionary of file sizes from app storage.
		/// </summary>
		public static readonly string SizeDictionary_FILE= GetFullPath("fileSizes.bin");


		/// <summary>
		///  Information on how to store or retrieve snapshots from app storage.
		/// </summary>
		public static readonly string StatisticsDatabase_FILE= GetFullPath("directoryStatistics.bin");
			// each snapshot ends with Two consecutive null-terminators  


		/// <summary>
		///  The default permissions this app provides for files it creates (user read/write).
		/// </summary>
		public static readonly int DefaultFilePermissions= Android.Systems.OsConstants.SIrusr | Android.Systems.OsConstants.SIwusr;
		public static readonly int DefaultDirectoryPermissions= DefaultFilePermissions | Android.Systems.OsConstants.SIxusr; // allows entering inside the directory


		/// <summary>
		///  The default date format this app uses when reading/writing internal files.
		/// </summary>
		public static readonly string UniversalDateTimeFormat= DateTimeFormatInfo.InvariantInfo.UniversalSortableDateTimePattern;


		/// <summary>
		///  The default time stamp format this app uses for writing the names of backup up files.
		/// </summary>
		public static readonly string DefaultFileVersionFormat= "yyyy'_'MM'_'dd'_'HH'-'mm'-'ss";


		/// <summary>
		///  Uses `sysconf` to retrieve the system's size of a page in bytes
		/// </summary>
		public static readonly int SystemPageSize= (int)Android.Systems.Os.Sysconf( Android.Systems.OsConstants.ScPageSize );


		public static readonly string[] DefaultPermissionsRequired=
			new string[] {
				Permission.ReadExternalStorage,
				Permission.WriteExternalStorage
			};

		public static readonly EnumerationOptions SafeRecursiveMode= new EnumerationOptions { IgnoreInaccessible= true, RecurseSubdirectories= true, AttributesToSkip= default /* enumerate hidden & system items */ };


		public const int MaxStackAllocLength= 512 * 1024 / 8;

		/// <summary>
		///  Returns the full path for a file in the app's data directory.
		/// </summary>
		public static string GetFullPath(string fileName) => Path.Combine(FileSystem.AppDataDirectory, fileName);

	}
}