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
using Xamarin.Essentials;
using System.Collections.Generic;

using ListFragment= AndroidX.Fragment.App.ListFragment;
using static Android.Views.ViewGroup;


namespace StorageHistory
{
	using Helpers;
	using static Helpers.Configuration;


	public class AnalysisActivity: ListFragment
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
		TextView header;
		Spinner timeSelector;
			
		public override View OnCreateView(LayoutInflater inflater, ViewGroup mainView, Bundle savedInstanceState)
			=> inflater.Inflate(Resource.Layout.activity_analysis, mainView, false) ;


		/// <summary>
		///  Called when the analysis view and its children are initialized.
		/// </summary>
		public override void OnViewCreated(View view, Bundle savedInstanceState)
		{
			base.OnViewCreated(view, savedInstanceState);

			if ( savedInstanceState != null )
				CurrentDirectory= savedInstanceState.GetString("currentDirectory");

			int lastNumberOfHours= Preferences.Get(AnalysisDuration_KEY, AnalysisDuration_DEFAULT);
			CurrentDuration= TimeSpan.FromTicks( TimeSpan.TicksPerHour * lastNumberOfHours );

			header= view.FindViewById<TextView>( Resource.Id.analysis_header );
			if ( header != null )
				header.Click+= OnHeaderClick;

			timeSelector= view.FindViewById<Spinner>( Resource.Id.analysis_time_selector );
			if ( timeSelector != null )
			{
				var durationStrings= this.retrieveDurationStrings(lastNumberOfHours, out int lastSelectionIndex); // the user-facing counterpart to DurationOptions
				var selectionAdapter= new ArrayAdapter<string> ( Context, Android.Resource.Layout.SimpleSpinnerItem, durationStrings );
				    selectionAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
				timeSelector.Adapter= selectionAdapter;
				timeSelector.SetSelection( lastSelectionIndex, animate: false );
				timeSelector.ItemSelected+= OnTimeSelection;
			}

			ListAdapter= new Adapter ( Context );
			UpdateState( CurrentDirectory );
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
			if ( CurrentDirectory != null )
				UpdateState( System.IO.Path.GetFullPath("..", CurrentDirectory) );
		}

		public void OnTimeSelection(object o, AdapterView.ItemSelectedEventArgs itemArgs)
		{
			int newDuration= DurationOptions[ itemArgs.Position ];
			CurrentDuration= TimeSpan.FromTicks( newDuration * TimeSpan.TicksPerHour );
			Preferences.Set(AnalysisDuration_KEY, newDuration);
			UpdateState( CurrentDirectory );
		}

		public override void OnListItemClick(ListView listView, View itemView, int itemIndex, long itemId)
		{
			base.OnListItemClick(listView, itemView, itemIndex, itemId);
			var adapter= ListAdapter as Adapter;
			if ( adapter != null )
				UpdateState( adapter[ itemIndex ].AbsoluteLocation, onlyUpdateIfNonEmpty: true );
		}

		public bool UpdateState(string dirPath= null, bool onlyUpdateIfNonEmpty= false)
		{
			DateTime startTime= default;
			if ( CurrentDuration != TimeSpan.Zero )
				startTime= DateTime.Now - CurrentDuration;

			var newTimeline= StatisticsManager.RetrieveTimeline( dirPath, startTime ); // ToDo: add startTime based on UI switch between 30 days, 60 days etc.

			if ( onlyUpdateIfNonEmpty && newTimeline.IsEmpty )
				return false;

			CurrentDirectory= dirPath;
			if ( header != null )
				if ( dirPath == null )
					header.Visibility= ViewStates.Gone;
				else {
					header.Visibility= ViewStates.Visible;
					header.Text= dirPath.ToUserPath();
				}

			var adapter= ListAdapter as Adapter;
			if ( adapter != null )
			{
				adapter.basePath= dirPath;
				adapter.Timeline= newTimeline;
			}

			return true;
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
		class Adapter: BaseAdapter
		{
			private Timeline.Directory[] @base;
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
					NotifyDataSetChanged();
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

			public override int Count => @base?.Length ?? 0; // returns 0 if `base` is null

			public override Java.Lang.Object GetItem(int position) => null;

			public override long GetItemId(int position) => position;

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

			public Color Color { set { paint.Color= value; textPaint.Color= value; } }

			public ItemView(Context context): base(context)
			{
				paint= new Paint(PaintFlags.AntiAlias);
				textPaint= new Paint(PaintFlags.AntiAlias);
				textPadding= Resources.GetDimension(Resource.Dimension.analysis_item_text_padding);
				verticalMargins= Resources.GetDimensionPixelSize(Resource.Dimension.analysis_item_vertical_margins);
				LayoutParameters= new LayoutParams( LayoutParams.MatchParent, Resources.GetDimensionPixelSize(Resource.Dimension.analysis_item_height) + verticalMargins );
				paint.StrokeWidth*= 2;
			}

			protected override void OnDraw(Canvas canvas)
			{
				textPaint.TextAlign= Paint.Align.Left;
				Source.GenerateOutput(minTime, maxTime, canvas.Width, canvas.Height-verticalMargins);
				canvas.Translate( 0, verticalMargins / 2.0f );
				canvas.DrawText(Source.AbsoluteLocation.ToUserPath(basePath), textPadding, paint.TextSize + textPadding, textPaint);
				canvas.DrawLines(Source.Output, paint);
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