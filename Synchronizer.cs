using Java.IO; // has classes for interfacing w/ Android Subsystem
using Android.Systems; // interfaces with the device's low-level Linux kernel
using Xamarin.Essentials; // for preferences
using System.Collections.Generic;
using System.IO.Compression;


namespace StorageHistory
{
	using System.Collections;
	using Helpers;
	using static Helpers.Configuration;


	public enum FileChangeType
	{

		/// <summary>
		///  When the event is a result of configuration changes.
		/// </summary>
		None,
		 
		/// <summary>
		///   When a file moves within/across monitored directories.
		/// </summary>
		Move,

		/// <summary>
		///  When a file is actually created or is moved from a unmonitored directory to a monitored one.
		/// </summary>
		Creation,
		
		/// <summary>
		///  When a file changes but doesn't move.
		/// </summary>
		Modification,

		/// <summary>
		///  When a file is actually deleted or is moved from a monitored directory to an unmonitored one. 
		/// </summary>
		Deletion

	}

	public struct FileChange {
		public string AbsoluteLocation;
		public FileChangeType Type;
		public static implicit operator FileChange(string str) => new FileChange { AbsoluteLocation= str };
	}

	/// <summary>
	///  Receives file changes from <see cref="StorageObserverService"/> and configuration changes from <see cref="ConfigurationActivity"/>.
	///  managing backup, updating statistics, and marshalling user preferences as needed. 
	/// </summary>
	static class Synchronizer
	{
		private static HashSet<string> directoryInclusions;
		private static HashSet<string> fileExclusions;

		/// <summary>
		///  Called when either the app or the storage monitor is closing.
		/// </summary>
		public static void OnExit()
		{
			StatisticsManager.SaveSnapshot(); // makes sure unsaved data is saved
			// UploadBackupCache();
		}

		/// <summary>
		///  Called when an error is encountered but not thrown.
		/// </summary>
		public delegate void ErrorCallback(string errorMessage);

		/// <summary>
		///  Performs a backup and/or statistics update of the given file based on user preferences.
		/// </summary>
		/// <remarks>
		///  This method assumes it's only called for user-selected directories.
		/// </remarks>
		public static void OnFileChange(string location, FileChangeType fileChange)
		{

			location= System.IO.Path.GetFullPath(location); // need absolute file path

			if ( fileExclusions == null ) {
				var unixFile= Android.Systems.Os.Open(ExclusionList_FILE, OsConstants.ORdonly | OsConstants.OCreat,  DefaultFilePermissions);
				fileExclusions= unixFile.GetStrings(); // reads file into memory
				Android.Systems.Os.Close(unixFile); // no longer needed
			}
				
			if ( ! fileExclusions.Contains(location) ) // changed file is not in exclude-list
			{

				if ( Preferences.Get(EnableStatistics_KEY, EnableStatistics_DEFAULT) ) {
					var unixFile= Android.Systems.Os.Open(SizeDictionary_FILE, OsConstants.ORdwr | OsConstants.OCreat,  DefaultFilePermissions);
					updateStatistics(location, sizeDictionaryFile: unixFile);
					Android.Systems.Os.Close(unixFile); // no longer needed
				}

				if ( Preferences.Get(EnableBackup_KEY, EnableBackup_DEFAULT) && fileChange != FileChangeType.Deletion )
					updateBackupCache(location, skipExisting: fileChange == FileChangeType.None);

			}

		}

		/// <summary>
		///  Updates file statistics and/or the local backup store based on user preferences.
		/// </summary>
		/// <remarks>
		///  This method assumes it's only called for user-selected directories.
		/// </remarks>
		public static void OnFileChanges(IEnumerable<FileChange> changes)
		{
			bool enableBackup= Preferences.Get(EnableBackup_KEY, EnableBackup_DEFAULT),
			     enableStatistics= Preferences.Get(EnableStatistics_KEY, EnableStatistics_DEFAULT);

			if ( enableBackup || enableStatistics )
			{
				FileDescriptor sizeDictionaryFile= null;

				if ( fileExclusions == null ) {
					var unixFile= Android.Systems.Os.Open(ExclusionList_FILE, OsConstants.ORdonly | OsConstants.OCreat,  DefaultFilePermissions);
					fileExclusions= unixFile.GetStrings(); // reads file into memory
					Android.Systems.Os.Close(unixFile); // no longer needed
				}

				if ( enableStatistics )
					sizeDictionaryFile= Android.Systems.Os.Open(SizeDictionary_FILE, OsConstants.ORdwr | OsConstants.OCreat,  DefaultFilePermissions);

				foreach ( FileChange fileChange in changes )
					if ( ! fileExclusions.Contains(fileChange.AbsoluteLocation) ) // changed file is not in exclude-list
					{
						if ( enableStatistics )
							updateStatistics(fileChange.AbsoluteLocation, sizeDictionaryFile);

						if ( enableBackup && fileChange.Type != FileChangeType.Deletion )
							updateBackupCache(fileChange.AbsoluteLocation, skipExisting: fileChange.Type == FileChangeType.None);
					}

				if ( sizeDictionaryFile != null )
					Android.Systems.Os.Close(sizeDictionaryFile);  // release the handle

			}
		}
		
		/// <summary>
		///  Internal method for backing up a single file.
		/// </summary>
		/// <param name="filePath"> The absolute path of the file to back up. </param>
		/// <param name="skipExisting"> Skips backup creation if a backup for the file has already been created. </param>
		private static void updateBackupCache(string filePath, bool skipExisting= false)
		{
			if ( filePath != BackupCache_FOLDER ) // makes sure we don't backup our backups!
				if ( ! filePath.IsChildOf( BackupCache_FOLDER ) ) // uses the IsChildOf static method in our `RuntimeExtensions.cs` helper
				{
					System.Diagnostics.Debug.Assert( filePath[0] == '/' ); // file paths given to updateBackupCache must be absolute
					string backupFilePath= BackupCache_FOLDER + filePath + ".zip",
					       backupFileVersion= System.DateTime.UtcNow.ToString(DefaultFileVersionFormat) + System.IO.Path.GetExtension(filePath);

					#region Add File To Zip Carrying Previous Versions

						#region Uses the `mkdir` system call for each directory the backup file needs
						{
							int nextSlashIndex= backupFilePath.IndexOf('/', 1); // finds the first non-root slash in the path
			
							while ( nextSlashIndex >= 0 )
							{
								string backupDir= backupFilePath.Substring(0, nextSlashIndex); 
								try {
									Android.Systems.Os.Mkdir(backupDir, DefaultDirectoryPermissions );
								} catch ( System.Exception e ) {
									var x= e;
								}

								nextSlashIndex= backupFilePath.IndexOf('/', nextSlashIndex+1); // finds the next slash in the directory path
							}
						}
						#endregion

						try { 
							using ( var fileStream= new System.IO.FileStream(backupFilePath, System.IO.FileMode.OpenOrCreate) )
								using ( var zipArchive= new ZipArchive(fileStream, ZipArchiveMode.Update) )
									if ( ! skipExisting || zipArchive.Entries.Count == 0 )
										zipArchive.CreateEntryFromFile( filePath, backupFileVersion );
						} catch ( System.IO.InvalidDataException ) {
							Android.Systems.Os.Rename(backupFilePath, backupFilePath+backupFileVersion); // backup the invalid backup
							using ( var fileStream= new System.IO.FileStream(backupFilePath, System.IO.FileMode.Create) )
								using ( var zipArchive= new ZipArchive(fileStream, ZipArchiveMode.Create) )
									zipArchive.CreateEntryFromFile( filePath, backupFileVersion );
						} 
						catch ( System.IO.IOException ) { }

					#endregion
				}
		}

		/// <summary>
		///  Internal method for updating the size of a single file and its parent directories.
		/// </summary>
		/// <param name="filePath"> The absolute path of the file to update. </param>
		private static void updateStatistics(string filePath, FileDescriptor sizeDictionaryFile)
		{
			int fileSizeDelta;
			long oldSizeOffset;

			var oldSizeStr= sizeDictionaryFile.ReadDictionaryEntry(filePath, out oldSizeOffset);
			int.TryParse(oldSizeStr, out fileSizeDelta); // reads the file's old size from the size dictionary as an integer

			#region Retrieve New File Size With System Call To Linux Kernel

				string newFileSizeStr= "0";
				try {
					var newFileSize= Android.Systems.Os.Lstat(filePath).StSize;
					newFileSizeStr= newFileSize.ToString();
					fileSizeDelta= (int)( newFileSize - fileSizeDelta );
				}
				catch ( System.Exception ) {
					fileSizeDelta= -fileSizeDelta;  // file lost all of its former size
				}

			#endregion

			if ( fileSizeDelta != 0 )
			{

				#region Update Size Dictionary With New File-Size

					if ( oldSizeStr != null )
						sizeDictionaryFile.ReplaceDictionaryEntry(newFileSizeStr, oldSizeOffset, oldSizeStr.Length);
					else {
						filePath.WriteTo(sizeDictionaryFile); // writes the key
						newFileSizeStr.WriteTo(sizeDictionaryFile); // writes the value
					}

				#endregion

				// calls the statistics manager to composite this file-size change with any others
				StatisticsManager.AddDirectory( System.IO.Path.GetDirectoryName(filePath), fileSizeDelta );

			}
		}


		/// <summary>
		///  Adds a directory to the list of directories this app should keep track of.
		/// </summary>
		/// <param name="onError"> Currently unused as this method doesn't catch any errors. </param>
		public static void AddDirectory(string absolutePath, ErrorCallback onError= null)
		{
			FileDescriptor unixFile= null;

			if ( directoryInclusions == null ) {
				unixFile= Android.Systems.Os.Open(DirectoryInclusionList_FILE, OsConstants.ORdwr | OsConstants.OCreat,  DefaultFilePermissions);
				directoryInclusions= unixFile.GetStrings(); // reads the entire file such that the read/write offset should now be EOF
			}

			if ( directoryInclusions.Add(absolutePath) ) // directory wasn't in the collection
			{
				if ( unixFile == null )
					unixFile= Android.Systems.Os.Open(DirectoryInclusionList_FILE, OsConstants.OWronly | OsConstants.OCreat | OsConstants.OAppend,  DefaultFilePermissions);
				
				absolutePath.WriteTo(unixFile); // appends the directory to the end of the file
				
				System.IO.Directory.CreateDirectory(absolutePath); // creates the directory if it doesn't already exist
				OnFileChanges( System.IO.Directory.EnumerateFiles(absolutePath, "*.*", SafeRecursiveMode).ToFileChanges() );
			}
			
			if ( unixFile != null )
				Android.Systems.Os.Close(unixFile); // file no longer needed
		}

		/// <summary>
		///  Removes a directory from the list of directories this app should keep track of.
		/// </summary>
		/// <param name="onError"> Currently unused as this method doesn't catch any errors. </param>
		public static void RemoveDirectory(string absolutePath, ErrorCallback onError= null)
		{
			FileDescriptor unixFile= null;

			if ( directoryInclusions == null ) {
				unixFile= Android.Systems.Os.Open(DirectoryInclusionList_FILE, OsConstants.ORdwr | OsConstants.OCreat,  DefaultFilePermissions);
				directoryInclusions= unixFile.GetStrings(); // reads the entire file such that the read/write offset should now be EOF
			}

			if ( directoryInclusions.Remove(absolutePath) ) // directory was in the collection
			{
				if ( unixFile == null )
					unixFile= Android.Systems.Os.Open(DirectoryInclusionList_FILE, OsConstants.OWronly | OsConstants.OCreat,  DefaultFilePermissions);
				else Android.Systems.Os.Lseek(unixFile, 0, OsConstants.SeekSet);
				
				directoryInclusions.WriteTo(unixFile); // rewrites the collection of directories to the list file
			}
			
			if ( unixFile != null )
				Android.Systems.Os.Close(unixFile); // file no longer needed

		}


		/// <summary>
		///  Retrieves the set of directories that this app should keep track of.
		/// </summary>
		public static HashSet<string> GetDirectories()
		{
			if ( directoryInclusions == null ) // if the inclusions file hasn't already been read into memory
			{
				var unixFile= Android.Systems.Os.Open(DirectoryInclusionList_FILE, OsConstants.ORdonly | OsConstants.OCreat,  DefaultFilePermissions);
				directoryInclusions= unixFile.GetStrings(); // reads the entire file as a set of strings
				Android.Systems.Os.Close(unixFile); // file no longer needed
			}
			return new HashSet<string> ( directoryInclusions ); // create a copy for the outside world; don't allow direct access
		}



		/// <summary>
		///  Adds a file to the list of files this app should ignore.
		/// </summary>
		/// <param name="onError"> Currently unused as this method doesn't catch any errors. </param>
		public static void IgnoreFile(string absolutePath, ErrorCallback onError= null)
		{
			FileDescriptor unixFile= null;

			if ( fileExclusions == null ) {
				unixFile= Android.Systems.Os.Open(ExclusionList_FILE, OsConstants.ORdwr | OsConstants.OCreat,  DefaultFilePermissions);
				fileExclusions= unixFile.GetStrings(); // reads the entire file such that the read/write offset should now be EOF
			}

			if ( fileExclusions.Add(absolutePath) ) // file wasn't in the collection
			{

				if ( unixFile == null )
					unixFile= Android.Systems.Os.Open(ExclusionList_FILE, OsConstants.OWronly | OsConstants.OCreat | OsConstants.OAppend,  DefaultFilePermissions);
				
				absolutePath.WriteTo(unixFile); // appends the filepath to the end of the file

			}
			
			if ( unixFile != null )
				Android.Systems.Os.Close(unixFile); // file no longer needed

		}

		/// <summary>
		///  Deleting files from backup is not yet implemented.
		/// </summary>
		public static void DeleteFile(string path, uint versionId_min= 0, uint versionId_max= 0, ErrorCallback onError= null) {
			onError?.Invoke("Deleting files from backup is not yet implemented.");
		}

		/// <summary>
		///  Restores a specific version of a file from backup.
		/// </summary>
		public static void RestoreFile(string path, long versionId, ErrorCallback onError= null)
		{
			bool success= false;

			path= System.IO.Path.GetFullPath(path);
			string backupFilePath= BackupCache_FOLDER + path + ".zip";
			string backupFileVersion= new System.DateTime(versionId*System.TimeSpan.TicksPerSecond).ToString(DefaultFileVersionFormat) + System.IO.Path.GetExtension(path);

			#region Restore Previous Version Of File From Zip Back To Outside World

				try {
					using ( var fileStream= new System.IO.FileStream(backupFilePath, System.IO.FileMode.Open) )
						using ( var zipArchive= new ZipArchive(fileStream, ZipArchiveMode.Read) )
						{
							
							var previousVersion= zipArchive.GetEntry(backupFileVersion);
							if ( previousVersion != null )
								previousVersion.ExtractToFile(path, true);

							success= true;
						}
				} catch ( System.Exception ) { }


			#endregion

			if ( ! success )
				onError?.Invoke("Previous version of file could not be located!");
		}

		/// <summary>
		///  Uploading the local backup store is not yet implemented.
		/// </summary>
		public static void UploadBackupCache(ErrorCallback onError= null) {
			onError?.Invoke("Uploading the local backup store is not yet implemented.");
		}

		/// <summary>
		///  Deleting the local backup store is not yet implemented.
		/// </summary>
		public static bool DeleteBackupCache(bool force, ErrorCallback onError= null) {
			onError?.Invoke("Deleting the local backup store is not yet implemented.");
			return false;
		}

	}
}