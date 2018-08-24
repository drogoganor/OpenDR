#region Copyright & License Information
/*
 * Copyright 2007-2018 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using OpenRA.FileSystem;
using FS = OpenRA.FileSystem.FileSystem;

namespace OpenRA.Mods.Dr.FileSystem
{
	public class FtgFileLoader : IPackageLoader
	{
		struct FtgEntry
		{
			public string Filename { get; set; }
			public int Offset { get; set; }
			public int Size { get; set; }
		}

		sealed class FtgFile : IReadOnlyPackage
		{
			public string Name { get; private set; }
			public IEnumerable<string> Contents { get { return index.Keys; } }

			readonly Dictionary<string, FtgEntry> index = new Dictionary<string, FtgEntry>();
			readonly Stream stream;

			public FtgFile(Stream stream, string filename)
			{
				Name = filename;
				this.stream = stream;

				try
				{
					stream.ReadBytes(4);
					var directoryOffset = BitConverter.ToInt32(stream.ReadBytes(4), 0);
					var fileCount = BitConverter.ToInt32(stream.ReadBytes(4), 0);

					stream.Seek(directoryOffset, SeekOrigin.Begin);
					for (int i = 0; i < fileCount; i++)
					{
						var entryFilename = stream.ReadASCII(28);
						entryFilename = entryFilename.Replace("\0", string.Empty);

						var offset = BitConverter.ToInt32(stream.ReadBytes(4), 0);
						var size = BitConverter.ToInt32(stream.ReadBytes(4), 0);

						// Ignore duplicate files
						if (index.ContainsKey(entryFilename))
							continue;

						var info = new FtgEntry() { Filename = entryFilename, Offset = offset, Size = size };
						index.Add(entryFilename, info);
					}
				}
				catch
				{
					Dispose();
					throw;
				}
			}

			public Stream GetStream(string filename)
			{
				FtgEntry entry;
				if (!index.TryGetValue(filename, out entry))
					return null;

				stream.Seek(entry.Offset, SeekOrigin.Begin);
				var data = stream.ReadBytes((int)entry.Size);
				return new MemoryStream(data);
			}

			public bool Contains(string filename)
			{
				return index.ContainsKey(filename);
			}

			public IReadOnlyPackage OpenPackage(string filename, FS context)
			{
				// Not implemented
				return null;
			}

			public void Dispose()
			{
				stream.Dispose();
			}
		}

		bool IPackageLoader.TryParsePackage(Stream s, string filename, FS context, out IReadOnlyPackage package)
		{
			if (!filename.EndsWith(".ftg", StringComparison.InvariantCultureIgnoreCase))
			{
				package = null;
				return false;
			}

			package = new FtgFile(s, filename);
			return true;
		}
	}
}
