﻿using System;
using Android.Systems; // interfaces with the device's low-level Linux kernel

namespace StorageHistory.Helpers
{
	using static Configuration;

	/// <summary>
	///  Used to record and store directory statistics.
	/// </summary>
	struct DynamicSnapshot
	{
		public int sizeDelta;
		public DateTime averageTime;
		public Directory firstChild;

		private uint totalChangeCount;
		// private uint duplicateChangeCount;


		public void AddDirectory(string location, int sizeDelta)
		{
			if ( location[ location.Length - 1 ] == '/' )
				location= location.Substring( 0, location.Length - 1 );

			if ( firstChild == null )
				firstChild= new Directory() { sizeDelta= sizeDelta, absoluteLocation= location, relativeLocation= location };
			else {
				Directory lastNode= null,
					      currNode= firstChild,
					      lastParent= null;
				while ( true )
				{
					int locationCompare= string.CompareOrdinal(location, currNode.absoluteLocation);
					if ( locationCompare <= 0 )
					{
						if ( locationCompare == 0 )
							currNode.sizeDelta+= sizeDelta;
						else if ( lastNode == null )
							firstChild= currNode= new Directory() { sizeDelta= sizeDelta, absoluteLocation= location, relativeLocation= location, nextNode= currNode };
						else {
							lastNode.nextNode= currNode= new Directory() {
																			sizeDelta= sizeDelta,
																			parent= lastParent, nextNode= currNode, 
																			absoluteLocation= location, relativeLocation= location };
							if ( lastParent != null )
								currNode.relativeLocation= location.Substring(lastParent.absoluteLocation.Length+1);
						}
						break;
					}

					if ( location.isChildOf( currNode.absoluteLocation ) ) // uses the isChildOf static method in our `RuntimeExtensions.cs` helper
					{
						lastParent= currNode; // update the last parent of the directory being added
						currNode.sizeDelta+= sizeDelta;
					}

					lastNode= currNode;
					currNode= currNode.nextNode; // next node in linked-list
					if ( currNode == null )
					{
						lastNode.nextNode= new Directory() { sizeDelta= sizeDelta, parent= lastParent, absoluteLocation= location, relativeLocation= location };
						if ( lastParent != null )
							currNode.relativeLocation= location.Substring(lastParent.absoluteLocation.Length+1);
						break;
					}
				}
			}
			
			this.sizeDelta+= sizeDelta;
			averageTime= averageTime.AddSeconds(  (double)( DateTime.UtcNow.Ticks - averageTime.Ticks ) / ( TimeSpan.TicksPerSecond * ++totalChangeCount ) );
		}


		public void WriteTo(string filePath)
		{
			var unixFile= Android.Systems.Os.Open(filePath, OsConstants.OWronly | OsConstants.OCreat | OsConstants.OAppend, DefaultFilePermissions);

			sizeDelta.ToString().WriteTo(unixFile);
			totalChangeCount.ToString().WriteTo(unixFile);
			averageTime.ToString(UniversalDateTimeFormat).WriteTo(unixFile); // date must always be last property before directory count

			int directoryCount= 0;
			for ( Directory node= firstChild; node != null; node= node.nextNode )
				node.index= ++directoryCount; // set each node's index

			directoryCount.ToString().WriteTo(unixFile);

			// write each directory property sequentially
			for ( Directory node= firstChild; node != null; node= node.nextNode ) {

				if ( node.parent != null )
					node.parent.index.ToString().WriteTo(unixFile);
				else "0".WriteTo(unixFile);
				
				System.Diagnostics.Debug.Assert( node.relativeLocation.Length > 0 ); // `relativeLocation` must NOT be empty
				node.relativeLocation.WriteTo(unixFile);

				node.sizeDelta.ToString().WriteTo(unixFile);

			}

			'\0'.WriteTo(unixFile); // makes sure the snapshot is terminated

			Android.Systems.Os.Close(unixFile); // release the handle
		}



		public static implicit operator bool(DynamicSnapshot @this) {
			return  @this.sizeDelta != 0  &&  @this.firstChild != null ;
		}


		public class Directory {
			public int index; // 1-based index of node in linked list
			public int sizeDelta;
			// public uint childCount;
			public Directory parent;
			public Directory nextNode;
			public string absoluteLocation; // used for comparing directories while creating the snapshot
			public string relativeLocation; // the path to be stored after finishing the snapshot
		}

	}
}