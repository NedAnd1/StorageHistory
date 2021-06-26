using System.IO;
using Xamarin.Essentials;

namespace StorageHistory.Helpers
{

	/// <summary>
	///  Extension methods for interpreting and manipulating path strings. 
	/// </summary>
	static class PathExtensions
	{
	
		/// <returns>
		///  whether a file exists at the given path (<see langword="false"/> if the path points to a directory).
		/// </returns>
		public static bool IsFile(this string path) => File.Exists(path);


		/// <summary>
		///  Checks if the first absolute path is a subdirectory or file of the second absolute path.
		/// </summary>
		public static bool IsChildOf(this string childPath, string parentPath)
		{
			if ( childPath.Length <= parentPath.Length ) // childPath must be longer
				return false;
			
			if ( childPath[ parentPath.Length ] == '/' ) // slashes should match up
			{
				if ( childPath.Length == parentPath.Length + 1 )
					return false; // prevents /X/ from being a child of /X
			}
			else if ( parentPath[ parentPath.Length - 1 ] != '/' )
				return false; // prevents /XY from being a child of /X

			return childPath.StartsWith( parentPath );
		}

		/// <summary>
		///  Checks if the first absolute path is a subdirectory or file of the second absolute path, assumming neither ends in a slash.
		/// </summary>
		public static bool isChildOf(this string childPath, string parentPath)
		{
			if ( childPath.Length <= parentPath.Length ) // childPath must be longer
				return false;

			if ( childPath[ parentPath.Length ] != '/' ) // slashes must match up
				return false;

			return childPath.StartsWith( parentPath );
		}

	}

}