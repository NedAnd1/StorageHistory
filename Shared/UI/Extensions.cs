using Android.OS;
using Android.Views;
using Android.Graphics;
using System.Threading.Tasks;
using System;

namespace StorageHistory.Shared.UI
{

	/// <summary>
	///  UI related extension methods. 
	/// </summary>
	static class Extensions
	{
		/// <summary>
		///  Inflates the view stub when the main thread is no longer busy.
		/// </summary>
		public static ViewStub InvokeOnReady(this ViewStub viewStub, ViewStub.IOnInflateListener listener= null)
		{
			viewStub.SetOnInflateListener(listener);

			InvokeOnReady( () => viewStub.Visibility= ViewStates.Visible );
			
			return viewStub;
		}

		/// <summary>
		///  Invokes the given action on the main thread w hen it's no longer busy.
		/// </summary>
		public static void InvokeOnReady(Action action)
		{
			var handler= invokeHandler;

			if ( handler == null || handler.Looper != Looper.MainLooper )
                invokeHandler= handler= new Handler(Looper.MainLooper);

			handler.Post(action);
		}
		private static volatile Handler invokeHandler;

		/// <summary>
		///  Invokes the given action asynchronously when the main thread is no longer busy.
		/// </summary>
		public static void InvokeTaskOnReady(Action taskAction)
			=> InvokeOnReady( () => Task.Run( taskAction ) );

		/// <summary>
		///  Invokes the given action asynchronously with the given object when the main thread is no longer busy.
		/// </summary>
		public static void InvokeTaskOnReady(Action<object> taskAction, object obj)
			=> InvokeOnReady( () => Task.Factory.StartNew( taskAction, obj ) );

		/// <summary>
		///  Returns a unique color based on the hash code of the given object.
		/// </summary>
		public static Color GetHashColor(this object obj)
		{
			var hashCode= obj.GetHashCode();
			int r=  (  hashCode >> 14   ^   hashCode >> 29 << 3  )  & 0x7F + 64,
			    g=  (  hashCode >>  7   ^   hashCode >> 25 << 3  )  & 0x7F + 64,
			    b=  (  hashCode >>  0   ^   hashCode >> 21 << 3  )  & 0x7F + 64;
			return new Color(r, g, b);
		}

	}

}