using System;
using Android.Views;
using Android.Content;
using Android.Graphics;
using Android.Text.Format;

using static Android.Views.ViewGroup;

namespace StorageHistory.Analysis
{
	using Shared.UI;
	using Shared.Logic;

	/// <summary>
	///  Manages the conversion of <see cref="Analysis.Timeline"/> data into a list of graphs for the most consequential directories. 
	/// </summary>
	class TimelineAdapter: BaseAdapter<Timeline.Directory>
	{
		private DateTime startTime;
		private DateTime endTime;
		private Context context;
		public string basePath;

		public TimelineAdapter(Context context) => this.context= context;
		public TimelineAdapter(Context context, Timeline source) {
			this.context= context;
			this.Timeline= source;
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
			}
		}
			
		public override View GetView(int position, View convertView, ViewGroup parent)
		{
			var view= convertView as Graph;
			if ( view == null )
				view= new Graph( context );

			view.minTime= this.startTime;
			view.maxTime= this.endTime;
			view.basePath= this.basePath;
			view.Source= @base[ position ];
			view.Color= view.Source.GetHashColor(); // a unique color based on the hash code of the directory path

			return view;
		}

		/// <summary>
		///  A graph that shows the change in size of a directory over time. 
		/// </summary>
		class Graph: View
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
			private static readonly Path AxisPath= new Path();
			private static void initAxis(Context context)
			{
				float density= context.Resources.DisplayMetrics.Density,
				      dashLength= 8f * density;
				AxisPaint.SetPathEffect(  new DashPathEffect( new float[]{ dashLength, dashLength }, dashLength )  ) ;
				AxisPaint.SetStyle(Paint.Style.Stroke);
				AxisPaint.StrokeWidth= density;
			}

			public Color Color { set { paint.Color= value; textPaint.Color= value; } }

			public Graph(Context context): base(context)
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
					AxisPath.Rewind();
					AxisPath.MoveTo(0, Source.AxisHeight);
					AxisPath.LineTo(canvas.Width, Source.AxisHeight);
					canvas.DrawPath(AxisPath, AxisPaint);
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