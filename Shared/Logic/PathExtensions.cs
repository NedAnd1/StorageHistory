using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Android.OS;
using Android.Content;
using Environment= Android.OS.Environment;

namespace StorageHistory.Shared.Logic
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
		///  Expands the given directory until a file or multiple subdirectories are reached.
		/// </summary>
		/// <returns>
		///  The relative path to the top-most child of consequence.
		/// </returns>
		public static string Expand(this DirectoryInfo parent)
		{
			DirectoryInfo singleChild= null;
			foreach ( var directoryOrFile in parent.EnumerateFileSystemInfos("*", Configuration.DefaultSearchOptions) )
				if (  singleChild != null  ||  ( singleChild= directoryOrFile as DirectoryInfo ) is null   )
					return parent.Name; // returns if the loop encounters its second directory or its first file		
			if ( singleChild is null )
				return parent.Name;  // returns if the loop never happened i.e. the directory was empty

			var currentPath= new StringBuilder(parent.Name).Append('/').Append(singleChild.Name);
			currentPath.@Expand(singleChild);
			return currentPath.ToString();
		}

		/// <summary>
		///  Recursively follows the singular children of the given directory, appending them to the current path.
		/// </summary>
		private static void @Expand(this StringBuilder currentPath, DirectoryInfo parent)
		{
			DirectoryInfo singleChild= null;
			foreach ( var directoryOrFile in parent.EnumerateFileSystemInfos("*", Configuration.DefaultSearchOptions) )
				if (  singleChild != null  ||  ( singleChild= directoryOrFile as DirectoryInfo ) is null   )
					return ;  // returns if the loop encounters its second directory or its first file
			if ( singleChild is null )
				return ;  // returns if the directory was empty
			currentPath.Append('/').Append(singleChild.Name).@Expand(singleChild);
		}
	
		
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
		
		/// <summary>
		///  Checks if the first absolute path is the same as the second absolute path.
		/// </summary>
		public static bool PathEquals(this string @this, string otherPath)
		{
			ReadOnlySpan<char> pathA= @this.AsSpan(),
			                   pathB= otherPath.AsSpan();

			uint pathOrder= (uint)( pathA.Length - pathB.Length + 1 );

			if ( pathOrder > 2 )
				return false;  // |difference| must be < or = to 1

			int minLength= pathB.Length;
			if ( pathOrder is 0 ) // A < B
			{
				minLength= pathA.Length;
				if ( pathB.FastGet( minLength ) != '/' )
					return false;
			}
			else if ( pathOrder is 2 ) // A > B
			{
				if ( pathA.FastGet( minLength ) != '/' )
					return false;
			}

			return pathA.FastSlice( 0, minLength ).SequenceEqual( pathB.FastSlice( 0, minLength ) );
		}

		/// <summary>
		///  Converts an absolute path to a user-facing path that recognizes special directories. 
		/// </summary>
		public static string ToUserPath(this string absolutePath)
		{
			if ( absolutePath.StartsWith(sharedStorageDir) )
			{
				int sharedDirLength= sharedStorageDir.Length;
				if ( absolutePath.Length == sharedDirLength )  // if absolutePath == sharedStorageDir
					return sharedStorageName;                   // return the user alias for shared storage
				else if ( absolutePath[ sharedDirLength ] == '/' )  // if the path is a child of shared storage
				{
					// find a direct child of shared storage that matches the given path
					int dirKeyStart= sharedDirLength + 1,             
						dirKeyEnd= absolutePath.IndexOf('/', dirKeyStart);
					if ( dirKeyEnd < 0 )
						dirKeyEnd= absolutePath.Length;
					string dirKey= absolutePath.Substring( dirKeyStart ,  dirKeyEnd - dirKeyStart );
					if ( sharedNames.TryGetValue(dirKey, out string dirName) )  // concat the path's end to the matching alias
						return dirName.Concat( absolutePath.FastSlice(dirKeyEnd) ); // minimize the number of string copies

					// look for an indirect child of shared storage that matches the given path
					foreach ( var alias in sharedAliases )
					{
						int keyLength= alias.Key.Length;
						if ( absolutePath.StartsWith(alias.Key) )
							if ( absolutePath.Length == keyLength )
								return alias.Value;
							else if ( absolutePath[ keyLength ] == '/' )
								return alias.Value.Concat( absolutePath.FastSlice(keyLength) );
					}

					return sharedStorageName.Concat( absolutePath.FastSlice(sharedDirLength) ); // replace sharedStorageDir w/ its alias
				}
			}

			// find a non-shared alias that matches the given path
			foreach ( var alias in otherAliases )
			{
				int keyLength= alias.Key.Length;
				if ( absolutePath.StartsWith(alias.Key) )
					if ( absolutePath.Length == keyLength )
						return alias.Value;
					else if ( absolutePath[ keyLength ] == '/' )
						return alias.Value.Concat( absolutePath.FastSlice(keyLength) );
			}

			return absolutePath;
		}


		/// <summary>
		///  Converts an absolute path to a user-facing path that recognizes special directories. 
		/// </summary>
		public static string ToUserPath(this string absolutePath, string basePath)
		{
			if ( basePath != null && absolutePath.isChildOf(basePath) )
			{
				string alias= basePath.ToUserPath();
				if ( alias != basePath )
					return alias.Concat( absolutePath.FastSlice(basePath.Length) );
			}
			return absolutePath.ToUserPath();
		}


		/// <summary>
		///  Converts the real file name of a zip archive to the name of the original file it's backing up.
		/// </summary>
		public static string ToUserFilename(this string realFilename, bool isFile= true, bool parentIsRoot= false)
		{
			if (  realFilename.EndsWith(".zip")  &&  isFile  )
				return realFilename.Substring(0, realFilename.Length-4);
			else if ( parentIsRoot )
			{
				string path= '/' + realFilename;
				if (  path  !=  ( path= path.ToUserPath() )  )  // converts an expanded directory path into a user-facing one
					return path;
			}
			return realFilename;
		}

		private static readonly Dictionary<string, int> specialFolders=
			new Dictionary<string, int>
			{
				{ Environment.DirectoryDocuments,		Resource.String.diralias__documents			},
				{ Environment.DirectoryDcim,			Resource.String.diralias__camera			},
				{ Environment.DirectoryMusic,			Resource.String.diralias__music				},
				{ Environment.DirectoryPictures,		Resource.String.diralias__pictures			},
				{ Environment.DirectoryMovies,			Resource.String.diralias__movies			},
				{ Environment.DirectoryAlarms,			Resource.String.diralias__alarms			},
				{ Environment.DirectoryDownloads,		Resource.String.diralias__downloads			},
				{ Environment.DirectoryNotifications,	Resource.String.diralias__notifications		},
				{ Environment.DirectoryPodcasts,		Resource.String.diralias__podcasts			},
				{ Environment.DirectoryRingtones,		Resource.String.diralias__ringtones			}
			};

		private static Dictionary<string, string> sharedNames= null;
		private static Dictionary<string, string> sharedAliases= null;
		private static Dictionary<string, string> otherAliases= null;


		private static string sharedStorageDir= null;
		private static string sharedStorageName= null;

		/// <summary>
		///  <see cref="ToUserPath"/> needs this method to be called before it works.
		/// </summary>
		public static void InitializeUserPaths(Context context)
		{
			if ( sharedStorageDir == null )
			{
				var resources= context.Resources;
				sharedStorageDir= Environment.ExternalStorageDirectory.AbsolutePath;
				sharedStorageName= resources.GetString(Resource.String.shared_storage_name);
				sharedNames= new Dictionary<string, string> ();
				sharedAliases= new Dictionary<string, string> ();
				otherAliases= new Dictionary<string, string> ();
				foreach ( var entry in specialFolders )
					addUserPath(
						Environment.GetExternalStoragePublicDirectory(entry.Key).AbsolutePath,
						resources.GetString(entry.Value)
					);

				// add an alias for the app's internal directory
				addUserPath(
					context.DataDir.AbsolutePath,
					resources.GetString(Resource.String.diralias__internal)
				);
			}
		}

		/// <summary>
		///  Adds the given alias to its appropriate dictionary.
		/// </summary>
		private static void addUserPath(string path, string alias)
		{
			if ( path.isChildOf(sharedStorageDir) )
			{
				if ( path.IndexOf('/', sharedStorageDir.Length+1) < 0 )
					sharedNames[ path.Substring(sharedStorageDir.Length+1) ]= alias;
				else sharedAliases[ path ]= alias;		
			}
			else otherAliases[ path ]= alias;
		}

	}

}