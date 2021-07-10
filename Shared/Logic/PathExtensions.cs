using System;
using Java.IO;
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
		public static bool IsFile(this string path) => System.IO.File.Exists(path);


		/// <returns>
		///  whether a file exists as a child of the given parent (<see langword="false"/> if it points to a directory).
		/// </returns>
		public static bool HasFile(this File parent, string child) => new File(parent, child).IsFile;


		/// <summary>
		///  Expands the given child until a file or multiple subdirectories are reached.
		/// </summary>
		/// <returns>
		///  The relative path to the top-most child of consequence.
		/// </returns>
		public static string ExpandChild(this File parent, string childName)
		{
			var child= new File(parent, childName);
			if ( child.IsDirectory )
			{
				var subChildNames= child.List();
				if ( subChildNames.Length == 1 )
				{
					var subChild= new File(child, subChildNames[0]);
					if ( subChild.IsDirectory )
					{
						var currentPath= new StringBuilder(childName).Append('/').Append(subChildNames[0]);
						currentPath.@Expand(subChild);
						return currentPath.ToString();
					}
				}
			}
			return childName;
		}

		/// <summary>
		///  Recursively follows the singular children of the given directory, appending them to the current path.
		/// </summary>
		private static void @Expand(this StringBuilder currentPath, File parent)
		{
			var childNames= parent.List();
			if ( childNames.Length == 1 )
			{
				var child= new File(parent, childNames[0]);
				if ( child.IsDirectory )
					currentPath.Append('/').Append(childNames[0]).@Expand(child);
			}
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
						return dirName.Concat( absolutePath.AsSpan().Slice(dirKeyEnd) ); // minimize the number of string copies

					// look for an indirect child of shared storage that matches the given path
					foreach ( var alias in sharedAliases )
					{
						int keyLength= alias.Key.Length;
						if ( absolutePath.StartsWith(alias.Key) )
							if ( absolutePath.Length == keyLength )
								return alias.Value;
							else if ( absolutePath[ keyLength ] == '/' )
								return alias.Value.Concat( absolutePath.AsSpan().Slice(keyLength) );
					}

					return sharedStorageName.Concat( absolutePath.AsSpan().Slice(sharedDirLength) ); // replace sharedStorageDir w/ its alias
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
						return alias.Value.Concat( absolutePath.AsSpan().Slice(keyLength) );
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
					return alias.Concat( absolutePath.AsSpan().Slice(basePath.Length) );
			}
			return absolutePath.ToUserPath();
		}


		/// <summary>
		///  Converts the real file name of a zip archive to the name of the original file it's backing up.
		/// </summary>
		public static string ToUserFilename(this string realFilename, File parent= null)
		{
			if (  realFilename.EndsWith(".zip")  &&  ( parent == null || parent.HasFile(realFilename) )  )
				return realFilename.Substring(0, realFilename.Length-4);
			else if ( parent != null && parent.AbsolutePath == Configuration.BackupCache_FOLDER )
			{
				string path= '/' + realFilename;  // converts an expanded directory path into a user-facing one
				if (  path  !=  ( path= path.ToUserPath() )  )
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