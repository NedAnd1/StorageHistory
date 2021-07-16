using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace StorageHistory.Shared.Logic
{

	/// <summary>
	///  Efficient hybrid representation for a string or span of characters.
	/// </summary>
	public ref struct Characters
	{
		/// <summary>
		///  An instance with its length set to this value indicates that it's backed by a string.
		/// </summary>
		private const int InString= -1;

		[StructLayout(LayoutKind.Explicit)]
		private unsafe struct Union
		{
			[FieldOffset(0)]
			public string String;

			[FieldOffset(0)]
			public char* Pointer; // no ref fields or access to ByReference<T> :(
		}

		private Union baseValue;
		private int length;

		/// <summary>
		///  Gets or sets the underlying span of characters.
		/// </summary>
		private unsafe ReadOnlySpan<char> spanValue
		{
			get => MemoryMarshal.CreateReadOnlySpan( ref *baseValue.Pointer, length );  // generates the span as needed

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set {
				length= value.Length;
				baseValue.Pointer= (char*)Unsafe.AsPointer( ref MemoryMarshal.GetReference(value) );  // retrieves a reference to the span's target
			}
		}

		/// <summary>
		///  Gets or sets the underlying string of characters.
		/// </summary>
		private string stringValue
		{
			get => baseValue.String;
			set {
				baseValue.String= value;
				length= InString;
			}
		}

		private bool isSpan => length != InString;

		private bool isNonEmptySpan => (uint)( length - InString ) > -InString;

		/// <summary>
		///  Returns the underlying sequence of characters as a string, performing a copy/conversion only when necessary.
		/// </summary>
		public override string ToString()
		{
			if ( isSpan )
				stringValue= new string ( spanValue );
			return stringValue;
		}

		/// <summary>
		///  Returns a reference to the underlying text as a readonly span of characters.
		/// </summary>
		public ReadOnlySpan<char> Span
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				if ( isSpan )
					return spanValue;
				else return stringValue.AsSpan();
			}
		}

		/// <summary>
		///  Most efficient way of checking if the underlying text is null.
		/// </summary>
		public bool IsNull
			=> stringValue is null;

		/// <summary>
		///  Most efficient way of checking if the underlying text is empty but not null.
		/// </summary>
		public bool IsEmpty
		{
			get {
				if ( stringValue is null || isNonEmptySpan )
					return false;  // most common scenario
				else if ( length is 0 )
					return true;
				else return stringValue.Length is 0;
			}
		}

		/// <summary>
		///  Most efficient way of checking if the underlying string is null or empty.
		/// </summary>
		public bool IsNullOrEmpty
			=> stringValue is null || length is 0 || ( length is InString ? stringValue.Length is 0 : false );

		/// <summary>
		///  Retrieves the length of the underlying text (with a possible null reference exception if the instance's underlying type is a string).
		/// </summary>
		public int Length
			=> length is InString ? stringValue.Length : length ;

		/// 
		/// <summary>
		///  Compares the sequence of characters to a string, using <see cref="MemoryExtensions"/> when appropriate.
		/// </summary>
		/// 
		/// <param name="A">
		///  Cached as a method parameter to prevent redundant null checks.
		/// </param>
		/// 
		/// <remarks>
		///  Correctly differentiates a null sequence of characters from an empty sequence
		///   (even if one `string` is actually just a char*).
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool EqualsHelper(string A, string B)                  // non-static to easily reference A's span-related properties
			=>  ReferenceEquals(A, B)  ||  A != null && B != null && ( isSpan ? spanValue : A ).SequenceEqual(B) ;

		/// 
		/// <summary>
		///  Compares the two sequences of characters, using <see cref="MemoryExtensions"/> when appropriate.
		/// </summary>
		/// 
		/// <remarks>
		///  <paramref name="strA"/> and <paramref name="strB"/> are cached as method parameters to prevent redundant null checks.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool EqualsHelper(Characters A, string strA, Characters B, string strB)
			=>  ReferenceEquals(strA, strB)  ||  strA != null && strB != null && ( A.isSpan ? A.spanValue : strA ).SequenceEqual( B.isSpan ? B.spanValue : strB ) ;

		public static bool operator ==(Characters A, string B)
			=> A.EqualsHelper(A.stringValue, B);

		public static bool operator !=(Characters A, string B)
			=> ! A.EqualsHelper(A.stringValue, B);

		public static bool operator ==(Characters A, Characters B)
			=> EqualsHelper(A, A.stringValue, B, B.stringValue);

		public static bool operator !=(Characters A, Characters B)
			=> ! EqualsHelper(A, A.stringValue, B, B.stringValue);

		public static implicit operator Characters(string src)
			=> new Characters { stringValue= src };

		public static implicit operator Characters(ReadOnlySpan<char> src)
			=> new Characters { spanValue= src };

		public static implicit operator Characters(Span<char> src)
			=> new Characters { spanValue= src };

		/// <summary>
		///  See <see cref="Span"/>
		/// </summary>
		public static implicit operator ReadOnlySpan<char>(Characters @this)
			=> @this.Span;

		/// <summary>
		///  See <see cref="ToString"/>
		/// </summary>
		public static explicit operator string(Characters @this)
			=> @this.ToString();

		/// <summary>
		///  If the underlying string isn't null, returns true.
		/// </summary>
		public static implicit operator bool(Characters @this)
			=> @this.stringValue != null;

		/// <summary> Not suported. </summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override bool Equals(object B)
			=> throw new NotSupportedException();

		/// <summary> Not suported. </summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode()
			=> throw new NotSupportedException();

	}

}