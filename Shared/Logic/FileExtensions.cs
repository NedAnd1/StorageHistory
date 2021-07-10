using System;
using System.Text;
using Java.IO;
using Android.Systems;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Threading;

namespace StorageHistory.Shared.Logic
{
	/// <summary>
	///  Low-level extension methods for reading and writing binary files using Linux systems calls.
	/// </summary>
	static class FileExtensions
	{
		public const int BufferCapacity= 8192;
		private const int bufferMaxChars= BufferCapacity / sizeof(char);

		[ThreadStatic] private static byte[] binaryBuffer; // ensures thread-safety for each buffer
		[ThreadStatic] private static int bufferOffset;
		[ThreadStatic] private static int bufferEnd;
		[ThreadStatic] private static StringBuilder stringBuffer;

		private static void initThread() {
			binaryBuffer= new byte [ BufferCapacity ];
			stringBuffer= new StringBuilder(bufferMaxChars);
		}



		/// <summary>
		///  Writes the collection to a file as strings seperated by the null-character.
		/// </summary>
		public static void WriteTo(this HashSet<string> @this, FileDescriptor unixFile, bool truncate= true)
		{
			foreach ( string item in @this )
				item.WriteTo(unixFile); // calls the `WriteTo` extension method below
			
			if ( truncate )
				Android.Systems.Os.Ftruncate( unixFile,  Android.Systems.Os.Lseek(unixFile, 0, OsConstants.SeekCur) );
		}


		/// <summary>
		///  Writes a string to a file and terminates the file with the null character or a given custom character.
		/// </summary>
		public static void WriteTo(this string @this, FileDescriptor unixFile, char terminator= '\0')
		{
			if ( binaryBuffer == null )
				initThread();

			int L0= @this.Length,
			    L;

			for ( L= L0; L >= bufferMaxChars; L-= bufferMaxChars ) // while string is bigger than buffer
			{
				@this.CopyTo(L0-L, Unsafe.As<char[]>(binaryBuffer), 0, bufferMaxChars); // copy & write a block at a time
				Android.Systems.Os.Write(unixFile, binaryBuffer, 0, BufferCapacity);
			}

			@this.CopyTo(L0-L, Unsafe.As<char[]>(binaryBuffer), 0, L);
			Unsafe.As<char[]>( binaryBuffer )[L]= terminator;
			Android.Systems.Os.Write(unixFile, binaryBuffer, 0, ( L + 1 ) * sizeof(char) );
		}


		/// <summary>
		///  Writes to a file at the given offset (without `lseek`).
		/// </summary>
		public static void WriteTo(this string @this, FileDescriptor unixFile, long fileOffset, char terminator= '\0')
		{
			if ( binaryBuffer == null )
				initThread();

			int L0= @this.Length,
			    L;

			for ( L= L0; L >= bufferMaxChars; L-= bufferMaxChars ) // while string is bigger than buffer
			{
				@this.CopyTo(L0-L, Unsafe.As<char[]>(binaryBuffer), 0, bufferMaxChars); // copy & write a block at a time
				Android.Systems.Os.Pwrite(unixFile, binaryBuffer, 0, BufferCapacity, fileOffset);
				fileOffset+= BufferCapacity;
			}

			@this.CopyTo(L0-L, Unsafe.As<char[]>(binaryBuffer), 0, L);
			Unsafe.As<char[]>( binaryBuffer )[L]= terminator;
			Android.Systems.Os.Pwrite(unixFile, binaryBuffer, 0, ( L + 1 ) * sizeof(char), fileOffset );
		}


		/// <summary>
		///  Writes a single character to a file.
		/// </summary>
		public static void WriteTo(this char @this, FileDescriptor unixFile)
		{
			if ( binaryBuffer == null )
				initThread();
			Unsafe.As<char[]>( binaryBuffer )[0]= @this;
			Android.Systems.Os.Write(unixFile, binaryBuffer, 0, sizeof(char));
		}



		/// <summary>
		///  Gets a hashed collection from a file with strings seperated by the null-character.
		/// </summary>
		public static HashSet<string> GetStrings(this FileDescriptor @this)
		{
			var stringSet= new HashSet<string>();
			string newString= ReadString(@this, resetState: true);
			while ( newString != null )
			{
				stringSet.Add(newString);
				newString= ReadString(@this);
			}

			return stringSet;
		}



		/// <summary>
		///  Reads the value of key-value pair of null-terminated strings from the given file.
		/// </summary>
		public static string ReadDictionaryEntry(this FileDescriptor @this, string key, out long entryFileOffset, char terminator= '\0')
		{
			if ( binaryBuffer == null )
				initThread();

			var unicodeBuffer= new Span<char>();


			const int MatchFail= -1;
			int matchProgress= 0;
			var matchKey= key.AsSpan();

			bool valueEntryIsBeingRead= false;

			int bufferSize= 0,
				bufferOffset= 0;

			entryFileOffset= 0;

			Android.Systems.Os.Lseek(@this, 0, OsConstants.SeekSet); // makes sure we start searching from the beginning

			while ( true )
			{
				int indexOfTerminator= unicodeBuffer.IndexOf(terminator);

				if ( indexOfTerminator >= 0 )
				{
					bufferOffset+= indexOfTerminator + 1;

					if (  matchProgress != MatchFail  &&  matchKey.Slice(matchProgress).SequenceEqual( unicodeBuffer.Slice(0, indexOfTerminator) )  )
						break;

					matchProgress= 0; // a new string is about to be read
					if ( valueEntryIsBeingRead= !valueEntryIsBeingRead ) // flips to true for each odd-numbered string
						matchProgress= MatchFail; // prevents matching the key to a value

					unicodeBuffer= unicodeBuffer.Slice( indexOfTerminator + 1 );
				}
				else { // no more characters in the buffer

					if (  matchProgress != MatchFail  &&  matchKey.Slice(matchProgress).StartsWith(unicodeBuffer)  )
						matchProgress+= unicodeBuffer.Length;
					else matchProgress= MatchFail; // prevents matching the key to a value
			
					entryFileOffset+= bufferSize;

					bufferSize= Android.Systems.Os.Read(@this, binaryBuffer, 0, BufferCapacity);
					if ( bufferSize > 1 ) {
						bufferOffset= 0;
						unicodeBuffer= MemoryMarshal.Cast<byte, char>(binaryBuffer).Slice( 0, bufferSize / sizeof(char) );
					}
					else return null; // no match was found
				
				}
			}

			entryFileOffset+= bufferOffset * sizeof(char);

			Android.Systems.Os.Lseek(@this, entryFileOffset, OsConstants.SeekSet);
			return @this.ReadString( resetState: true, terminator );
		}
		

		/// <summary>
		///  Uses various low-level Linux systems call to replace the file's previous dictionary value entry with the new value provided.
		/// </summary>
		public static unsafe void ReplaceDictionaryEntry(this FileDescriptor @this, string newValue, long oldValueFileOffset, int oldValueLength)
		{

			if ( newValue.Length != oldValueLength ) // hoping we won't have to grow/shrink the file
			{
				long oldFileSize= Android.Systems.Os.Fstat(@this).StSize,
				     newFileSize= oldFileSize + ( newValue.Length - oldValueLength ) * sizeof(char);

				if ( newValue.Length > oldValueLength ) // grow file before memmove
					Android.Systems.Os.Ftruncate(@this, newFileSize);

				
				long pageOffset= oldValueFileOffset / Configuration.SystemPageSize * Configuration.SystemPageSize;
				long mapSize= oldFileSize - pageOffset;
				var mapPointer= (char*)
					Android.Systems.Os.Mmap(0, mapSize, OsConstants.ProtRead | OsConstants.ProtWrite, OsConstants.MapShared, @this, pageOffset);

				int pageWaste= (int)( oldValueFileOffset - pageOffset ); // amount of the page in bytes that will be skipped / ignored
				long sizeToMove= mapSize - oldValueLength * sizeof(char) - pageWaste;
				Buffer.MemoryCopy(mapPointer + pageWaste + oldValueLength, mapPointer + pageWaste + newValue.Length, sizeToMove, sizeToMove);

				Android.Systems.Os.Munmap( (long) mapPointer, mapSize );

				if ( oldValueLength < newValue.Length ) // shrink file after memmove
					Android.Systems.Os.Ftruncate(@this, newFileSize);
			}

			newValue.WriteTo(@this, oldValueFileOffset);
			// Android.Systems.Os.Pwrite( @this, newValue.ToByteArray(), 0, newValue.Length * sizeof(char), oldValueFileOffset );
		}



		/// <summary>
		///  Reads a null-terminated string from the given file, unless a custom terminator is provided.
		/// </summary>
		public static string ReadString(this FileDescriptor @this, bool resetState= false, char terminator= '\0')
		{
			#region Prepare The Buffers For Use

				if ( binaryBuffer == null )
					initThread();
				else if ( resetState )
					bufferOffset= bufferEnd= 0;

				stringBuffer.Clear();

				int bufferStart= bufferOffset;
				var unicodeBuffer= MemoryMarshal.Cast<byte, char>(binaryBuffer);

			#endregion


			do {
				if ( bufferOffset == bufferEnd ) { // no more characters in the buffer


					if ( bufferEnd != bufferStart ) // characters have been read from the buffer
					{
						if ( bufferEnd != bufferMaxChars && stringBuffer.Length == 0 ) // avoids string-builder, b/c end-of-file anticipated
							return new string( unicodeBuffer.slice(bufferStart, bufferEnd) ); // copies characters to the string being returned 
						else stringBuffer.Append( unicodeBuffer.slice(bufferStart, bufferEnd) ); // adds the rest of the characters to the string-builder 
					}


					bufferStart= bufferOffset= 0;
					bufferEnd= Android.Systems.Os.Read(@this, binaryBuffer, 0, BufferCapacity) / sizeof(char);

					if ( bufferEnd <= 0 ) // error or end-of-file encountered
					{
						bufferEnd= 0;
						if ( stringBuffer.Length > 0 )
							return stringBuffer.ToString();
						else return null;
					}

				}
			} while ( unicodeBuffer[ bufferOffset++ ] != terminator );


			#region Return String With Characters Until The Terminator

				unicodeBuffer= unicodeBuffer.slice(bufferStart, bufferOffset-1);

				if ( stringBuffer.Length == 0 )
					return new string(unicodeBuffer);
				else return stringBuffer.Append(unicodeBuffer).ToString();

			#endregion
		}


		/// <summary>
		///  Retrieves the length of a terminated string from the given file.
		/// </summary>
		public static int GetStringLength(this FileDescriptor @this, bool resetState= false, char terminator= '\0')
		{
			#region Prepare The Buffers For Use

				if ( binaryBuffer == null )
					initThread();
				else if ( resetState )
					bufferOffset= bufferEnd= 0;

				var unicodeBuffer= MemoryMarshal.Cast<byte, char>(binaryBuffer);

			#endregion

			int L= -bufferOffset;

			do {
				if ( bufferOffset == bufferEnd ) { // no more characters in the buffer
					L+= bufferEnd;

					bufferOffset= 0;
					bufferEnd= Android.Systems.Os.Read(@this, binaryBuffer, 0, BufferCapacity) / sizeof(char);

					if ( bufferEnd <= 0 ) { // error or end-of-file encountered
						if ( L == 0 )
							L= -1; // no string
						return L;
					}
				}
			} while ( unicodeBuffer[ bufferOffset++ ] != terminator );

			return L + bufferOffset - 1;
		}

	}
}