using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Android.Graphics;

namespace StorageHistory.Helpers
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


		#region Helpers For Converting Strings To FileChanges

			/// <summary>
			///  Converts an enumerable of strings into an enumerable of FileChanges with changeType = None
			/// </summary>
			public static IEnumerable<FileChange> ToFileChanges(this IEnumerable<string> source) => new StringToFileChangeEnumerable(source);

			private struct StringToFileChangeEnumerable: IEnumerable<FileChange> {
				private IEnumerable<string> source;
				public StringToFileChangeEnumerable(IEnumerable<string> source) => this.source= source;
				public IEnumerator<FileChange> GetEnumerator() => new StringToFileChangeEnumerator( source.GetEnumerator() );
				IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
			}

			private class StringToFileChangeEnumerator: IEnumerator<FileChange> {
				private IEnumerator<string> source;
				public FileChange Current => source.Current;
				object IEnumerator.Current => source.Current;
				public StringToFileChangeEnumerator(IEnumerator<string> source) => this.source= source;
				public void Dispose() => source.Dispose();
				public bool MoveNext() => source.MoveNext();
				public void Reset() => source.Reset();
			}

		#endregion


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
		///  Returns the string represented by the span of characters.
		/// </summary>
		public static string AsString(this ReadOnlySpan<char> @this)
		{
			var union= new SpanUnion { UnmanagedString= @this };
			union.Pointer-= RuntimeHelpers.OffsetToStringData;
			return union.ManagedString;
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