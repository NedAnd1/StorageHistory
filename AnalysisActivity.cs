using System;
using Android.OS;
using Android.App;
using Android.Views;
using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Widget;
using Android.Text.Format;
using AndroidX.Fragment.App;
using AndroidX.SwipeRefreshLayout.Widget;
using Xamarin.Essentials;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

using static Android.Views.ViewGroup;

namespace StorageHistory
{
	using Helpers;
	using static Helpers.Configuration;
	using static Helpers.RuntimeExtensions;

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

			ListAdapter= new Adapter ( Context );

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
			var adapter= ListAdapter as Adapter;
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

				var adapter= ListAdapter as Adapter;
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



		/// <summary>
		///  Manages the conversion of <see cref="Helpers.Timeline"/> data into a list of graphs for the most consequential directories. 
		/// </summary>
		class Adapter: BaseAdapter<Timeline.Directory>
		{
			private DateTime startTime;
			private DateTime endTime;
			private Context context;
			public string basePath;

			public Adapter(Context context) => this.context= context;
			public Adapter(Context context, Timeline source) {
				this.context= context;
				this.Timeline= source;
			}

			public Timeline.Directory this[ int position ] => @base[ position ];

			public Timeline Timeline {
				set {
					if ( value.directories == null )
						@base= null;
					else {
						@base= new Timeline.Directory [ value.directories.Count ];
						value.directories.CopyTo(@base, 0);
						Array.Sort( @base );
					}
					startTime= value.startTime;
					endTime= value.endTime;
				}
			}
			
			public override View GetView(int position, View convertView, ViewGroup parent)
			{
				var view= convertView as ItemView;
				if ( view == null )
					view= new ItemView( context );

				view.minTime= this.startTime;
				view.maxTime= this.endTime;
				view.basePath= this.basePath;
				view.Source= @base[ position ];
				view.Color= view.Source.GetHashColor(); // a unique color based on the hash code of the directory path

				return view;
			}
		}

		/// <summary>
		///  A graph that shows the change in size of a directory over time. 
		/// </summary>
		class ItemView: View
		{
			private Paint paint;
			private Paint textPaint;
			private float textPadding;
			private int verticalMargins;
			public string basePath;
			public DateTime minTime;
			public DateTime maxTime;
			public Timeline.Directory Source;

			private static readonly Paint AxisPaint= new Paint(PaintFlags.AntiAlias) { Color= new Color(0x7F888888) };
			private static void initAxis(Context context)
			{
				float density= context.Resources.DisplayMetrics.Density,
				      dashLength= 8f * density;
				AxisPaint.SetPathEffect(  new DashPathEffect( new float[]{ dashLength, dashLength }, dashLength )  ) ;
				AxisPaint.SetStyle(Paint.Style.Stroke);
				AxisPaint.StrokeWidth= density;
			}

			public Color Color { set { paint.Color= value; textPaint.Color= value; } }

			public ItemView(Context context): base(context)
			{
				paint= new Paint(PaintFlags.AntiAlias);
				textPaint= new Paint(PaintFlags.AntiAlias);
				textPadding= Resources.GetDimension(Resource.Dimension.analysis_item_text_padding);
				verticalMargins= Resources.GetDimensionPixelSize(Resource.Dimension.analysis_item_vertical_margins);
				LayoutParameters= new LayoutParams( LayoutParams.MatchParent, Resources.GetDimensionPixelSize(Resource.Dimension.analysis_item_height) + verticalMargins );
				if ( AxisPaint.PathEffect == null )
					initAxis(context);
			}

			protected override void OnDraw(Canvas canvas)
			{
				// generate the graph of size changes
				Source.GenerateOutput(minTime, maxTime, canvas.Width, canvas.Height-verticalMargins);
				canvas.Translate( 0, verticalMargins / 2.0f );

				// draw the dashed line that represents the x-axis i.e. no change in size
				if ( (int)Source.AxisHeight + 1 < canvas.Height )  // if it's above the bottom of the graph
				{
					var axisPath= new Path();
					axisPath.MoveTo(0, Source.AxisHeight);
					axisPath.LineTo(canvas.Width, Source.AxisHeight);
					canvas.DrawPath(axisPath, AxisPaint);
				}

				// draw the graph of size changes
				canvas.DrawLines(Source.Output, paint);

				// write the name of the directory
				textPaint.TextAlign= Paint.Align.Left;
				canvas.DrawText(Source.AbsoluteLocation.ToUserPath(basePath), textPadding, paint.TextSize + textPadding, textPaint);

				// write the amount of the directory's change in size
				textPaint.TextAlign= Paint.Align.Right;
				canvas.DrawText(SizeDeltaString, canvas.Width-textPadding, canvas.Height-textPadding, textPaint);
			}

			private string SizeDeltaString
			{
				get {
					long sizeDelta= Source.SizeDelta;
					string prefix;
					if ( sizeDelta < 0 )
					{
						sizeDelta= -sizeDelta;
						prefix= "− ";
					}
					else prefix= "+ ";

					return prefix + Formatter.FormatFileSize(Context, sizeDelta);
				}
			}
		}


	}


}