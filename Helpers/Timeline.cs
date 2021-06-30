using System;
using System.Collections.Generic;

namespace StorageHistory.Helpers
{
	struct Timeline
	{
		public DateTime startTime;
		public DateTime endTime;
		public HashSet<Directory> directories;

		public Timeline(IReadOnlyList<Snapshot> snapshots, DateTime minTime= default)
		{
			int snapshotIndex= snapshots.Count - 1;
			var currSnapshot= snapshots[ snapshotIndex ];
			startTime= snapshots[0].averageTime;
			endTime= currSnapshot.averageTime;
			directories= new HashSet<Directory> ( currSnapshot.children.Count );

			var timelineParents= new HashSet<string> ( currSnapshot.children.Count );

			#region Add Latest Snapshot To The Timeline

				if ( currSnapshot.averageTime < minTime )
					return ;
				else foreach ( var directory in currSnapshot.children )
						if ( directory.parentLocation == null )
						{
							var parentDirectory= new Directory();
							parentDirectory.AbsoluteLocation= directory.absoluteLocation;
							parentDirectory.relativeSize= new (DateTime, long) [ snapshots.Count ];
							parentDirectory.relativeSize[ snapshotIndex ]= ( currSnapshot.averageTime, directory.sizeDelta );
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
					if ( currSnapshot.averageTime < minTime )
						break;

					foreach ( var snapshotChild in currSnapshot.children )
						if ( parentsToSkip.Contains( snapshotChild.parentLocation ) ) // skip the children of parents that already have the info we need
							parentsToSkip.Add( snapshotChild.absoluteLocation );
						else {
							Directory existingDirectory; // a directory already in the set
							unusedDirectory.AbsoluteLocation= snapshotChild.absoluteLocation;
							if ( directories.TryGetValue( unusedDirectory, out existingDirectory ) )
								existingDirectory.relativeSize[ snapshotIndex ]= ( currSnapshot.averageTime, snapshotChild.sizeDelta );
							else {
								foreach ( var timelineDirectory in directories )
									if ( snapshotChild.absoluteLocation.isChildOf( timelineDirectory.AbsoluteLocation ) )
									{
										timelineDirectory.relativeSize[ snapshotIndex ].Item1= currSnapshot.averageTime;
										timelineDirectory.relativeSize[ snapshotIndex ].Item2+= snapshotChild.sizeDelta;
										parentsToSkip.Add(snapshotChild.absoluteLocation);
										goto ContinueParentAdditions;
									}
									else if ( timelineDirectory.AbsoluteLocation.isChildOf( snapshotChild.absoluteLocation ) )
										goto ContinueParentAdditions;

								unusedDirectory.relativeSize= new (DateTime, long) [ snapshotIndex + 1 ];
								unusedDirectory.relativeSize[ snapshotIndex ]= ( currSnapshot.averageTime, snapshotChild.sizeDelta );
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

		public Timeline(IReadOnlyList<Snapshot> snapshots, string basePath, DateTime minTime= default)
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

				if ( currSnapshot.averageTime < minTime )
					return ;
				else foreach ( var directory in currSnapshot.children )
						if ( directory.parentLocation == basePath )
						{
							var parentDirectory= new Directory();
							parentDirectory.AbsoluteLocation= directory.absoluteLocation;
							parentDirectory.relativeSize= new (DateTime, long) [ snapshots.Count ];
							parentDirectory.relativeSize[ snapshotIndex ]= ( currSnapshot.averageTime, directory.sizeDelta );
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
					if ( currSnapshot.averageTime < minTime )
						break;

					foreach ( var snapshotChild in currSnapshot.children )
						if ( parentsToSkip.Contains( snapshotChild.parentLocation ) ) // skip the children of parents that already have the info we need
							parentsToSkip.Add( snapshotChild.absoluteLocation );
						else {
							Directory existingDirectory; // a directory already in the set
							unusedDirectory.AbsoluteLocation= snapshotChild.absoluteLocation;
							if ( directories.TryGetValue( unusedDirectory, out existingDirectory ) )
								existingDirectory.relativeSize[ snapshotIndex ]= ( currSnapshot.averageTime, snapshotChild.sizeDelta );
							else if ( snapshotChild.absoluteLocation.isChildOf(basePath) )
							{
								foreach ( var timelineDirectory in directories )
									if ( snapshotChild.absoluteLocation.isChildOf( timelineDirectory.AbsoluteLocation ) )
									{
										timelineDirectory.relativeSize[ snapshotIndex ].Item1= currSnapshot.averageTime;
										timelineDirectory.relativeSize[ snapshotIndex ].Item2+= snapshotChild.sizeDelta;
										parentsToSkip.Add(snapshotChild.absoluteLocation);
										goto ContinueParentAdditions;
									}
									else if ( timelineDirectory.AbsoluteLocation.isChildOf( snapshotChild.absoluteLocation ) )
										goto ContinueParentAdditions;

								unusedDirectory.relativeSize= new (DateTime, long) [ snapshotIndex + 1 ];
								unusedDirectory.relativeSize[ snapshotIndex ]= ( currSnapshot.averageTime, snapshotChild.sizeDelta );
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
		///  Calculates directory properties while accumulating their changes in size across time.
		/// </summary>
		private void normalizeDirectories()
		{
			foreach ( var timelineDirectory in directories )
			{
				int j= 0;
				long currentSize= 0;
				var sizeArray= timelineDirectory.relativeSize;
				for ( int i= 0; i < sizeArray.Length; ++i )
					if ( sizeArray[i].Item1 != default(DateTime) ) // skips sizes without the required timestamp
					{
						sizeArray[j].Item1= sizeArray[i].Item1;
						sizeArray[ j++ ].Item2= currentSize+= sizeArray[i].Item2; // sizes are now cumulative

						if ( currentSize > timelineDirectory.maxSizeDelta )
							timelineDirectory.maxSizeDelta= currentSize;
					}	
					
				timelineDirectory.sizeCount= j;
				// timelineDirectory.name= timelineDirectory.AbsoluteLocation.Substring( timelineDirectory.AbsoluteLocation.LastIndexOf('/') + 1 );
			}
		}

		public bool IsEmpty => directories == null || directories.Count == 0;

		public class Directory: IEquatable<Directory>, IComparable<Directory>
		{
			public string AbsoluteLocation;
			public float[] Output;
			internal (DateTime, long)[] relativeSize;
			internal int sizeCount;
			internal long maxSizeDelta;

			/// <summary>
			///  Generates the array of floats required for a draw to the canvas
			/// </summary>
			public void GenerateOutput(DateTime minTime, DateTime maxTime, int outputWidth, int outputHeight)
			{
				int sizeCount= 0;
				if ( maxSizeDelta != 0 )
					sizeCount= this.sizeCount;

				if (  Output == null  ||  ( sizeCount + 1 ) * 4 != Output.Length  )  // each line consists of 2 points (4 numbers)
					Output= new float [ ( sizeCount + 1 ) * 4 ];                      // 2 extra lines are drawn to ensure the given range // ( n-1 + 2 ) * 4

				Output[ 0 ]= 0; // sets the first x-value  // the composite line starts here
				Output[ 1 ]= outputHeight * 0.5f; // sets the default y-value

				if ( sizeCount > 0 )
				{
					Output[ 1 ]= outputHeight; // sets the first y-value

					float xFactor= (float)outputWidth / ( maxTime.Ticks - minTime.Ticks ),
					      yFactor= (float)outputHeight / maxSizeDelta;

					int i= 0;
					for ( int j= 5;  j < Output.Length; ++i, j+= 4 )
					{
						Output[ j - 1 ]= ( relativeSize[i].Item1 - minTime ).Ticks * xFactor; // sets the x start-value
						Output[ j - 0 ]= ( maxSizeDelta - relativeSize[i].Item2 ) * yFactor; // sets the y start-value
					}

					for ( int j= 5; j < Output.Length; j+= 4 )  // copies the start-point of each line to the end-point of the previous line
					{
						Output[ j - 3 ]= Output[ j - 1 ]; // sets the x end-value
						Output[ j - 2 ]= Output[ j - 0 ]; // sets the y end-value
					}

				}

				Output[ Output.Length - 2 ]= outputWidth; // sets the last x-value  // the composite line ends here
				Output[ Output.Length - 1 ]= Output[ Output.Length - 3 ]; // sets the last y-value
			}

			public long SizeDelta => relativeSize[ sizeCount - 1 ].Item2;  // directories in the timeline always have at least one size

			public override int GetHashCode() => AbsoluteLocation.GetHashCode();

			public bool Equals(Directory other) => string.Equals( this.AbsoluteLocation, other.AbsoluteLocation );

			public int CompareTo(Directory other) // used to sort this class
			{
				if ( other == null )
					return -1; // non-null values first

				if ( this.relativeSize != null && other.relativeSize != null )
				{
					long a= this.relativeSize[this.sizeCount-1].Item2,
					     b= other.relativeSize[other.sizeCount-1].Item2;
					if ( a > b )
						return -1; // larger deltas first
					else if ( a < b )
						return 1;
				}

				return string.CompareOrdinal( this.AbsoluteLocation, other.AbsoluteLocation );
			}
		}

	}
}