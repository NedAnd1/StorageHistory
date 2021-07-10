using System;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.SwipeRefreshLayout.Widget;
using Xamarin.Essentials;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace StorageHistory
{
	using Analysis;
	using Shared.UI;
	using Shared.Logic;
	using static Shared.Configuration;
	using static Shared.UI.Extensions;

	public class AnalysisActivity: LazyListFragment, SwipeRefreshLayout.IOnRefreshListener
	{
		const int HoursPerDay= 24;

		/// <summary>
		///  How long a day is in <see cref="AnalysisActivity"/>'s default unit of hours.
		/// </summary>
		const int Day= HoursPerDay;

		/// <summary>
		///  The culture-invariant list of options for the time selector (in <see cref="AnalysisActivity"/>'s default unit of hours).
		/// </summary>
		static readonly int[] DurationOptions=
			new int[] {
				  1 * Day,
				  7 * Day,
				 30 * Day,
				365 * Day,
				  0 * Day, // All time (infinite duration)
			};

		public string CurrentDirectory;
		public TimeSpan CurrentDuration;
		public string HeaderText;
		TextView header;
		Spinner timeSelector;
		SwipeRefreshLayout mainRefresher;
		SwipeRefreshLayout emptyRefresher;

			
		/// <summary>
		///  Adds `activity_analysis.xml` to the main view, now or later depending on the analysis view's potential visibility.
		/// </summary>
		public override View OnCreateView(LayoutInflater inflater, ViewGroup parent, Bundle savedInstanceState)
			=> this.Inflate(ViewIndices.Analysis, inflater, Resource.Layout.activity_analysis, parent);

		public override void OnViewCreated(View view, Bundle savedInstanceState)
		{
			if ( savedInstanceState != null )
				CurrentDirectory= savedInstanceState.GetString("currentDirectory");

			base.OnViewCreated(view, savedInstanceState);
		}

		/// <summary>
		///  Called when the analysis view and its children are truly initialized.
		/// </summary>
		public override void OnInflate(View view, bool immediate)
		{
			base.OnInflate(view, immediate);

			int lastNumberOfHours= Preferences.Get(AnalysisDuration_KEY, AnalysisDuration_DEFAULT);
			CurrentDuration= TimeSpan.FromTicks( TimeSpan.TicksPerHour * lastNumberOfHours );

			header= view.FindViewById( Resource.Id.analysis_header ) as TextView;
			if ( header != null )
				header.Click+= OnHeaderClick;

			timeSelector= view.FindViewById( Resource.Id.analysis_time_selector ) as Spinner;
			if ( timeSelector != null )
			{
				var durationStrings= this.retrieveDurationStrings(lastNumberOfHours, out int lastSelectionIndex); // the user-facing counterpart to DurationOptions
				var selectionAdapter= new ArrayAdapter<string> ( Context, Android.Resource.Layout.SimpleSpinnerItem, durationStrings );
				    selectionAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
				timeSelector.Adapter= selectionAdapter;
				timeSelector.SetSelection( lastSelectionIndex, animate: false );
				timeSelector.ItemSelected+= OnTimeSelection;
			}

			mainRefresher= view.FindViewById( Resource.Id.analysis_refresher ) as SwipeRefreshLayout;
			if ( mainRefresher != null )
				mainRefresher.SetOnRefreshListener(this);

			emptyRefresher= view.FindViewById( Android.Resource.Id.Empty ) as SwipeRefreshLayout;
			if ( emptyRefresher != null )
				emptyRefresher.SetOnRefreshListener(this);

			ListAdapter= new TimelineAdapter ( Context );

			if ( immediate )
				UpdateState( CurrentDirectory );
			else InvokeTaskOnReady( () => UpdateState( CurrentDirectory ) ); // asynchronously updates the analysis view when the app is no longer loading
		}

		/// <summary>
		///  Retrieves the culture-specific strings of each duration option as an array with the same order of <see cref="DurationOptions"/>
		/// </summary>
		private string[] retrieveDurationStrings(int numberOfHours, out int selectionIndex)
		{
			var map= new Dictionary<int, string> ();
			int[] pastDays= Resources.GetIntArray(Resource.Array.past_day_string_keys);
			string[] pastDayStrings= Resources.GetStringArray(Resource.Array.past_day_string_values);
			for ( int i= 0; i < pastDayStrings.Length; ++i )
				if (  ! string.IsNullOrWhiteSpace( pastDayStrings[i] )  )
					map.Add( pastDays[i] * HoursPerDay ,  pastDayStrings[i] );

			selectionIndex= 0;
			var durationStrings= new string [ DurationOptions.Length ];
			for ( int i= 0; i < durationStrings.Length; ++i )
			{
				string durationString;
				int duration= DurationOptions[i];
				if ( ! map.TryGetValue( duration, out durationString ) )
				{
					if ( duration < Day )
						durationString= Resources.GetQuantityString( Resource.Plurals.past_hours, duration, duration );
					else durationString= Resources.GetQuantityString( Resource.Plurals.past_days, duration/Day, duration/Day );
				}

				durationStrings[ i ]= durationString;
				if ( duration == numberOfHours )
					selectionIndex= i;
			}

			return durationStrings;
		}
		
		public void OnHeaderClick(object o, EventArgs e)
		{
			Loading= true;
			if ( CurrentDirectory != null )
				if ( header == null || header.Text.IndexOf('/', 1) > 0 )  // if the current directory is neither special nor a direct child of the root
					UpdateStateAsync(  CurrentDirectory.Substring( 0, CurrentDirectory.LastIndexOf('/') )  ); // go up to the current directory's parent
				else UpdateStateAsync(); // go up to the root
		}

		public void OnTimeSelection(object o, AdapterView.ItemSelectedEventArgs itemArgs)
		{
			Loading= true;
			int newDuration= DurationOptions[ itemArgs.Position ];
			CurrentDuration= TimeSpan.FromTicks( newDuration * TimeSpan.TicksPerHour );
			Preferences.Set(AnalysisDuration_KEY, newDuration);
			UpdateStateAsync( CurrentDirectory );
		}

		public override void OnListItemClick(ListView listView, View itemView, int itemIndex, long itemId)
		{
			Loading= true;
			base.OnListItemClick(listView, itemView, itemIndex, itemId);
			var adapter= ListAdapter as TimelineAdapter;
			if ( adapter != null )
				UpdateStateAsync( adapter[ itemIndex ].AbsoluteLocation, onlyUpdateIfNonEmpty: true );
		}

		public void OnRefresh()
			=> UpdateStateAsync( CurrentDirectory, refreshData: true );

		public bool UpdateState(string dirPath= null, bool onlyUpdateIfNonEmpty= false, bool refreshData= false)
		{
			bool lockTaken= false;
			try {
				updateLock.Enter(ref lockTaken);

				DateTime startTime= default;
				if ( CurrentDuration != TimeSpan.Zero )
					startTime= DateTime.Now - CurrentDuration;

				var newTimeline= StatisticsManager.RetrieveTimeline( dirPath, startTime, refreshData );

				if ( onlyUpdateIfNonEmpty && newTimeline.IsEmpty )
					return false;

				CurrentDirectory= dirPath;
				HeaderText= dirPath?.ToUserPath();

				var adapter= ListAdapter as TimelineAdapter;
				if ( adapter != null )
				{
					adapter.basePath= dirPath;
					adapter.Timeline= newTimeline;
				}

				MainThread.BeginInvokeOnMainThread( UpdateView );

				return true;
			}
			finally {

				if ( lockTaken )
					updateLock.Exit();

				if ( ! updateLock.IsHeld )
					Loading= false;
			}
		}

		private SpinLock updateLock= new SpinLock();

		private bool Loading {
			set {
				if ( mainRefresher != null )
					mainRefresher.Refreshing= value;
				if ( emptyRefresher != null )
					emptyRefresher.Refreshing= value;
			}
		}

		public void UpdateStateAsync(string dirPath= null, bool onlyUpdateIfNonEmpty= false, bool refreshData= false)
			=> Task.Run( () => UpdateState(dirPath, onlyUpdateIfNonEmpty, refreshData) );

		public void UpdateView()
		{
			( ListAdapter as BaseAdapter )?.NotifyDataSetChanged();
			if ( header != null )
				if ( HeaderText == null )
					header.Visibility= ViewStates.Gone;
				else {
					header.Visibility= ViewStates.Visible;
					header.Text= HeaderText;
				}
		}

		/// <summary>
		///  Saves the current directory before the activity is destroyed.
		/// </summary>
		public override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			outState.PutString("currentDirectory", CurrentDirectory);
		}

	}

}