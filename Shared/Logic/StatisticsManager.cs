using System;
using Android.Systems;
using Xamarin.Essentials;
using System.Collections.Generic;
using System.Globalization;

namespace StorageHistory.Shared.Logic
{
	using Analysis;
	using Collection;
	using static Configuration;

	/// <summary>
	///  High-level class to store and retrieve directory statistics.
	/// </summary>
	static class StatisticsManager
	{
		private static DateTime nextSnapshotStartTime;
		private static DynamicSnapshot latestSnapshot;
		private static List<Snapshot> snapshotsCache; // potentially large object which should only exist in memory when the app is retrieving snapshots for analysis

		public static void AddDirectory(string location, long sizeDelta)
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

		public static Timeline RetrieveTimeline(string basePath= null, DateTime startTime= default, bool refreshCache= false)
		{
			if ( snapshotsCache == null || refreshCache )
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

			using var snapshotsFile= new UnicodeFileStream(StatisticsDatabase_FILE, OsConstants.ORdonly | OsConstants.OCreat, DefaultFilePermissions);
			while ( true )
			{
				Characters newString; // for temporary data that we don't need to keep as strings on the heap
				var currSnapshot= new Snapshot();

				newString= snapshotsFile.ReadCharacters();
				if ( newString.IsNull || ! long.TryParse(newString, out currSnapshot.sizeDelta ) )
					break;

				newString= snapshotsFile.ReadCharacters();
				if ( newString.IsNull || ! uint.TryParse(newString, out currSnapshot.changeCount ) )
					break;

				newString= snapshotsFile.ReadCharacters();
				if ( newString.IsNull || ! DateTime.TryParseExact(newString, UniversalDateTimeFormat, null, DateTimeStyles.None, out currSnapshot.averageTime ) )
					break;

				int directoryCount;
				newString= snapshotsFile.ReadCharacters();
				if ( newString.IsNull || ! int.TryParse(newString, out directoryCount) )
					break;

				var absolutePaths= stackalloc ReadOnlySpan<char> [ directoryCount ]; // this temporary array also avoids the heap
				currSnapshot.children= new HashSet<Snapshot.Directory> ( directoryCount );			
				for ( int currIndex= 0; currIndex < directoryCount; ++currIndex )
				{
					uint parentIndex;
					Snapshot.Directory currDirectory;

					newString= snapshotsFile.ReadCharacters();
					if ( newString.IsNull || ! uint.TryParse(newString, out parentIndex) )
						break;

					var relativeLocation= snapshotsFile.ReadCharacters();
					currDirectory.parentLocation= null;
					if ( relativeLocation == "/" )
						currDirectory.absoluteLocation= string.Empty;
					else if ( parentIndex == 0 )
						currDirectory.absoluteLocation= relativeLocation.ToString();
					else {
						currDirectory.parentLocation= absolutePaths[parentIndex-1].AsString();
						currDirectory.absoluteLocation= System.IO.Path.Join(absolutePaths[parentIndex-1], relativeLocation);
					}
					absolutePaths[ currIndex ]= currDirectory.absoluteLocation.AsSpan();

					newString= snapshotsFile.ReadCharacters();
					if ( newString.IsNull || ! long.TryParse(newString, out currDirectory.sizeDelta) )
						break;

					currSnapshot.children.Add(currDirectory);
				}

				if ( snapshotsFile.ReadLengthOfString() != 0 ) // valid snapshot MUST end with empty string signaling its end
					break;

				snapshotsCache.Add(currSnapshot);
			}

		}

		public static void TrimMemory()
			=> snapshotsCache= null;

	}
}