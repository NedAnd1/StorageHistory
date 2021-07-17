using Android.Views;
using Android.Widget;
using Android.Content;

namespace StorageHistory.Shared.UI
{

	/// <summary>
	///  Places the appropriate icon over the view returned by <see cref="TextAdapter/>.
	/// </summary>
	public class DirectoryAdapter: TextAdapter
	{
		private Context context;

		public int IndexOfFirstFile;

		/// <param name="itemResource">
		///  The root of this layout must derive from <see cref="Button"/>
		/// </param>
		public DirectoryAdapter(Context context, int itemResource): base(context, itemResource)
			=> this.context= context;


		public override View GetView(int position, View view, ViewGroup parent)
		{
			var textView= base.GetView(position, view, parent) as TextView;

			int icon= Resource.Drawable.folder_icon_24;
			if ( position >= IndexOfFirstFile )
			{
				icon= Resource.Drawable.file_icon_24;
			}

			textView.SetCompoundDrawablesWithIntrinsicBounds( left: icon, 0, 0, 0 );

			return textView;
		}
	}

}