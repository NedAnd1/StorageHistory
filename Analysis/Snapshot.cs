using System;
using System.Collections.Generic;

namespace StorageHistory.Analysis
{
	using Shared.Logic;

	/// <summary>
	///  Used by <see cref="StatisticsManager"/> for reading and analyzing directory statistics.
	/// </summary>
	struct Snapshot
	{
		public long sizeDelta;
		public uint changeCount;
		public DateTime averageTime;
		public HashSet<Directory> children;

		/// <summary>
		///  Combines multiple snapshots into a single snapshot that consumes less memory
		/// </summary>
		public Snapshot(IEnumerable<Snapshot> snapshots)
		{
			sizeDelta= 0;
			changeCount= 0;
			averageTime= default;
			children= null;

			foreach ( Snapshot snapshot in snapshots ) {
				changeCount+= snapshot.changeCount;
				averageTime= averageTime.AddTicks( ( snapshot.averageTime.Ticks - averageTime.Ticks ) * snapshot.changeCount / changeCount );
			}

			foreach ( Snapshot snapshot in snapshots )
			{
				if ( children == null )
					children= new HashSet<Directory>( snapshot.children );
				else foreach ( Directory directoryB in snapshot.children )
					{
						Directory directoryA;
						if ( children.TryGetValue(directoryB, out directoryA) )
							directoryA.sizeDelta+= directoryB.sizeDelta;
						else children.Add(directoryB);
					}
			}

		}

		/// <summary>
		///  Adds two snapshots into one snapshot representing a single averaged point in time
		/// </summary>
		public static Snapshot operator +(Snapshot inputA, Snapshot inputB)
		{
			uint changeCountSum= inputA.changeCount + inputB.changeCount;
			long averageTickShift= ( inputB.averageTime.Ticks - inputA.averageTime.Ticks ) * inputB.changeCount / changeCountSum;

			var output= new Snapshot()
						{
							sizeDelta= inputA.sizeDelta + inputB.sizeDelta,
							changeCount= changeCountSum,
							averageTime= inputA.averageTime.AddTicks(averageTickShift),
							children= new HashSet<Directory>( inputA.children )
						};

			foreach ( Directory directoryB in inputB.children ) {
				Directory directoryA;
				if ( output.children.TryGetValue(directoryB, out directoryA) )
					directoryA.sizeDelta+= directoryB.sizeDelta;
				else output.children.Add(directoryB);
			}

			return output;
		}


		public struct Directory: IEquatable<Directory>
		{

			public long sizeDelta;
			public string parentLocation;
			public string absoluteLocation;

			public override int GetHashCode()
			{
				return absoluteLocation.GetHashCode();
			}

			public bool Equals(Directory other)
			{
				return string.Equals( this.absoluteLocation, other.absoluteLocation );
			}

		}

	}

}