﻿using System;
using System.Diagnostics;
using System.Collections.Generic;
using Java.IO;
using Android.App;
using Android.Content;
using Android.OS;
using Xamarin.Essentials;

namespace StorageHistory
{
	using Helpers;
	using static Helpers.Configuration;
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

		public override void OnCreate() {
			base.OnCreate();
			showNotification();
			updateObservers();
		}

		public override void OnDestroy() {
			Synchronizer.OnExit();
			base.OnDestroy();
		}

		public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId) {
			updateObservers();
			return StartCommandResult.Sticky;
		}

		/// <summary>
		///  Updates the list of directories being observed and their observers.
		/// </summary>
		private static void updateObservers()
		{
			var previousDirs= directories;
			directories= Synchronizer.GetDirectories();
			if ( previousDirs != null ) {
				previousDirs.SymmetricExceptWith(directories);
				if ( previousDirs.Count == 0 )
					return ;  // no difference between new set of directories and old set
			}

			// estimate the number of observer items we'll need
			int sizeEstimate= directories.Count;
			sizeEstimate*= avgDirectorySize; // make space for sub-directories

			if ( @base == null )
				@base= new List<ObserverItem>( sizeEstimate );
			else {
				@base.Clear();
				if ( @base.Capacity < sizeEstimate )
					@base.Capacity= sizeEstimate;
			}

			foreach ( string path in directories )
				monitorDirectory(path);
			
			if ( directories.Count > 0 )
			{
				// update the average directory size
				avgDirectorySize= ( avgDirectorySize + @base.Count / directories.Count ) / 2;
				Preferences.Set(AvgDirectorySize_KEY, avgDirectorySize);
			}
		}

		private static void monitorDirectory(string path)
		{
			try {
				System.IO.Directory.CreateDirectory(path); // creates the directory if it doesn't already exist
				@base.Add( new ObserverItem(path) );
				foreach ( string subPath in System.IO.Directory.EnumerateDirectories(path, "*.*", SafeRecursiveMode) )
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
			public ObserverItem(string path)
				: base( new File(path).CanonicalPath, eventsFilter ) // FileObserver requires the canonical or non-symbolic path to the directory 
				=> base.StartWatching();

			~ObserverItem() => base.StopWatching();

			public override void OnEvent(FileObserverEvents e, string path)
			{
				switch ( e )
				{
					case FileObserverEvents.MovedTo:
					case FileObserverEvents.Create:
						if ( path.IsFile() )
							Synchronizer.OnFileChange(path, FileChangeType.Creation);
						else monitorDirectory(path); // recursively monitors the new sub-directory
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
							if ( path.IsChildOf(parent) )
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
	public class IntentListener: BroadcastReceiver {
		public override void OnReceive(Context context, Intent intent)
		{
			switch ( intent.Action )
			{
				case Intent.ActionBootCompleted:
					if ( Preferences.Get(EnableBackup_KEY, EnableBackup_DEFAULT) || Preferences.Get(EnableStatistics_KEY, EnableStatistics_DEFAULT) )
						context.StartForegroundService( new Intent( context, typeof(StorageObserverService) ) );
					break;
				case Intent.ActionMediaMounted:
					break;
				case Intent.ActionShutdown:
					Synchronizer.OnExit();
					break;
				case Intent.ActionDeviceStorageLow:
					break;
			}
		}
		
	}
}