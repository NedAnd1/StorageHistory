using System.Collections;
using System.Collections.Generic;

namespace StorageHistory.Collection
{


	public enum FileChangeType
	{

		/// <summary>
		///  When the event is a result of configuration changes.
		/// </summary>
		None,
		 
		/// <summary>
		///   When a file moves within/across monitored directories.
		/// </summary>
		Move,

		/// <summary>
		///  When a file is actually created or is moved from a unmonitored directory to a monitored one.
		/// </summary>
		Creation,
		
		/// <summary>
		///  When a file changes but doesn't move.
		/// </summary>
		Modification,

		/// <summary>
		///  When a file is actually deleted or is moved from a monitored directory to an unmonitored one. 
		/// </summary>
		Deletion

	}


	/// <summary>
	///  The basis of storage history collection; used for initiating updates by <see cref="StorageObserverService"/> and <see cref="Synchronizer"/>.
	/// </summary>
	public struct FileChange
	{
		public string AbsoluteLocation;
		public FileChangeType Type;
		public static implicit operator FileChange(string str) => new FileChange { AbsoluteLocation= str };
	}


	public static class FileChangeExtensions
	{

		/// <summary>
		///  Converts an enumerable of strings into an enumerable of FileChanges with changeType = None
		/// </summary>
		public static IEnumerable<FileChange> ToFileChanges(this IEnumerable<string> source) => new StringToFileChangeEnumerable(source);

		private struct StringToFileChangeEnumerable: IEnumerable<FileChange>
		{
			private IEnumerable<string> source;
			public StringToFileChangeEnumerable(IEnumerable<string> source) => this.source= source;
			public IEnumerator<FileChange> GetEnumerator() => new StringToFileChangeEnumerator( source.GetEnumerator() );
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}

		private class StringToFileChangeEnumerator: IEnumerator<FileChange>
		{
			private IEnumerator<string> source;
			public FileChange Current => source.Current;
			object IEnumerator.Current => source.Current;
			public StringToFileChangeEnumerator(IEnumerator<string> source) => this.source= source;
			public void Dispose() => source.Dispose();
			public bool MoveNext() => source.MoveNext();
			public void Reset() => source.Reset();
		}

	}


}