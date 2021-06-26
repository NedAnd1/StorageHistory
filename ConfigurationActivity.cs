using Android.OS;
using Android.App;
using Android.Views;
using Android.Content;
using Android.Runtime;
using Android.Widget;
using Xamarin.Essentials;
using AndroidX.Fragment.App;
using System.Collections.Generic;

using ListFragment= AndroidX.Fragment.App.ListFragment;

namespace StorageHistory
{
	using static Helpers.Configuration;

	public class ConfigurationActivity: ListFragment
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


		public override View OnCreateView(LayoutInflater inflater, ViewGroup mainView, Bundle savedInstanceState)
		{
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

			// activity_configuration.xml is added as a fragment
			return inflater.Inflate(Resource.Layout.activity_configuration, mainView, false);
		}		

		/// <summary>
		///  Called when the configuration view and its children are initialized.
		/// </summary>
		public override void OnViewCreated(View view, Bundle savedInstanceState)
		{
			base.OnViewCreated(view, savedInstanceState);

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
			Context.StartForegroundService( new Intent( Context, typeof(StorageObserverService) ) );
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