using System;
using Android.Systems; // interfaces with the device's low-level Linux kernel

namespace StorageHistory.Collection
{
	using Shared.Logic;
	using static Shared.Configuration;

	/// <summary>
	///  Used to record and store directory statistics.
	/// </summary>
	struct DynamicSnapshot
	{
		public long sizeDelta;
		public DateTime averageTime;
		public Directory firstChild;

		private uint totalChangeCount;


		public void AddDirectory(string location, long sizeDelta)
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
							firstChild= new Directory() { sizeDelta= sizeDelta, absoluteLocation= location, relativeLocation= location, nextNode= currNode };
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
						lastNode.nextNode= currNode= new Directory() { sizeDelta= sizeDelta, parent= lastParent, absoluteLocation= location, relativeLocation= location };
						if ( lastParent != null )
							currNode.relativeLocation= location.Substring(lastParent.absoluteLocation.Length+1);
						break;
					}
				}
			}
			
			this.sizeDelta+= sizeDelta;
			averageTime= averageTime.AddTicks( ( DateTime.UtcNow.Ticks - averageTime.Ticks ) / ++totalChangeCount );  // formula for an unweighted moving average
		}

		public void WriteTo(string filePath)
		{
			using var file= new UnicodeFileStream(filePath, OsConstants.OWronly | OsConstants.OCreat | OsConstants.OAppend, DefaultFilePermissions);

			sizeDelta.WriteTo(file);
			totalChangeCount.WriteTo(file);
			averageTime.ToString(UniversalDateTimeFormat).WriteTo(file); // date must always be last property before directory count

			int directoryCount= 0;
			for ( Directory node= firstChild; node != null; node= node.nextNode )
				node.index= ++directoryCount; // set each node's index

			directoryCount.WriteTo(file);

			// write each directory property sequentially
			for ( Directory node= firstChild; node != null; node= node.nextNode )
			{
				if ( node.parent != null )
					node.parent.index.WriteTo(file);
				else "0".WriteTo(file);
				
				if ( node.relativeLocation.Length > 0 )
					node.relativeLocation.WriteTo(file);
				else "/".WriteTo(file); // replace empty string

				node.sizeDelta.WriteTo(file);
			}

			'\0'.WriteTo(file); // makes sure the snapshot is terminated
		}



		public static implicit operator bool(DynamicSnapshot @this)
			=>  @this.sizeDelta != 0  &&  @this.firstChild != null ;


		public class Directory {
			public int index; // 1-based index of node in linked list
			public long sizeDelta;
			public Directory parent;
			public Directory nextNode;
			public string absoluteLocation; // used for comparing directories while creating the snapshot
			public string relativeLocation; // the path to be stored after finishing the snapshot
		}

	}
}