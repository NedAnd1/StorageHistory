using Java.Lang;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Views;
using AndroidX.Fragment.App;
using AndroidX.AppCompat.App;
using AndroidX.ViewPager2.Adapter;
using AndroidX.ViewPager2.Widget;
using Android.Content;
using Google.Android.Material.BottomNavigation;

using Fragment= AndroidX.Fragment.App.Fragment;

namespace StorageHistory
{
	using Collection;
	using Shared.Logic;
	using static Shared.Configuration;

	public static class ViewIndices
	{
		public const int Backup= 0;
		public const int Analysis= 1;
		public const int Configuration= 2;
	}

	[Activity(Label= "@string/app_name", Theme= "@style/AppTheme", MainLauncher= true)]
	public class MainActivity : AppCompatActivity, BottomNavigationView.IOnNavigationItemSelectedListener
	{
		private ViewPager2 mainView;
		public ViewPager2 ViewPager => mainView;

		/// <summary>
		///  Called when the app is started or resumed.
		/// </summary>
		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.main);

			Xamarin.Essentials.Platform.Init(this, savedInstanceState);
			PathExtensions.InitializeUserPaths(this);

			mainView= FindViewById<ViewPager2>(Resource.Id.main_view);
			mainView.Adapter= new ViewManager(this);
			
			BottomNavigationView navigation= FindViewById<BottomNavigationView>(Resource.Id.navigation);
			mainView.RegisterOnPageChangeCallback( new SubViewNavigationHandler(navigation) );
			if ( savedInstanceState != null )
				mainView.SetCurrentItem( savedInstanceState.GetInt("currentPosition", ViewIndices.Analysis), smoothScroll: false );
			else mainView.SetCurrentItem( ViewIndices.Analysis, smoothScroll: false );
			mainView.OffscreenPageLimit= 2;

			navigation.SetOnNavigationItemSelectedListener(this);

			bool requestPermissions= false;
			foreach ( var permisionId in DefaultPermissionsRequired )
				if ( CheckSelfPermission(permisionId) != Android.Content.PM.Permission.Granted )
					requestPermissions= true; // only request permissions if we don't have what we need
			
			if ( requestPermissions )
				RequestPermissions(DefaultPermissionsRequired, 1);

			// Start the service that monitors file changes
			StartForegroundService( new Intent(this, typeof(StorageObserver) ) );

		}

		/// <summary>
		///  Called when the user responds to a request for permissions.
		/// </summary>
		public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Android.Content.PM.Permission[] grantResults)
		{
			Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

			base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
		}

		/// <summary>
		///  Called when the user selects a navigation button to switch to its respective view.
		/// </summary>
		public bool OnNavigationItemSelected(IMenuItem item)
		{
			switch ( item.ItemId )
			{
				case Resource.Id.navigation_left:
					mainView.SetCurrentItem( ViewIndices.Backup, smoothScroll: true );
					return true;
				case Resource.Id.navigation_home:
					mainView.SetCurrentItem( ViewIndices.Analysis, smoothScroll: true );
					return true;
				case Resource.Id.navigation_right:
					mainView.SetCurrentItem( ViewIndices.Configuration, smoothScroll: true );
					return true;
			}
			return false;
		}

		protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			outState.PutInt("currentPosition", mainView.CurrentItem);  // saves the current tab position
		}

		protected override void OnDestroy()
		{
			Synchronizer.OnExit();
			base.OnDestroy();
		}

		/// <summary>
		///  Handles tab switches in the Main View.
		/// </summary>
		class SubViewNavigationHandler: ViewPager2.OnPageChangeCallback
		{
			private BottomNavigationView target;
			
			public SubViewNavigationHandler(BottomNavigationView bottomNavigationView)
			{
				target= bottomNavigationView;
			}

			public override void OnPageSelected(int index)
			{
				int menuItemId;
				switch ( index )
				{
					case ViewIndices.Backup:
						menuItemId= Resource.Id.navigation_left;
						break;
					case ViewIndices.Configuration:
						menuItemId= Resource.Id.navigation_right;
						break;
					default:
						menuItemId= Resource.Id.navigation_home;
						break;
				}
				target.SelectedItemId= menuItemId;
			}

		}

		/// <summary>
		///  Helps the Main View to initialize tabs (based on a given index).
		/// </summary>
		class ViewManager: FragmentStateAdapter
		{
			public ViewManager(FragmentActivity parent): base(parent) { }

			public override int ItemCount => 3;

			public override Fragment CreateFragment(int index)
			{
				switch ( index )
				{
					case ViewIndices.Backup:
						return new BackupActivity();
					case ViewIndices.Configuration:
						return new ConfigurationActivity();
					default:
						return new AnalysisActivity();
				}
			}

		}

	}
}

