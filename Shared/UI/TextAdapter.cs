using Android.Views;
using Android.Widget;
using Android.Content;

namespace StorageHistory.Shared.UI
{

	/// <summary>
	///  Lightweight class for managing the state of a text-based <see cref="ListView"/>.
	/// </summary>
	public class TextAdapter: BaseAdapter<string>
	{
		private LayoutInflater inflater;
		private int itemResource;

		public string[] Items
		{
			set {
				@base= value;
				NotifyDataSetChanged();
			}
		}

		/// <param name="itemResource">
		///  The root of this layout must derive from <see cref="TextView"/>
		/// </param>
		public TextAdapter(Context context, int itemResource)
		{
			this.inflater= LayoutInflater.From(context);
			this.itemResource= itemResource;
		}

		public override View GetView(int position, View view, ViewGroup parent)
		{
			if ( view == null )
				view= inflater.Inflate( itemResource, parent, attachToRoot: false );
				
			( (TextView)view ).Text= @base[ position ];

			return view;
		}
	}

}