using Android.OS;
using Android.App;
using Android.Views;
using Android.Content;
using Android.Runtime;
using Android.Widget;
using Xamarin.Essentials;
using AndroidX.Fragment.App;
using System.Collections.Generic;

namespace StorageHistory
{
	using Collection;
	using Shared.UI;
	using static Shared.Configuration;

	public class ConfigurationActivity: LazyListFragment
	{
		public static readonly Dictionary<string, int> SpecialFolders=
			new Dictionary<string, int>
			{
				{ Environment.DirectoryDocuments,		Resource.String.directory_name__documents		},
				{ Environment.DirectoryDcim,			Resource.String.directory_name__camera			},
				{ Environment.DirectoryMusic,			Resource.String.directory_name__music			},
				{ Environment.DirectoryPictures,		Resource.String.directory_name__pictures		},
				{ Environment.DirectoryMovies,			Resource.String.directory_name__movies			},
				{ Environment.DirectoryAlarms,			Resource.String.directory_name__alarms			},
				{ Environment.DirectoryDownloads,		Resource.String.directory_name__downloads		},
				{ Environment.DirectoryNotifications,	Resource.String.directory_name__notifications	},
				{ Environment.DirectoryPodcasts,		Resource.String.directory_name__podcasts		},
				{ Environment.DirectoryRingtones,		Resource.String.directory_name__ringtones		}
			};

		public List<string> Directories;
		List<string> DirectoryLocations;
		HashSet<int> MonitoredIndices;


		/// <summary>
		///  Adds `activity_configuration.xml` to the main view, now or later depending on the configuration view's potential visibility.
		/// </summary>
		public override View OnCreateView(LayoutInflater inflater, ViewGroup parent, Bundle savedInstanceState)
			=> this.Inflate( ViewIndices.Configuration, inflater, Resource.Layout.configuration_activity, parent );


		/// <summary>
		///  Called when the configuration view and its children are initialized.
		/// </summary>
		public override void OnInflate(View view, bool immediate)
		{
			base.OnInflate(view, immediate);

			var monitoredDirectories= Synchronizer.GetDirectories();
			MonitoredIndices= new HashSet<int>( monitoredDirectories.Count );
			
			int maxL= SpecialFolders.Count + monitoredDirectories.Count + 1;
			Directories= new List<string>(maxL);
			DirectoryLocations= new List<string>(maxL);

			Directories.Add( Resources.GetString(Resource.String.shared_storage_name) );
			DirectoryLocations.Add( Environment.ExternalStorageDirectory.AbsolutePath );

			foreach ( var entry in SpecialFolders ) {
				Directories.Add( Resources.GetString(entry.Value) );
				DirectoryLocations.Add( Environment.GetExternalStoragePublicDirectory(entry.Key).AbsolutePath );
			}

			Directories.Add( Resources.GetString(Resource.String.app_directory_name) );
			DirectoryLocations.Add( Context.DataDir.AbsolutePath );

			// add user-defined folders to list
			foreach ( var dir in monitoredDirectories )
			{
				int i= DirectoryLocations.IndexOf(dir);
				if ( i < 0 ) {
					i= Directories.Count;
					Directories.Add(dir);
					DirectoryLocations.Add(dir);
				}
				MonitoredIndices.Add(i);
			}

			ListAdapter= new ArrayAdapter<string>(Context, Resource.Layout.configuration_item, Directories);

			foreach ( int i in MonitoredIndices )
				ListView.SetItemChecked(i, true);

			var browseBtn= view.FindViewById<Button>(Resource.Id.button_browse);
			if ( browseBtn != null )
				browseBtn.Click+= OnBrowseBtnClick;
		}


		public override void OnListItemClick(ListView listView, View itemView, int itemIndex, long itemId)
		{
			if ( MonitoredIndices.Add(itemIndex) )
				Synchronizer.AddDirectory( DirectoryLocations[itemIndex] );
			else {
				MonitoredIndices.Remove(itemIndex);
				Synchronizer.RemoveDirectory( DirectoryLocations[itemIndex] );
			}

			// update the file monitoring service
			Context.StartForegroundService( new Intent( Context, typeof(StorageObserver) ) );
		}


		const int BrowseRequestCode= 2;

		public void OnBrowseBtnClick(object sender, System.EventArgs e)
		{
			var intent= new Intent ( Intent.ActionOpenDocumentTree );
			intent.AddFlags( ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission );
			this.StartActivityForResult(intent, BrowseRequestCode);
		}

		public override void OnActivityResult(int requestCode, int resultCode, Intent data)
		{
			if ( requestCode == BrowseRequestCode && resultCode == (int)Result.Ok ) {
				var dirUri= data?.Data;
				if ( dirUri != null )
					Context.ContentResolver.TakePersistableUriPermission( dirUri, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission );
			}
		}
		

	}

}