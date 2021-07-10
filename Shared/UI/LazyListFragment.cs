using Android.OS;
using Android.Views;
using AndroidX.Fragment.App;
using Java.Lang;
using Java.Interop;
using Java.Lang.Reflect;

namespace StorageHistory.Shared.UI
{

	/// <summary>
	///  Optimizes the creation of <see cref="MainActivity"/> list fragments for faster startup / resume.
	/// </summary>
	public class LazyListFragment: ListFragment, ViewStub.IOnInflateListener
	{

		/// <summary>
		///  If the given view will be visible, immediately adds its layout file to the view,
		///   otherwise the layout file is added later on.
		/// </summary>
		public View Inflate(int viewIndex, LayoutInflater inflater, int layoutResource, ViewGroup parent)
		{
			var viewPager= ( inflater.Context as MainActivity )?.ViewPager;
			if ( viewPager == null || viewPager.CurrentItem == viewIndex )
				return inflater.Inflate( layoutResource, parent, attachToRoot: false ) ;  // adds the xml layout file to the main view as a fragment
			else return new LazyView(this, layoutResource);  // if the view won't be visible...
		}	                                                  // add its layout file after the app finishes loading
		
		/// <summary>
		///  Called after the view or its lazy stub is created.
		/// </summary>
		public override void OnViewCreated(View view, Bundle savedInstanceState)
		{
			if ( view is LazyView is false )
				OnInflate(view, immediate: true);
		}

		void ViewStub.IOnInflateListener.OnInflate(ViewStub _, View view)
			=> OnInflate(view, immediate: false);

		/// <summary>
		///  Called when the view and its children are initialized.
		/// </summary>
		/// <param name="immediate">
		///	 Whether the view was inflated immediately after it was created.
		/// </param>
		public virtual void OnInflate(View view, bool immediate)
			=> base.OnViewCreated(view, null);


		/// <summary>
		///  Uses a view stub to delay inflation of the given layout file until after the app finishes loading.
		/// </summary>
		private class LazyView: ViewGroup
		{
			/// <summary>
			///  Creates a thinly wrapped view stub for seamless lazy inflation. 
			/// </summary>
			public LazyView(LazyListFragment parent, int layoutResource): base(parent.Context)
				=> this.AddViewInLayout(
				            new ViewStub( parent.Context, layoutResource ).InvokeOnReady( listener: parent ) ,
				            index: 0,
				            @params: null,  // prevents the view stub from overriding the layout parameters of its replacement
				            preventRequestLayout: true  // skips the view's null layout parameter check
				        );

			/// <summary>
			///  Allows the view stub to begin with null layout parameters.
			/// </summary>
			protected override bool CheckLayoutParams(LayoutParams p) => true;

			/// <summary>
			///  Ensures that the child has the same boundaries as the LazyView.
			/// </summary>
			protected override void OnLayout(bool changed, int l, int t, int r, int b)
				=> this.GetChildAt(0)?.Layout(0, 0, r-l, b-t);

			/// <summary>
			///  Ensures that the LazyView doesn't alter the layout relationship between its parent and its child.
			/// </summary>
			protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
			{
				var child= this.GetChildAt(0);
				if ( child != null )
				{
					child.Measure(widthMeasureSpec, heightMeasureSpec);
					SetMeasuredDimension(child.MeasuredWidth, child.MeasuredHeight);
				}
				else SetMeasuredDimension(widthMeasureSpec, heightMeasureSpec);
			}

			/* // unused by our layouts since they're all MATCH_PARENT

				private static readonly Method LayoutParamsChecker= Class.FromType(typeof(ViewGroup)).GetDeclaredMethod( "checkLayoutParams", Class.FromType(typeof(LayoutParams)) );
				private static readonly Method LayoutParamsGenerator= Class.FromType(typeof(ViewGroup)).GetDeclaredMethod( "generateLayoutParams", Class.FromType(typeof(LayoutParams)) );
				static LazyView()
					=> LayoutParamsGenerator.Accessible= LayoutParamsChecker.Accessible= true;  // enables access to these protected methods

				/// <summary>
				///  Called by the child when its layout parameters change.
				/// </summary>
				[Export] /* @Override * /
				public void OnSetLayoutParams(View _, LayoutParams newParams)
				{
					if ( Parent is ViewGroup parent )  // if the view has a ViewGroup parent
						if ( (bool)(Boolean)LayoutParamsChecker.Invoke( parent, newParams ) is false )   // if the new parameters aren't valid for our parent...
							newParams= LayoutParamsGenerator.Invoke( parent, newParams ) as LayoutParams; // use the its generator to create a valid version
					base.LayoutParameters= newParams;
				}

			*/
		}

	}

}