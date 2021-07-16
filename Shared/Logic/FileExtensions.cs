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

		/// <summary>
		///  Gets a hashed collection from a file with strings seperated by the null-character.
		/// </summary>
		public static HashSet<string> GetStrings(this FileDescriptor @this)
		{
			var stringSet= new HashSet<string>();
			UnicodeFileStream file= @this;

			string newString;
			while (  ( newString= file.ReadString() )  !=  null  )
				stringSet.Add(newString);

			return stringSet;
		}

		/// <summary>
		///  Writes the collection to a file as strings seperated by the null-character.
		/// </summary>
		public static void WriteTo(this HashSet<string> @this, UnicodeFileStream file, bool truncate= true)
		{
			foreach ( string item in @this )
				file.WriteString(item);
			
			if ( truncate )
				file.Truncate();
		}

		/// <summary>
		///  Writes the string to the given file, terminating it with the null character or the given custom terminator.
		/// </summary>
		public static void WriteTo(this string @this, UnicodeFileStream file, char terminator= '\0')
			=> file.WriteString(@this, terminator);

		/// <summary>
		///  Writes the object/value as a string to the given file, terminating it with the null character or the given custom terminator.
		/// </summary>
		public static void WriteTo<T>(this T @this, UnicodeFileStream file, char terminator= '\0')
			=> file.WriteString(@this.ToString(), terminator);

		/// <summary>
		///  Writes the string to the given file at the given offset (without `lseek`).
		/// </summary>
		public static void WriteTo(this string @this, UnicodeFileStream file, long fileOffset, char terminator= '\0')
			=> file.WriteString(@this, fileOffset, terminator);

		/// <summary>
		///  Writes the object/value to the given file at the given offset (without `lseek`).
		/// </summary>
		public static void WriteTo<T>(this T @this, UnicodeFileStream file, long fileOffset, char terminator= '\0')
			=> file.WriteString(@this.ToString(), fileOffset, terminator);

		/// <summary>
		///  Writes a single character to the given file.
		/// </summary>
		public static void WriteTo(this char @this, UnicodeFileStream file)
			=> file.WriteCharacter(@this);

		/// <summary>
		///  Reads the value of key-value pair of null-terminated strings from the given file.
		/// </summary>
		public static Characters ReadDictionaryEntry(this FileDescriptor @this, string key, out long entryFileOffset)
			=> ( (UnicodeFileStream)@this ).ReadDictionaryEntry(key, out entryFileOffset);

		/// <summary>
		///  Uses various low-level Linux systems call to replace the file's previous dictionary value entry with the new value provided.
		/// </summary>
		public static void ReplaceDictionaryEntry(this FileDescriptor @this, string newValue, long oldValueFileOffset, int oldValueLength)
			=> ( (UnicodeFileStream)@this ).ReplaceDictionaryEntry(newValue, oldValueFileOffset, oldValueLength);

	}
}