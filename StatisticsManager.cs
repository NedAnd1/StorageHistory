using System;
using Android.Systems; // interfaces with the device's low-level Linux kernel
using Xamarin.Essentials;
using System.Collections.Generic;
using System.Globalization;

namespace StorageHistory
{
	using Helpers;
	using static Helpers.Configuration;

	/// <summary>
	///  High-level class to store and retrieve directory statistics.
	/// </summary>
	static class StatisticsManager
	{
		private static DateTime nextSnapshotStartTime;
		private static DynamicSnapshot latestSnapshot;
		private static List<Snapshot> snapshotsCache; // potentially large object which should only exist in memory when the app is retrieving snapshots for analysis

		public static void AddDirectory(string location, int sizeDelta)
		{
			if ( DateTime.UtcNow >= nextSnapshotStartTime  )
			{
				SaveSnapshot();
				nextSnapshotStartTime= DateTime.UtcNow.AddSeconds( Preferences.Get(MinSnapshotDuration_KEY, MinSnapshotDuration_DEFAULT) );
			}
			
			latestSnapshot.AddDirectory(location, sizeDelta);
		}

		public static void SaveSnapshot()
		{
			if ( latestSnapshot ) // saves the latest snapshot to local storage and starts a new snapshot
			{
				latestSnapshot.WriteTo(StatisticsDatabase_FILE);
				latestSnapshot= new DynamicSnapshot();
			}
		}

		public static Snapshot RetrieveSnapshot(DateTime startTime, DateTime endTime)
		{
			if ( snapshotsCache == null )
				initializeCache();

			var matches= new List<Snapshot> ( snapshotsCache.Count );
			foreach ( var snapshot in snapshotsCache )
				if ( startTime <= snapshot.averageTime && endTime >= snapshot.averageTime )
					matches.Add(snapshot);

			return new Snapshot(matches); // returns a profile of the matches
		}

		public static Timeline RetrieveTimeline(string basePath= null, DateTime startTime= default)
		{
			if ( snapshotsCache == null )
				initializeCache();

			if ( snapshotsCache.Count > 0 )
				if ( basePath != null )
					return new Timeline ( snapshotsCache, basePath, startTime );
				else return new Timeline ( snapshotsCache, startTime );
			else return new Timeline();
		}

		private static unsafe void initializeCache()
		{
			snapshotsCache= new List<Snapshot>();
			var snapshotsFile=
				Android.Systems.Os.Open(StatisticsDatabase_FILE, OsConstants.ORdonly | OsConstants.OCreat, DefaultFilePermissions);
				
			while ( true )
			{
				string newString;
				var currSnapshot= new Snapshot();

				newString= snapshotsFile.ReadString();
				if ( newString == null || ! int.TryParse(newString, out currSnapshot.sizeDelta ) )
					break;

				newString= snapshotsFile.ReadString();
				if ( newString == null || ! uint.TryParse(newString, out currSnapshot.changeCount ) )
					break;

				newString= snapshotsFile.ReadString();
				if ( newString == null || ! DateTime.TryParseExact(newString, UniversalDateTimeFormat, null, DateTimeStyles.None, out currSnapshot.averageTime ) )
					break;

				int directoryCount;
				newString= snapshotsFile.ReadString();
				if ( newString == null || ! int.TryParse(newString, out directoryCount) )
					break;

				var absolutePaths= stackalloc ReadOnlySpan<char> [ directoryCount ];
				currSnapshot.children= new HashSet<Snapshot.Directory> ( directoryCount );			
				for ( int currIndex= 0; currIndex < directoryCount; ++currIndex )
				{
					uint parentIndex;
					Snapshot.Directory currDirectory;

					newString= snapshotsFile.ReadString();
					if ( newString == null || ! uint.TryParse(newString, out parentIndex) )
						break;

					var relativeLocation= snapshotsFile.ReadString();
					currDirectory.parentLocation= null;
					if ( relativeLocation == "/" )
						currDirectory.absoluteLocation= string.Empty;
					else if ( parentIndex == 0 )
						currDirectory.absoluteLocation= relativeLocation;
					else {
						currDirectory.parentLocation= absolutePaths[parentIndex-1].AsString();
						currDirectory.absoluteLocation= System.IO.Path.Join(absolutePaths[parentIndex-1], relativeLocation);
					}
					absolutePaths[ currIndex ]= currDirectory.absoluteLocation.AsSpan();

					newString= snapshotsFile.ReadString();
					if ( newString == null || ! int.TryParse(newString, out currDirectory.sizeDelta) )
						break;

					currSnapshot.children.Add(currDirectory);
				}

				if ( snapshotsFile.ReadString() != string.Empty ) // valid snapshot MUST end with empty string signaling its end
					break;

				snapshotsCache.Add(currSnapshot);
			}

			Os.Close(snapshotsFile); // no longer need the file
		}

	}
}