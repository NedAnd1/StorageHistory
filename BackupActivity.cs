using Android.OS;
using Android.Views;
using Android.Widget;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xamarin.Essentials;
using System.Runtime.CompilerServices;

namespace StorageHistory
{
	using Shared.UI;
	using Shared.Logic;
	using static Shared.Configuration;
	using static Shared.UI.Extensions;

	public class BackupActivity: LazyListFragment
	{
		public List<string> RealFileNames;
		public string[] UserFileNames;
		public string CurrentDirectory;
		public string CurrentBackupFile;
		public string HeaderText;
		public int IndexOfFirstFile;
		List<string> TempList;
		TextView header;
			
		public override View OnCreateView(LayoutInflater inflater, ViewGroup parent, Bundle savedInstanceState)
		{
			if ( savedInstanceState != null )
			{
				CurrentDirectory= savedInstanceState.GetString("currentDirectory", "/");
				CurrentBackupFile= savedInstanceState.GetString("currentBackupFile", null);
			}
			else CurrentDirectory= "/";

			// adds `activity_backup.xml` to the main view, now or later depending on the backup view's potential visibility
			return this.Inflate(ViewIndices.Backup, inflater, Resource.Layout.backup_activity, parent);
		}

		/// <summary>
		///  Called when the backup view and its children are initialized.
		/// </summary>
		public override void OnInflate(View view, bool immediate)
		{
			base.OnInflate(view, immediate);
			header= view.FindViewById<TextView>( Resource.Id.backup_header );

			TempList= new List<string>();
			RealFileNames= new List<string>();
			ListAdapter= new DirectoryAdapter(Context, Resource.Layout.backup_item);

			if ( immediate )
			{
				UpdateState( CurrentDirectory );
				UpdateFileView();
			}
			else InvokeTaskOnReady( UpdateState, CurrentDirectory ); // asynchronously updates the backup view when the app is no longer loading
		}

		/// <summary>
		///  Updates the backup view for the given path.
		/// </summary>
		[MethodImpl(MethodImplOptions.Synchronized)]
		public void UpdateState(string itemPath)
		{
			var dir= new DirectoryInfo ( BackupCache_FOLDER + itemPath );
			if ( dir.Exists )
			{
				if ( itemPath.EndsWith("/..") ) // handles the directory "Up" action
					if ( HeaderText == null || HeaderText.IndexOf('/') > 0 )
						itemPath= itemPath.Substring( 0, itemPath.LastIndexOf('/', itemPath.Length-4).Or(1) );
					else dir= new DirectoryInfo ( BackupCache_FOLDER + ( itemPath= "/" ) );  // revert from the expanded dir back to the root of the backup folder

				bool atRoot= true;
				RealFileNames.Clear();
				if ( itemPath.Length > 1 )
				{
					atRoot= false;
					RealFileNames.Add("..");  // show the directory for going up
				}
				
				int indexOfFirstFile= AddChildren(dir, RealFileNames, TempList);  // adds each file to the list of items

				var fileNames= new string [ RealFileNames.Count ];
				for ( int i= 0; i < RealFileNames.Count; ++i )				                                // removes the internal file extension of backup archives
					fileNames[i]= RealFileNames[i].ToUserFilename( isFile: i >= indexOfFirstFile, atRoot );  // and transforms expanded dir paths into user-facing ones

				UserFileNames= fileNames;
				CurrentDirectory= itemPath;
				HeaderText= itemPath.ToUserPath();
				IndexOfFirstFile= indexOfFirstFile;
				MainThread.BeginInvokeOnMainThread( UpdateDirectoryView );
			}
			else {
				CurrentBackupFile= itemPath;
				MainThread.BeginInvokeOnMainThread( UpdateFileView );
			}
		}

		/// <summary>
		///  Adds child directories and files (sorted respectively) to the provided list.
		/// </summary>
		/// <returns>
		///  The number of directories i.e. the position of the first file in the list.
		/// </returns>
		private static int AddChildren(DirectoryInfo parent, List<string> directoryNames, List<string> fileNames)
		{
			foreach ( var child in parent.EnumerateFileSystemInfos("*", DefaultSearchOptions) )
				if ( child is DirectoryInfo directory )
					directoryNames.Add( directory.Expand() );  // expands linearly nested directories
				else fileNames.Add( child.Name );

			int indexOfFirstFile= directoryNames.Count;
			directoryNames.Sort();  // sort directories and files separately
			fileNames.Sort();
			directoryNames.AddRange(fileNames);  // directories should always come first
			fileNames.Clear();
			return indexOfFirstFile;
		}
		
		public void UpdateDirectoryView()
		{
			if ( ListAdapter is DirectoryAdapter adapter )
			{
				adapter.IndexOfFirstFile= IndexOfFirstFile;
				adapter.Items= UserFileNames;
			}

			if ( header != null )
				header.Text= HeaderText;
		}

		public void UpdateFileView()
		{
			var itemPath= CurrentBackupFile;
			if ( itemPath != null )
			{
				new BackupDialog().Show( ChildFragmentManager, "dialog" );
			}
		}

		private void UpdateState(object itemPath)
			=> UpdateState( (string)itemPath );

		public void UpdateStateAsync(string itemPath)
			=> Task.Factory.StartNew( UpdateState, itemPath );
			
		public override void OnListItemClick(ListView listView, View itemView, int itemIndex, long itemId)
			=> UpdateStateAsync( Path.Combine( CurrentDirectory, RealFileNames[ itemIndex ] ) );

		/// <summary>
		///  Saves the current directory and file seen in backup before the activity is destroyed.
		/// </summary>
		public override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			outState.PutString("currentDirectory", CurrentDirectory);
			outState.PutString("currentBackupFile", CurrentBackupFile);
		}

	}
}