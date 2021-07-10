using Java.IO; // has classes for interfacing w/ Android Subsystem
using Android.Systems; // interfaces with the device's low-level Linux kernel
using Xamarin.Essentials; // for preferences
using System.Collections.Generic;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace StorageHistory.Collection
{
	using Shared.Logic;
	using static Shared.Configuration;

	/// <summary>
	///  Receives file changes from <see cref="StorageObserverService"/> and configuration changes from <see cref="ConfigurationActivity"/>.
	///   managing backups, updating statistics, and marshalling user preferences as needed. 
	/// </summary>
	static class Synchronizer
	{
		private static HashSet<string> directoryInclusions;
		private static HashSet<string> fileExclusions;

		private static readonly object DirectoryInclusionBarrier= new object();
		private static readonly ReaderWriterLockSlim FileExclusionLock= new ReaderWriterLockSlim();

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
		public static void OnFileChange(string absoluteLocation, FileChangeType fileChange)
		{
			if ( fileExclusions == null )
				initFileExclusions();

			bool fileExcluded;
			FileExclusionLock.EnterReadLock();
			try {
				fileExcluded= fileExclusions.Contains(absoluteLocation);
			} finally {
				FileExclusionLock.ExitReadLock();
			}

			if ( ! fileExcluded ) // changed file is not in exclude-list
			{

				if ( Preferences.Get(EnableStatistics_KEY, EnableStatistics_DEFAULT) ) {
					var unixFile= Os.Open(SizeDictionary_FILE, OsConstants.ORdwr | OsConstants.OCreat,  DefaultFilePermissions);
					updateStatistics(absoluteLocation, sizeDictionaryFile: unixFile);
					Os.Close(unixFile); // no longer needed
				}

				if ( Preferences.Get(EnableBackup_KEY, EnableBackup_DEFAULT) && fileChange != FileChangeType.Deletion )
					updateBackupCache(absoluteLocation, skipExisting: fileChange == FileChangeType.None);

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

				if ( fileExclusions == null )
					initFileExclusions();

				if ( enableStatistics )
					sizeDictionaryFile= Os.Open(SizeDictionary_FILE, OsConstants.ORdwr | OsConstants.OCreat,  DefaultFilePermissions);

				FileExclusionLock.EnterReadLock();
				try {
					foreach ( FileChange fileChange in changes )
						if ( ! fileExclusions.Contains(fileChange.AbsoluteLocation) ) // changed file is not in exclude-list
						{
							if ( enableStatistics )
								updateStatistics(fileChange.AbsoluteLocation, sizeDictionaryFile);

							if ( enableBackup && fileChange.Type != FileChangeType.Deletion )
								updateBackupCache(fileChange.AbsoluteLocation, skipExisting: fileChange.Type == FileChangeType.None);
						}
				}
				finally {
					FileExclusionLock.ExitReadLock();
				}

				if ( sizeDictionaryFile != null )
					Os.Close(sizeDictionaryFile);  // release the handle

			}
		}

		private static void initFileExclusions()
		{
			var unixFile= Os.Open(ExclusionList_FILE, OsConstants.ORdonly | OsConstants.OCreat,  DefaultFilePermissions);

			if ( FileExclusionLock.TryEnterWriteLock(0) )  // only the first thread to arrive here reads the file into memory
				try {
					fileExclusions= unixFile.GetStrings(); // reads file into memory
				}
				finally {
					FileExclusionLock.ExitWriteLock();
					Os.Close(unixFile); // no longer needed
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
				if ( ! filePath.IsChildOf( BackupCache_FOLDER ) )
				{
					System.Diagnostics.Debug.Assert( filePath[0] == '/' ); // file paths given to updateBackupCache must be absolute
					string backupFilePath= BackupCache_FOLDER + filePath + ".zip",
					       backupFileVersion= System.DateTime.UtcNow.ToString(DefaultFileVersionFormat),
					       backupFileBackupPath= BackupCache_FOLDER + filePath + " [" + backupFileVersion + "].zip"; // an unusable backup archive will be renamed to this value
					backupFileVersion+= System.IO.Path.GetExtension(filePath);

					#region Add File To Zip Carrying Previous Versions

						#region Uses the `mkdir` system call for each directory the backup file needs
						{
							int nextSlashIndex= backupFilePath.IndexOf('/', 1); // finds the first non-root slash in the path
			
							while ( nextSlashIndex >= 0 )
							{
								string backupDir= backupFilePath.Substring(0, nextSlashIndex); 
								try {
									Os.Mkdir(backupDir, DefaultDirectoryPermissions );
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
							Os.Rename(backupFilePath, backupFileBackupPath); // backup the invalid backup
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
			long fileSizeDelta,
			     oldSizeOffset;

			var oldSizeStr= sizeDictionaryFile.ReadDictionaryEntry(filePath, out oldSizeOffset);
			long.TryParse(oldSizeStr, out fileSizeDelta); // reads the file's old size from the size dictionary as an integer

			#region Retrieve New File Size With System Call To Linux Kernel

				string newFileSizeStr= "0";
				try {
					long newFileSize= Os.Lstat(filePath).StSize;
					newFileSizeStr= newFileSize.ToString();
					fileSizeDelta= newFileSize - fileSizeDelta;
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
			bool added;

			lock ( DirectoryInclusionBarrier )
			{
				if ( directoryInclusions == null ) {
					unixFile= Os.Open(DirectoryInclusionList_FILE, OsConstants.ORdwr | OsConstants.OCreat,  DefaultFilePermissions);
					directoryInclusions= unixFile.GetStrings(); // reads the entire file such that the read/write offset should now be EOF
				}

				added= directoryInclusions.Add(absolutePath);
			}
			
			if ( added ) // directory wasn't in the collection
			{
				if ( unixFile == null )
					unixFile= Os.Open(DirectoryInclusionList_FILE, OsConstants.OWronly | OsConstants.OCreat | OsConstants.OAppend,  DefaultFilePermissions);
				
				absolutePath.WriteTo(unixFile); // appends the directory to the end of the file
				
				Task.Factory.StartNew( addDirectoryData, absolutePath ); // updates directory statistics while adding it to backup
			}

			if ( unixFile != null )
				Os.Close(unixFile); // file no longer needed
		}

		/// <summary>
		///  Updates the statistics of the given directory while adding it to backup.
		/// </summary>
		private static void addDirectoryData(object absolutePath)
		{
			string dirPath= (string)absolutePath;
			System.IO.Directory.CreateDirectory(dirPath); // creates the directory if it doesn't already exist
			OnFileChanges( System.IO.Directory.EnumerateFiles(dirPath, "*", SafeRecursiveMode).ToFileChanges() );
		}

		/// <summary>
		///  Removes a directory from the list of directories this app should keep track of.
		/// </summary>
		/// <param name="onError"> Currently unused as this method doesn't catch any errors. </param>
		public static void RemoveDirectory(string absolutePath, ErrorCallback onError= null)
		{
			FileDescriptor unixFile= null;

			lock ( DirectoryInclusionBarrier )
			{
				if ( directoryInclusions == null ) {
					unixFile= Os.Open(DirectoryInclusionList_FILE, OsConstants.ORdwr | OsConstants.OCreat,  DefaultFilePermissions);
					directoryInclusions= unixFile.GetStrings(); // reads the entire file such that the read/write offset should now be EOF
				}

				if ( directoryInclusions.Remove(absolutePath) ) // directory was in the collection
				{
					if ( unixFile == null )
						unixFile= Os.Open(DirectoryInclusionList_FILE, OsConstants.OWronly | OsConstants.OCreat,  DefaultFilePermissions);
					else Os.Lseek(unixFile, 0, OsConstants.SeekSet);
				
					directoryInclusions.WriteTo(unixFile); // rewrites the collection of directories to the list file
				}
			}

			if ( unixFile != null )
				Os.Close(unixFile); // file no longer needed

		}


		/// <summary>
		///  Retrieves the set of directories that this app should keep track of.
		/// </summary>
		public static HashSet<string> GetDirectories()
		{
			lock ( DirectoryInclusionBarrier )
			{
				if ( directoryInclusions == null ) // if the inclusions file hasn't already been read into memory
				{
					var unixFile= Os.Open(DirectoryInclusionList_FILE, OsConstants.ORdonly | OsConstants.OCreat,  DefaultFilePermissions);
					directoryInclusions= unixFile.GetStrings(); // reads the entire file as a set of strings
					Os.Close(unixFile); // file no longer needed
				}
				return new HashSet<string> ( directoryInclusions ); // create a copy for the outside world; don't allow direct access
			}
		}



		/// <summary>
		///  Adds a file to the list of files this app should ignore.
		/// </summary>
		/// <param name="onError"> Currently unused as this method doesn't catch any errors. </param>
		public static void IgnoreFile(string absolutePath, ErrorCallback onError= null)
		{
			FileDescriptor unixFile= null;
			bool newExclusion;

			FileExclusionLock.EnterWriteLock();
			try {

				if ( fileExclusions == null ) {
					unixFile= Os.Open(ExclusionList_FILE, OsConstants.ORdwr | OsConstants.OCreat,  DefaultFilePermissions);
					fileExclusions= unixFile.GetStrings(); // reads the entire file such that the read/write offset should now be EOF
				}

				newExclusion= fileExclusions.Add(absolutePath);
			}
			finally {
				FileExclusionLock.ExitWriteLock();
			}

			if ( newExclusion ) // file wasn't in the collection
			{
				if ( unixFile == null )
					unixFile= Os.Open(ExclusionList_FILE, OsConstants.OWronly | OsConstants.OCreat | OsConstants.OAppend,  DefaultFilePermissions);
				
				absolutePath.WriteTo(unixFile); // appends the filepath to the end of the file
			}
			
			if ( unixFile != null )
				Os.Close(unixFile); // file no longer needed

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