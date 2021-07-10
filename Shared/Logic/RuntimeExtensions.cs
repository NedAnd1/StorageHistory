using System;
using System.Collections;
using System.Collections.Generic;
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
		///  Returns the string referenced by an unsliced span of characters. 
		/// </summary>
		internal static string AsString(this ReadOnlySpan<char> @this)
		{
			var union= new SpanUnion { UnmanagedString= @this };
			union.Pointer-= RuntimeHelpers.OffsetToStringData;
			return union.ManagedString;
		}


		/// <summary>
		///  Concatenates the string with a span of characters. 
		/// </summary>
		public static unsafe string Concat(this string strA, ReadOnlySpan<char> strB)
        {
            var strOut= new string( '\0' ,  checked(strA.Length+strB.Length) );
            fixed ( char* outPtr= strOut )
            {
                var outSpan= new Span<char>(outPtr, strOut.Length);
				strA.AsSpan().CopyTo( outSpan );
				strB.CopyTo( outSpan.Slice(strA.Length) );
            }
            return strOut;
        }


		/// <summary>
		///  Emulates ECMAScript slicing behavior.
		/// </summary>
		public static Span<char> slice(this Span<char> @this, int start, int end) => @this.Slice(start, end-start);


		[StructLayout(LayoutKind.Explicit)]
		private ref struct SpanUnion
		{
			[FieldOffset(0)]
			public string ManagedString;

			[FieldOffset(0)]
			public ReadOnlySpan<char> UnmanagedString;

			[FieldOffset(0)]
			public IntPtr Pointer;
		}

		[StructLayout(LayoutKind.Explicit)]
		private struct ByteUnion
		{
			private const string @string= "ABCD"; 

			public static readonly int ArrayStringDelta=
				Unsafe.ByteOffset( ref Unsafe.AsRef(@string.AsSpan().GetPinnableReference()), ref Unsafe.As<char[]>(@string)[0] ).ToInt32(); 

			[FieldOffset(0)]
			public string ManagedString;

			[FieldOffset(0)]
			public byte[] ByteArray;

			[FieldOffset(0)]
			public IntPtr Pointer;

		}

		[StructLayout(LayoutKind.Explicit)]
		struct ColorUnion
		{
			[FieldOffset(0)]
			public byte r;

			[FieldOffset(1)]
			public byte g;

			[FieldOffset(2)]
			public byte b;

			[FieldOffset(0)]
			public ushort rg;

			[FieldOffset(1)]
			public ushort gb;
		}

	}

}