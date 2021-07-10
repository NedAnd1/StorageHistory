using System;
using System.Diagnostics;
using System.Collections.Generic;
using Java.IO;
using Android.App;
using Android.Content;
using Android.OS;
using Xamarin.Essentials;
using System.Threading;

namespace StorageHistory.Collection
{
	using Shared.Logic;
	using static Shared.Configuration;
	using Debug = System.Diagnostics.Debug;

	[Service]
	public class StorageObserverService: Service
	{
		const FileObserverEvents eventsFilter=
			FileObserverEvents.Create | FileObserverEvents.Modify | FileObserverEvents.Delete
			| FileObserverEvents.MovedFrom | FileObserverEvents.MovedTo | FileObserverEvents.MoveSelf;

		/// <summary>
		///  The average size of a monitored directory by directory count.
		/// </summary>
		private static int avgDirectorySize= Preferences.Get(AvgDirectorySize_KEY, AvgDirectorySize_DEFAULT);

		private static List<ObserverItem> @base;
		private static HashSet<string> directories;
		private static Thread firstUpdater;
		private static Thread lastUpdater;

		public override void OnCreate() {
			base.OnCreate();
			showNotification();
		}

		public override void OnDestroy() {
			Synchronizer.OnExit();
			base.OnDestroy();
		}

		public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
		{
			new Thread(updateObservers).Start();
			return StartCommandResult.Sticky;
		}

		/// <summary>
		///  Updates the list of directories being observed and their observers.
		/// </summary>
		private static void updateObservers()
		{
			var newDirectories= Synchronizer.GetDirectories();
			var previousDirectories= Interlocked.Exchange(ref directories, newDirectories); // gets the previous set of directories while replacing it w/ our new set

			var previousUpdater= Interlocked.Exchange(ref lastUpdater, Thread.CurrentThread); // set `lastUpdater` to the current thread while retrieving it's previous value
			try {
				var directories= newDirectories;  // in case `newDirectories` becomes only the newly added directories rather than the complete set 
				
				if ( previousUpdater != null )
				{
					#region Determine The Best Way To Synchronize Multiple Updates

						Interlocked.CompareExchange(ref firstUpdater, previousUpdater, null); // `firstUpdater` is only set to previousUpdater if it was null
						if ( previousDirectories.IsSubsetOf(newDirectories) )  // directory additions can run after one another
						{
							if ( newDirectories.Count == previousDirectories.Count )
								return ;  // don't do anything if directories and newDirectories are the same

							// Wait until the previous update has completed
							previousUpdater.Join();  // so we can add the new directories in a thread-safe manner
							if ( Interlocked.CompareExchange(ref firstUpdater, Thread.CurrentThread, null) != null )  // set `firstUpdater` to the current thread if it was equal to null
								return ;  // if the queue of updates was interrupted by a directory being removed

							newDirectories= new HashSet<string> ( newDirectories );  // create a copy of the set...
							newDirectories.ExceptWith( previousDirectories );         // to store only the newly added directories
						}
						else {
							// Start over if directories were removed
							previousUpdater= Interlocked.Exchange(ref firstUpdater, Thread.CurrentThread); // ensures that only the last update to arrive here continues
							if ( previousUpdater != null )
								previousUpdater.Abort(); // prior updates are cancelled here
						}

					#endregion
				}
				else if ( previousDirectories != null )
				{
					// Find any differences
					previousDirectories.SymmetricExceptWith(newDirectories);
					if ( previousDirectories.Count == 0 )
						return ;  // no difference between new set of directories and old set
				}

				// estimate the number of observer items we'll need
				int sizeEstimate= directories.Count;
				sizeEstimate*= avgDirectorySize; // make space for sub-directories

				// Prepare the list of observer items we'll use to monitor each directory and subdirectory.
				if ( @base == null )
					@base= new List<ObserverItem>( sizeEstimate );
				else {
					if ( directories == newDirectories ) // if we're starting fresh
						@base.Clear();
					if ( @base.Capacity < sizeEstimate )
						@base.Capacity= sizeEstimate;
				}

				// Create and add an observer item for each directory and subdirectory.
				foreach ( string path in newDirectories )
					monitorDirectory(path);
			
				if ( directories.Count > 0 )
				{
					// update the average directory size
					avgDirectorySize= ( avgDirectorySize + @base.Count / directories.Count ) / 2;
					Preferences.Set(AvgDirectorySize_KEY, avgDirectorySize);
				}

			}
			finally {
				Interlocked.CompareExchange(ref firstUpdater, null, Thread.CurrentThread); // set `firstUpdater` to null if it's equal to the current thread
				Interlocked.CompareExchange(ref lastUpdater, null, Thread.CurrentThread); // set `lastUpdater` to null if it's equal to the current thread
			}
		}

		/// <summary>
		///  Recursively monitors subdirectories so the service works correctly.
		/// </summary>
		private static void monitorDirectory(string path, bool isChild= false)
		{
			try {
				if ( isChild )
				{
					if ( ! System.IO.Directory.Exists(path) )
						return ; // if the child isn't a directory we have access to
				}
				else System.IO.Directory.CreateDirectory(path); // creates the directory if it doesn't already exist
				@base.Add( new ObserverItem(path) );
				foreach ( string subPath in System.IO.Directory.EnumerateDirectories(path, "*", SafeRecursiveMode) )
					@base.Add( new ObserverItem(subPath) );  // makes sure all sub-directories are monitored as well
			}
			catch ( Exception e ) {
				Debug.WriteLine(e.Message);
				Debugger.Break();
			}
		}

		public override IBinder OnBind(Intent intent) => null;

		/// <summary>
		///  Indicates to the user that this service is running.
		/// </summary>
		private void showNotification()
		{
			( GetSystemService(NotificationService) as NotificationManager )
				?.CreateNotificationChannel(
					new NotificationChannel("storistory.services.low", "Storage Services", NotificationImportance.None)
				);

			var notification= new Notification.Builder(this, "storistory.services.low")
							.SetContentTitle( Resources.GetString(Resource.String.app_name) )
							.SetContentText( Resources.GetString(Resource.String.notification_message) )
							.SetContentIntent( PendingIntent.GetActivity(this, 1, new Intent(this, typeof(MainActivity)), PendingIntentFlags.Immutable) )
							.SetSmallIcon( Resource.Drawable.backup_icon_24 )
							.SetOngoing( true )
							.Build();

			StartForeground(1, notification);
		}

		/// <summary>
		///  Propagates file changes in the given directory to the <see cref="Synchronizer"/>.
		/// </summary>
		public class ObserverItem : FileObserver
		{
			private string basePath;

			public ObserverItem(string path)
				: base( new File(path).CanonicalPath, eventsFilter ) // FileObserver requires the canonical or non-symbolic path to the directory 
			{
				basePath= path + '/';
				base.StartWatching();
			}

			~ObserverItem() => base.StopWatching();

			public override void OnEvent(FileObserverEvents e, string path)
			{
				if ( string.IsNullOrEmpty(path) || path[0] != '/' )
					path= basePath + path;  // turn relative paths into absolute paths
				else if ( ! path.StartsWith(basePath) )
				{
					Debug.WriteLine($"Warning: Observer {e} event for `{path}` wasn't in `{basePath}`.");
					path= basePath.Concat(  path.AsSpan().Slice( path.LastIndexOf('/') + 1 )  );
				}

				switch ( e )
				{
					case FileObserverEvents.MovedTo:
					case FileObserverEvents.Create:
						if ( path.IsFile() )
							Synchronizer.OnFileChange(path, FileChangeType.Creation);
						else monitorDirectory( path, isChild: true ); // recursively monitors the new sub-directory
						break;
					case FileObserverEvents.Modify:
						Synchronizer.OnFileChange(path, FileChangeType.Modification);
						break;
					case FileObserverEvents.MovedFrom:
					case FileObserverEvents.Delete:
						Synchronizer.OnFileChange(path, FileChangeType.Deletion);
						break;
					case FileObserverEvents.MoveSelf:
						foreach ( string parent in directories )
							if ( basePath.IsChildOf(parent) )
								return ;  // no need to update observers
						updateObservers();
						break;
					// ToDo: better move support?
				}

			}
		}

	}


	/// <summary>
	///  Receives device startup, storage, and shutdown notifications.
	/// </summary>
	public class IntentListener: BroadcastReceiver
	{
		public override void OnReceive(Context context, Intent intent)
		{
			switch ( intent.Action )
			{
				case Intent.ActionBootCompleted:
				case Intent.ActionMediaMounted:
					if ( Preferences.Get(EnableBackup_KEY, EnableBackup_DEFAULT) || Preferences.Get(EnableStatistics_KEY, EnableStatistics_DEFAULT) )
						context.StartForegroundService( new Intent( context, typeof(StorageObserverService) ) );  // starts or updates the service
					break;
				case Intent.ActionShutdown:
					Synchronizer.OnExit();
					break;
				#pragma warning disable CS0618
				case Intent.ActionDeviceStorageLow: // not actually using this obsolete enumeration member
				#pragma warning restore CS0618
					break;
			}
		}
	}

}