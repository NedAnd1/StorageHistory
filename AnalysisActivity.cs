using System;
using Android.OS;
using Android.App;
using Android.Views;
using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Widget;
using AndroidX.Fragment.App;

using ListFragment= AndroidX.Fragment.App.ListFragment;

namespace StorageHistory
{
	using Helpers;

	public class AnalysisActivity: ListFragment
	{
		public string CurrentDirectory;
		TextView header;
			
		public override View OnCreateView(LayoutInflater inflater, ViewGroup mainView, Bundle savedInstanceState)
			=> inflater.Inflate(Resource.Layout.activity_analysis, mainView, false) ;


		/// <summary>
		///  Called when the analysis view and its children are initialized.
		/// </summary>
		public override void OnViewCreated(View view, Bundle savedInstanceState)
		{
			base.OnViewCreated(view, savedInstanceState);

			header= view.FindViewById<TextView>( Resource.Id.analysis_header );
			if ( header != null )
				header.Click+= OnHeaderClick;
			ListAdapter= new Adapter ( Context );
			UpdateState();
		}
		
		public void OnHeaderClick(object o, EventArgs e)
		{
			if ( CurrentDirectory != null )
				UpdateState( System.IO.Path.GetFullPath("..", CurrentDirectory) );
		}

		public override void OnListItemClick(ListView listView, View itemView, int itemIndex, long itemId)
		{
			base.OnListItemClick(listView, itemView, itemIndex, itemId);
			var adapter= ListAdapter as Adapter;
			if ( adapter != null )
				UpdateState( adapter[ itemIndex ].absoluteLocation, onlyUpdateIfNonEmpty: true );
		}

		public bool UpdateState(string dirPath= null, bool onlyUpdateIfNonEmpty= false)
		{
			var newTimeline= StatisticsManager.RetrieveTimeline( dirPath );

			if ( onlyUpdateIfNonEmpty && newTimeline.IsEmpty )
				return false;

			CurrentDirectory= dirPath;
			if ( header != null )
				if ( dirPath == null )
					header.Visibility= ViewStates.Gone;
				else {
					header.Visibility= ViewStates.Visible;
					header.Text= dirPath;
				}

			var adapter= ListAdapter as Adapter;
			if ( adapter != null )
				adapter.Timeline= newTimeline;

			return true;
		}



		class Adapter: BaseAdapter
		{
			private Timeline.Directory[] @base;
			private DateTime startTime;
			private DateTime endTime;
			private Context context;

			public Adapter(Context context) => this.context= context;
			public Adapter(Context context, Timeline source) {
				this.context= context;
				this.Timeline= source;
			}

			public Timeline.Directory this[ int position ] {
				get => @base[ position ];
			}

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
				view.Source= @base[ position ];
				view.Color= view.Source.GetHashColor(); // a unique color based on the hash code of the directory path

				return view;
			}

			public override int Count => @base?.Length ?? 0; // returns 0 if `base` is null

			public override Java.Lang.Object GetItem(int position) => null;

			public override long GetItemId(int position) => position;

		}

		class ItemView: View
		{
			private Paint paint;
			private Paint textPaint;
			public DateTime minTime;
			public DateTime maxTime;
			public Timeline.Directory Source;

			public Color Color { set { paint.Color= value; textPaint.Color= value; } }

			public ItemView(Context context): base(context)
			{
				paint= new Paint(PaintFlags.AntiAlias);
				textPaint= new Paint(PaintFlags.AntiAlias);
				paint.StrokeWidth*= 2;
			}

			protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
			{
				int minWidth= Math.Max( 64, SuggestedMinimumWidth ),
					minHeight=  Math.Max( 64, SuggestedMinimumHeight );
				SetMeasuredDimension( GetDefaultSize(minWidth, widthMeasureSpec), GetDefaultSize(minHeight, heightMeasureSpec) );
			}

			protected override void OnDraw(Canvas canvas)
			{
				Source.GenerateOutput(minTime, maxTime, canvas.Width, canvas.Height);
				canvas.DrawText(Source.absoluteLocation, 0, paint.TextSize + 8, textPaint);
				canvas.DrawLines(Source.output, paint);
			}
		}

	}



}