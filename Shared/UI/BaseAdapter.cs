using Android.Widget;

namespace StorageHistory.Shared.UI
{

	/// <summary>
	///  Common foundation for a <see cref="ListView"/> adapter of items with a single type.
	/// </summary>
	public abstract class BaseAdapter<T>: BaseAdapter
	{

		protected T[] @base;

		public T this[ int position ] => @base[ position ];

		public override int Count => @base?.Length ?? 0; // returns 0 if `base` is null

		public override Java.Lang.Object GetItem(int position) => null;

		public override long GetItemId(int position) => position;

	}

}