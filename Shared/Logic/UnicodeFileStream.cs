using System;
using System.Text;
using Java.IO;
using Android.Systems;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace StorageHistory.Shared.Logic
{

	/// <summary>
	///  A lightweight class for reading unicode strings from a file.
	/// </summary>
	public class UnicodeFileStream: IDisposable
	{
		public const int BufferCapacity= 8192;
		private const int BufferMaxChars= BufferCapacity / sizeof(char);

		[ThreadStatic] private static byte[] BinaryBuffer; // ensures thread-safety for each buffer
		[ThreadStatic] private static StringBuilder StringBuffer;
		[ThreadStatic] private static int InstanceCount;
		[ThreadStatic] private static UnicodeFileStream Default;

		private FileDescriptor file;
		private int bufferOffset;
		private int bufferEnd;
		private byte[] binaryBuffer;
		private StringBuilder stringBuffer;
		private bool autoClose;

		public UnicodeFileStream(string filePath, int openFlags, int openMode)
			: this( Os.Open(filePath, openFlags, openMode), autoClose: true ) {}

		public UnicodeFileStream(FileDescriptor file, bool autoClose= false)
		{
			this.file= file;
			this.autoClose= autoClose;
			if ( ++InstanceCount == 1 && BinaryBuffer != null )
			{
				binaryBuffer= BinaryBuffer; // share buffers to minimize memory churn and memory usage
				stringBuffer= StringBuffer;
			}
			else {
				BinaryBuffer= binaryBuffer= new byte [ BufferCapacity ];
				StringBuffer= stringBuffer= new StringBuilder(BufferMaxChars);
			}
		}

		/// <summary>
		///  Reads the next null-terminated string (or a string with the specified terminator).
		/// </summary>
		public string ReadString(char terminator= '\0')
			=> ReadCharacters(terminator).ToString();

		/// <summary>
		///  Reads the next null-terminated sequence of characters (or a span with the specified terminator).
		/// </summary>
		public Characters ReadCharacters(char terminator= '\0')
		{
			stringBuffer.Clear();

			int bufferStart= bufferOffset;
			var unicodeBuffer= MemoryMarshal.Cast<byte, char>(binaryBuffer);

			do {
				if ( bufferOffset == bufferEnd ) // no more characters in the buffer
				{

					if ( bufferEnd != bufferStart ) // characters have been read from the buffer
					{
						if ( bufferEnd < BufferMaxChars && stringBuffer.Length == 0 ) // b/c we got less than we asked for, we can assume end-of-file and avoid string-builder
							return unicodeBuffer.slice(bufferStart, bufferEnd); // copies characters to the string being returned 
						else stringBuffer.Append( unicodeBuffer.slice(bufferStart, bufferEnd) ); // adds the rest of the characters to the string-builder 
					}


					bufferStart= bufferOffset= 0;
					bufferEnd= Os.Read(file, binaryBuffer, 0, BufferCapacity) / sizeof(char);

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
					return unicodeBuffer;
				else return stringBuffer.Append(unicodeBuffer).ToString();

			#endregion
		}

		/// <summary>
		///  Retrieves the length of the next terminated string in the file (by reading over it sequentially).
		/// </summary>
		/// <returns>
		///  -1 if there is no next string.
		/// </returns>
		public int ReadLengthOfString(char terminator= '\0')
		{
			var unicodeBuffer= MemoryMarshal.Cast<byte, char>(binaryBuffer);

			int L= -bufferOffset;
			do {
				if ( bufferOffset == bufferEnd ) // no more characters in the buffer
				{
					L+= bufferEnd;

					bufferOffset= 0;
					bufferEnd= Os.Read(file, binaryBuffer, 0, BufferCapacity) / sizeof(char);

					if ( bufferEnd <= 0 ) { // error or end-of-file encountered
						if ( L == 0 )
							L= -1; // no string
						return L;
					}
				}
			} while ( unicodeBuffer[ bufferOffset++ ] != terminator );

			return L + bufferOffset - 1;
		}

		/// <summary>
		///  Reads the value of the first matching key-value pair of terminated strings.
		/// </summary>
		public Characters ReadDictionaryEntry(string key, out long entryFileOffset, char terminator= '\0')
		{
			var unicodeBuffer= new Span<char>();


			const int MatchFail= -1;
			int matchProgress= 0;
			var matchKey= key.AsSpan();

			bool valueEntryIsBeingRead= false;

			int bufferSize= 0,
				bufferOffset= 0;

			entryFileOffset= 0;

			Os.Lseek(file, 0, OsConstants.SeekSet); // makes sure we start searching from the beginning

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

					bufferSize= Os.Read(file, binaryBuffer, 0, BufferCapacity);
					if ( bufferSize > 1 ) {
						bufferOffset= 0;
						unicodeBuffer= MemoryMarshal.Cast<byte, char>(binaryBuffer).Slice( 0, bufferSize / sizeof(char) );
					}
					else return null; // no match was found
				
				}
			}

			entryFileOffset+= bufferOffset * sizeof(char);
			this.bufferOffset= bufferOffset;
			this.bufferEnd= unicodeBuffer.Length;
			return ReadCharacters(terminator);
		}
		

		/// <summary>
		///  Uses various low-level Linux systems call to replace the file's previous dictionary value entry with the new value provided.
		/// </summary>
		public unsafe void ReplaceDictionaryEntry(string newValue, long oldValueFileOffset, int oldValueLength)
		{
			int newValueLength= newValue.Length;

			if ( newValueLength != oldValueLength ) // hoping we won't have to grow/shrink the file
			{
				// Convert lengths to number of bytes
				newValueLength*= sizeof(char);
				oldValueLength*= sizeof(char);

				// Get info needed for growing/shrinking the given file-offset range
				long oldFileSize= Os.Fstat(file).StSize,
				     newFileSize= oldFileSize + newValueLength - oldValueLength,
				     pageOffset= oldValueFileOffset / Configuration.SystemPageSize * Configuration.SystemPageSize,
					 mapSize= oldFileSize - pageOffset;

				if ( newValueLength > oldValueLength ) // grow file before mem-move
				{
					Os.Ftruncate(file, newFileSize);
					mapSize= newFileSize - pageOffset;  // the size of mapped memory should include the file's extended size
				}

				// Retrieve a memory-mapped view of the given file
				long mapAddress= Os.Mmap(0, mapSize, OsConstants.ProtRead | OsConstants.ProtWrite, OsConstants.MapShared, file, pageOffset);

					// Move file contents after the old value to where the new value will end
					var oldValuePointer= (byte*)mapAddress + ( oldValueFileOffset - pageOffset ); // pointer to the old value in mapped memory
					long sizeToMove= oldFileSize - ( oldValueFileOffset + oldValueLength );
					Buffer.MemoryCopy(  oldValuePointer + oldValueLength,  oldValuePointer + newValueLength,  sizeToMove,  sizeToMove  );
					
					// Copy the new value over the old value
					fixed ( char* newValuePointer= newValue )
						Buffer.MemoryCopy(  newValuePointer,  oldValuePointer,  newValueLength + sizeof(char),  newValueLength + sizeof(char)  );

				// Release mapped memory
				Os.Munmap( mapAddress, mapSize );

				if ( oldValueLength < newValueLength ) // shrink file after mem-move
					Os.Ftruncate(file, newFileSize);
			}
			else WriteString(newValue, oldValueFileOffset);
		}

		/// <summary>
		///  Writes the given string, terminating it with the null character or a given custom character.
		/// </summary>
		public void WriteString(string str, char terminator= '\0')
		{
			int L0= str.Length,
			    L;

			for ( L= L0; L >= BufferMaxChars; L-= BufferMaxChars ) // while string is bigger than buffer
			{
				str.CopyTo(L0-L, Unsafe.As<char[]>(binaryBuffer), 0, BufferMaxChars); // copy & write a block at a time
				Os.Write(file, binaryBuffer, 0, BufferCapacity);
			}

			str.CopyTo(L0-L, Unsafe.As<char[]>(binaryBuffer), 0, L);
			Unsafe.As<char[]>( binaryBuffer )[L]= terminator;
			Os.Write(file, binaryBuffer, 0, ( L + 1 ) * sizeof(char) );
		}


		/// <summary>
		///  Writes the given string at the given offset (without `lseek`).
		/// </summary>
		public void WriteString(string str, long fileOffset, char terminator= '\0')
		{
			int L0= str.Length,
			    L;

			for ( L= L0; L >= BufferMaxChars; L-= BufferMaxChars ) // while string is bigger than buffer
			{
				str.CopyTo(L0-L, Unsafe.As<char[]>(binaryBuffer), 0, BufferMaxChars); // copy & write a block at a time
				Os.Pwrite(file, binaryBuffer, 0, BufferCapacity, fileOffset);
				fileOffset+= BufferCapacity;
			}

			str.CopyTo(L0-L, Unsafe.As<char[]>(binaryBuffer), 0, L);
			Unsafe.As<char[]>( binaryBuffer )[L]= terminator;
			Os.Pwrite(file, binaryBuffer, 0, ( L + 1 ) * sizeof(char), fileOffset );
		}


		/// <summary>
		///  Writes a single character to the file.
		/// </summary>
		public void WriteCharacter(char c)
		{
			Unsafe.As<char[]>( binaryBuffer )[0]= c;
			Os.Write(file, binaryBuffer, 0, sizeof(char));
		}

		public void Truncate()
			=> Os.Ftruncate( file, Os.Lseek(file, 0, OsConstants.SeekCur) );

		public void Dispose()
		{
			if ( file != null )
				try {
					if ( this != Default )
						--InstanceCount;
					if ( autoClose )
						Os.Close(file);
				}
				finally {
					file= null;
				}
		}

		~UnicodeFileStream() =>	Dispose();

		public static implicit operator UnicodeFileStream(FileDescriptor file)
			=> GetDefault(file);

		private static UnicodeFileStream GetDefault(FileDescriptor file)
		{
			if ( Default == null )
			{
				Default= new UnicodeFileStream(file);
				--InstanceCount;
			}
			else {
				Default.bufferOffset= Default.bufferEnd= 0;
				Default.file= file;
			}
			return Default;
		}

		public static string ReadString(FileDescriptor file, char terminator= '\0')
			=> GetDefault(file).ReadString(terminator);

		/// <summary>
		///  Causes the current thread's unnecessary I/O buffers to be released.
		/// </summary>
		/// <remarks>
		///  Only appropriate for the main thread or the file observer thread
		///  (the only threads that persist long term).
		/// </remarks>
		public static void TrimMemory()
		{
			Default= null;
			BinaryBuffer= null;
			StringBuffer= null;
		}

	}
}