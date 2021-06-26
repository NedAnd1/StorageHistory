using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace StorageHistory.Helpers
{
	struct Timeline
	{
		public DateTime startTime;
		public DateTime endTime;
		public HashSet<Directory> directories;

		public Timeline(IReadOnlyList<Snapshot> snapshots)
		{
			int snapshotIndex= snapshots.Count - 1;
			var currSnapshot= snapshots[ snapshotIndex ];
			startTime= snapshots[0].averageTime;
			endTime= currSnapshot.averageTime;
			directories= new HashSet<Directory> ( currSnapshot.children.Count );

			var timelineParents= new HashSet<string> ( currSnapshot.children.Count );

			#region Add Latest Snapshot To The Timeline

				foreach ( var directory in currSnapshot.children )
					if ( directory.parentLocation == null )
					{
						var parentDirectory= new Directory();
						parentDirectory.absoluteLocation= directory.absoluteLocation;
						parentDirectory.sizes= new (DateTime, int) [ snapshots.Count ];
						parentDirectory.sizes[ snapshotIndex ]= ( currSnapshot.averageTime, directory.sizeDelta );
						directories.Add(parentDirectory);
						timelineParents.Add(directory.absoluteLocation);
					}

			#endregion

			#region Add All Other Snapshots To The Timeline

				Directory unusedDirectory= new Directory();
				var parentsToSkip= new HashSet<string> ( timelineParents );
				while ( snapshotIndex > 0 )
				{
					currSnapshot= snapshots[ --snapshotIndex ];

					foreach ( var snapshotChild in currSnapshot.children )
						if ( parentsToSkip.Contains( snapshotChild.parentLocation ) ) // skip the children of parents that already have the info we need
							parentsToSkip.Add( snapshotChild.absoluteLocation );
						else {
							Directory existingDirectory; // a directory already in the set
							unusedDirectory.absoluteLocation= snapshotChild.absoluteLocation;
							if ( directories.TryGetValue( unusedDirectory, out existingDirectory ) )
								existingDirectory.sizes[ snapshotIndex ]= ( currSnapshot.averageTime, snapshotChild.sizeDelta );
							else {
								foreach ( var timelineDirectory in directories )
									if ( snapshotChild.absoluteLocation.isChildOf( timelineDirectory.absoluteLocation ) )
									{
										timelineDirectory.sizes[ snapshotIndex ].Item1= currSnapshot.averageTime;
										timelineDirectory.sizes[ snapshotIndex ].Item2+= snapshotChild.sizeDelta;
										parentsToSkip.Add(snapshotChild.absoluteLocation);
										goto ContinueParentAdditions;
									}
									else if ( timelineDirectory.absoluteLocation.isChildOf( snapshotChild.absoluteLocation ) )
										goto ContinueParentAdditions;

								unusedDirectory.sizes= new (DateTime, int) [ snapshotIndex + 1 ];
								unusedDirectory.sizes[ snapshotIndex ]= ( currSnapshot.averageTime, snapshotChild.sizeDelta );
								directories.Add(unusedDirectory);
								parentsToSkip.Add(snapshotChild.absoluteLocation);
								unusedDirectory= new Directory();

								ContinueParentAdditions: ; // the snapshot child has been added to our timeline
							}
						}

				}

			#endregion

			this.normalizeDirectories();

		}

		public Timeline(IReadOnlyList<Snapshot> snapshots, string basePath)
		{
			int snapshotIndex= snapshots.Count - 1;
			var currSnapshot= snapshots[ snapshotIndex ];
			startTime= snapshots[0].averageTime;
			endTime= currSnapshot.averageTime;
			directories= new HashSet<Directory> ( currSnapshot.children.Count );

			var timelineParents= new HashSet<string> ( currSnapshot.children.Count );

			if ( basePath != null )
				#region Normalize Base Path
				{
					basePath= System.IO.Path.GetFullPath(basePath);
					if ( basePath[ basePath.Length - 1 ] == '/' )
						basePath= basePath.Substring(0, basePath.Length-1);
					if ( basePath == "" )
						basePath= null;
				}
				#endregion

			#region Add Latest Snapshot To The Timeline

				foreach ( var directory in currSnapshot.children )
					if ( directory.parentLocation == basePath )
					{
						var parentDirectory= new Directory();
						parentDirectory.absoluteLocation= directory.absoluteLocation;
						parentDirectory.sizes= new (DateTime, int) [ snapshots.Count ];
						parentDirectory.sizes[ snapshotIndex ]= ( currSnapshot.averageTime, directory.sizeDelta );
						directories.Add(parentDirectory);
						timelineParents.Add(directory.absoluteLocation);
					}

			#endregion

			if ( basePath == null )
				basePath= "";

			#region Add All Other Snapshots To The Timeline

				Directory unusedDirectory= new Directory();
				var parentsToSkip= new HashSet<string> ( timelineParents );
				while ( snapshotIndex > 0 )
				{
					currSnapshot= snapshots[ --snapshotIndex ];

					foreach ( var snapshotChild in currSnapshot.children )
						if ( parentsToSkip.Contains( snapshotChild.parentLocation ) ) // skip the children of parents that already have the info we need
							parentsToSkip.Add( snapshotChild.absoluteLocation );
						else {
							Directory existingDirectory; // a directory already in the set
							unusedDirectory.absoluteLocation= snapshotChild.absoluteLocation;
							if ( directories.TryGetValue( unusedDirectory, out existingDirectory ) )
								existingDirectory.sizes[ snapshotIndex ]= ( currSnapshot.averageTime, snapshotChild.sizeDelta );
							else if ( snapshotChild.absoluteLocation.isChildOf(basePath) )
							{
								foreach ( var timelineDirectory in directories )
									if ( snapshotChild.absoluteLocation.isChildOf( timelineDirectory.absoluteLocation ) )
									{
										timelineDirectory.sizes[ snapshotIndex ].Item1= currSnapshot.averageTime;
										timelineDirectory.sizes[ snapshotIndex ].Item2+= snapshotChild.sizeDelta;
										parentsToSkip.Add(snapshotChild.absoluteLocation);
										goto ContinueParentAdditions;
									}
									else if ( timelineDirectory.absoluteLocation.isChildOf( snapshotChild.absoluteLocation ) )
										goto ContinueParentAdditions;

								unusedDirectory.sizes= new (DateTime, int) [ snapshotIndex + 1 ];
								unusedDirectory.sizes[ snapshotIndex ]= ( currSnapshot.averageTime, snapshotChild.sizeDelta );
								directories.Add(unusedDirectory);
								parentsToSkip.Add(snapshotChild.absoluteLocation);
								unusedDirectory= new Directory();

								ContinueParentAdditions: ; // the snapshot child has been added to our timeline
							}
						}

				}

			#endregion

			this.normalizeDirectories();

		}

		/// <summary>
		///  Calculates directory properties while normalizing their sizes across time (from delta values to absolute ones).
		/// </summary>
		private void normalizeDirectories()
		{
			foreach ( var timelineDirectory in directories )
			{
				int j= 0,
					currentSize= 0;
				var sizeArray= timelineDirectory.sizes;
				timelineDirectory.minSize= int.MaxValue;
				for ( int i= 0; i < sizeArray.Length; ++i )
					if ( sizeArray[i].Item1 != default(DateTime) ) // skips sizes without the required timestamp
					{
						sizeArray[j].Item1= sizeArray[i].Item1;
						sizeArray[ j++ ].Item2= currentSize+= sizeArray[i].Item2; // sizes are now consecutive and absolute

						if ( currentSize < timelineDirectory.minSize )
							timelineDirectory.minSize= currentSize;

						if ( currentSize > timelineDirectory.maxSize )
							timelineDirectory.maxSize= currentSize;
					}	
					
				timelineDirectory.sizeCount= j;
				timelineDirectory.name= timelineDirectory.absoluteLocation.Substring( timelineDirectory.absoluteLocation.LastIndexOf('/') + 1 );
			}
		}

		public bool IsEmpty => directories == null || directories.Count == 0;

		public class Directory: IEquatable<Directory>, IComparable<Directory>
		{
			public int minSize;
			public int maxSize;
			public string name;
			public string absoluteLocation;
			public (DateTime,int)[] sizes;
			public int sizeCount;
			public float[] output;

			/// <summary>
			///  Generates the array of floats required for a draw to the canvas
			/// </summary>
			public void GenerateOutput(DateTime minTime, DateTime maxTime, int outputWidth, int outputHeight)
			{
				int sizeCount= 0;
				if ( minSize != maxSize )
					sizeCount= this.sizeCount; 

				if (  output == null  ||  ( sizeCount + 1 ) * 4 != output.Length  )
					output= new float [ ( sizeCount + 1 ) * 4 ];


				float xFactor= (float)outputWidth / ( maxTime.Ticks - minTime.Ticks ),
				      yFactor= (float)outputHeight / ( maxSize - minSize );

				output[0]= 0;
				output[1]= outputHeight * 0.5f;

				int j,
					i= 0;
				for ( j= 5;  j < output.Length; ++i, j+= 4 )
				{
					output[ j - 1 ]= ( sizes[i].Item1 - minTime ).Ticks * xFactor; // sets the x start-value
					output[ j - 0 ]= ( maxSize - sizes[i].Item2 ) * yFactor; // sets the y start-value
				}

				for ( j= 5;  j < output.Length; j+= 4 ) {  // copies the start-point of each line to the end-point of the previous line
					output[ j - 3 ]= output[ j - 1 ]; // sets the x end-value
					output[ j - 2 ]= output[ j - 0 ]; // sets the y end-value
				}
				
				output[ output.Length - 2 ]= outputWidth;
				output[ output.Length - 1 ]= outputHeight * 0.5f;

				if ( sizeCount > 0 )
				{
					output[ 1 ]= ( maxSize - sizes[0].Item2 ) * yFactor;
					output[ output.Length - 1 ]= ( maxSize - sizes[ sizeCount - 1 ].Item2 ) * yFactor;
				}
			}

			public override int GetHashCode() => absoluteLocation.GetHashCode();

			public bool Equals(Directory other) => string.Equals( this.absoluteLocation, other.absoluteLocation );

			public int CompareTo(Directory other) // used to sort this class
			{
				if ( other == null )
					return -1; // non-null values first

				if ( this.sizes != null && other.sizes != null )
				{
					int delta= ( this.sizes[this.sizeCount-1].Item2 - this.sizes[0].Item2 ) - ( other.sizes[other.sizeCount-1].Item2 - other.sizes[0].Item2 ) ;

					if ( delta != 0 )
						return -delta; // larger deltas first
				}

				return string.CompareOrdinal( this.absoluteLocation, other.absoluteLocation );
			}
		}

	}
}