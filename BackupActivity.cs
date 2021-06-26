using Android.OS;
using Android.Views;
using Android.Runtime;
using Android.Widget;
using AndroidX.Fragment.App;
using System.Collections.Generic;

using ListFragment= AndroidX.Fragment.App.ListFragment;
using FragmentTransaction= AndroidX.Fragment.App.FragmentTransaction;


namespace StorageHistory
{
	using Helpers;
	using static Helpers.Configuration;

	public class BackupActivity: ListFragment
	{
		public List<string> Items;
		public string CurrentDirectory;
		public string CurrentBackupFile;
		TextView header;
			
		public override View OnCreateView(LayoutInflater inflater, ViewGroup mainView, Bundle savedInstanceState)
		{
			if ( savedInstanceState != null )
			{
				CurrentDirectory= savedInstanceState.GetString("currentDirectory", "/");
				CurrentBackupFile= savedInstanceState.GetString("currentBackupFile", null);
			}
			else CurrentDirectory= "/";

			Items= new List<string>();
			UpdateState( CurrentDirectory );

			ListAdapter= new ArrayAdapter<string>(Context, Resource.Layout.backup_item, Items);

			// activity_backup.xml is added as a fragment
			return inflater.Inflate(Resource.Layout.activity_backup, mainView, false);
		}		

		/// <summary>
		///  Called when the backup view and its children are initialized.
		/// </summary>
		public override void OnViewCreated(View view, Bundle savedInstanceState)
		{
			base.OnViewCreated(view, savedInstanceState);
			header= view.FindViewById<TextView>( Resource.Id.backup_header );
			header.Text= CurrentDirectory;
		}

		/// <summary>
		///  Updates the backup view for the given path.
		/// </summary>
		public void UpdateState(string itemPath)
		{
			var file= new Java.IO.File( BackupCache_FOLDER + itemPath );
			if ( file.IsDirectory )
			{
				if ( itemPath.EndsWith("/..") ) // handles the directory "Up" action
					itemPath= itemPath.Substring( 0, itemPath.LastIndexOf('/', itemPath.Length-4).Or(1) );

				CurrentDirectory= itemPath;
				Items.Clear();
				if ( CurrentDirectory.Length > 1 )
					Items.Add("..");
				Items.AddRange( file.List() ); // adds each file to the list of items

				var adapter= ListAdapter as ArrayAdapter<string>;
				if ( adapter != null ) {
					adapter.Clear();
					adapter.AddAll(Items);
					adapter.NotifyDataSetChanged();
				}

				if ( header != null )
					header.Text= itemPath;
			}
			else {
				new BackupDialog().Show( ChildFragmentManager, "dialog" );
			}
		}

		public override void OnListItemClick(ListView listView, View itemView, int itemIndex, long itemId)
		{
			UpdateState( System.IO.Path.Combine( CurrentDirectory, Items[ itemIndex ] ) );
		}

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