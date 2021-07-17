using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace StorageHistory.Shared.Logic
{

	/// <summary>
	///  Extension methods for abstracting repetetive or low-level runtime functionalities. 
	/// </summary>
	static class RuntimeExtensions
	{
		
		/// <summary>
		///  Returns the secondary number if the primary is zero.
		/// </summary>
		public static int Or(this int primary, int secondary)
		{
			if ( primary != 0 )
				return primary;
			else return secondary;
		}


		/// <summary>
		///  Converts the string to an array of UTF-16 bytes.
		/// </summary>
		public static byte[] ToByteArray(this string @this)
		{
			var output= new byte [ @this.Length * sizeof(char) ];
			@this.CopyTo(0, Unsafe.As<char[]>(output), 0, @this.Length);
			return @output;
		}


		/// <summary>
		///  Retrieves the element at the given index without a bounds check.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref T FastGet<T>(this Span<T> src, int index)
			=> ref Unsafe.Add( ref MemoryMarshal.GetReference(src), index );

		/// <summary>
		///  Retrieves the element at the given index without a bounds check.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref readonly T FastGet<T>(this ReadOnlySpan<T> src, int index)
			=> ref Unsafe.Add( ref MemoryMarshal.GetReference(src), index );

	
		/// <summary>
		///  Slices the span without any parameter boundary checks.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Span<T> FastSlice<T>(this Span<T> src, int start)
			=> MemoryMarshal.CreateSpan( ref src.FastGet(start), src.Length-start );

		/// <summary>
		///  Slices the span without any parameter boundary checks.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReadOnlySpan<T> FastSlice<T>(this ReadOnlySpan<T> src, int start)
			=> MemoryMarshal.CreateReadOnlySpan( ref Unsafe.AsRef( src.FastGet(start) ), src.Length-start );

		/// <summary>
		///  Slices the string into a span of characters without any copying or parameter boundary checks.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReadOnlySpan<char> FastSlice(this string src, int start)
			=> MemoryMarshal.CreateReadOnlySpan( ref Unsafe.AsRef( src.AsSpan().FastGet(start) ), src.Length-start );


		/// <summary>
		///  Slices the span without any parameter boundary checks.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Span<T> FastSlice<T>(this Span<T> src, int start, int length)
			=> MemoryMarshal.CreateSpan( ref src.FastGet(start), length-start );

		/// <summary>
		///  Slices the span without any parameter boundary checks.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReadOnlySpan<T> FastSlice<T>(this ReadOnlySpan<T> src, int start, int length)
			=> MemoryMarshal.CreateReadOnlySpan( ref Unsafe.AsRef( src.FastGet(start) ), length-start );

		/// <summary>
		///  Slices the string into a span of characters without any copying or parameter boundary checks.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReadOnlySpan<char> FastSlice(this string src, int start, int length)
			=> MemoryMarshal.CreateReadOnlySpan( ref Unsafe.AsRef( src.AsSpan().FastGet(start) ), length-start );


			
		/// <summary>
		///  Converts the string to a span of characters, assuming it isn't null (otherwise it throws a null reference exception).
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReadOnlySpan<char> AsSpan(this string src)
			=> MemoryMarshal.CreateReadOnlySpan( ref Unsafe.As<String>(src).FirstChar, src.Length );

		/// <summary>
		///  Creates an empty span of characters if the given string is null.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReadOnlySpan<char> AsNullableSpan(this string src)
			=> MemoryExtensions.AsSpan(src);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Span<char> AsWritableSpan(this string src)
			=> MemoryMarshal.CreateSpan( ref Unsafe.As<String>(src).FirstChar, src.Length );

		/// <summary> For accessing private string fields directly without reflection. </summary>
		private class String
		{
			public int Length;
			public char FirstChar;
		}


		
		/// <summary>
		///  Returns the string referenced by an unsliced span of characters. 
		/// </summary>
		/// <remarks>
		///  An invalid reference is returned if the span doesn't actually point to the start of a mamaged string.
		/// </remarks>
		internal static unsafe string AsString(this ReadOnlySpan<char> @this)
		{
			var stringPointer= (byte*)Unsafe.AsPointer( ref MemoryMarshal.GetReference(@this) ) - RuntimeHelpers.OffsetToStringData;
			return Unsafe.Read<string>( &stringPointer );
		}


		/// <summary>
		///  Concatenates the string with a span of characters. 
		/// </summary>
		public static string Concat(this string strA, ReadOnlySpan<char> strB)
        {
			var strOut= new string ( '\0' , strA.Length + strB.Length ); // string .ctor does the checking for us
			var outSpan= strOut.AsWritableSpan();
			strA.AsSpan().CopyTo( outSpan );
			strB.CopyTo( outSpan.FastSlice(strA.Length) );
			return strOut;
        }


		/// <summary>
		///  Emulates ECMAScript slicing behavior.
		/// </summary>
		public static Span<char> slice(this Span<char> @this, int start, int end)
			#if DEBUG
				=> @this.Slice(start, end-start);
			#else
				=> @this.FastSlice(start, end-start);
			#endif

	}

}