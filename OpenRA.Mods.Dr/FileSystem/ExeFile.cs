#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
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
using System.Linq;
using ICSharpCode.SharpZipLib.Zip;
using OpenRA.FileSystem;
using FS = OpenRA.FileSystem.FileSystem;

namespace OpenRA.Mods.Dr.FileSystem
{
	public class ExeFileLoader : IPackageLoader
	{
		const uint ZipSignature = 9460301;

		public class ReadOnlyExeFile : IReadOnlyPackage
		{
			public string Name { get; protected set; }
			protected ZipFile pkg;

			// Dummy constructor for use with ReadWriteZipFile
			protected ReadOnlyExeFile() { }

			public ReadOnlyExeFile(Stream s, string filename)
			{
				Name = filename;
				pkg = new ZipFile(s);
			}

			public Stream GetStream(string filename)
			{
				var entry = pkg.GetEntry(filename);
				if (entry == null)
					return null;

				using (var z = pkg.GetInputStream(entry))
				{
					var ms = new MemoryStream((int)entry.Size);
					z.CopyTo(ms);
					ms.Seek(0, SeekOrigin.Begin);
					return ms;
				}
			}

			public IEnumerable<string> Contents
			{
				get
				{
					foreach (ZipEntry entry in pkg)
						if (entry.IsFile)
							yield return entry.Name;
				}
			}

			public bool Contains(string filename)
			{
				return pkg.GetEntry(filename) != null;
			}

			public void Dispose()
			{
				pkg?.Close();
				GC.SuppressFinalize(this);
			}

			public IReadOnlyPackage OpenPackage(string filename, FS context)
			{
				// Directories are stored with a trailing "/" in the index
				var entry = pkg.GetEntry(filename) ?? pkg.GetEntry(filename + "/");
				if (entry == null)
					return null;

				if (entry.IsDirectory)
					return new ZipFolder(this, filename);

				// Other package types can be loaded normally
				var s = GetStream(filename);
				if (s == null)
					return null;

				if (context.TryParsePackage(s, filename, out var package))
					return package;

				s.Dispose();
				return null;
			}
		}

		sealed class ZipFolder : IReadOnlyPackage
		{
			public string Name { get; }
			public ReadOnlyExeFile Parent { get; }

			public ZipFolder(ReadOnlyExeFile parent, string path)
			{
				if (path.EndsWith('/'))
					path = path[..^1];

				Name = path;
				Parent = parent;
			}

			public Stream GetStream(string filename)
			{
				// Zip files use '/' as a path separator
				return Parent.GetStream(Name + '/' + filename);
			}

			public IEnumerable<string> Contents
			{
				get
				{
					foreach (var entry in Parent.Contents)
					{
						if (entry.StartsWith(Name, StringComparison.Ordinal) && entry != Name)
						{
							var filename = entry[(Name.Length + 1)..];
							var dirLevels = filename.Split('/').Count(c => !string.IsNullOrEmpty(c));
							if (dirLevels == 1)
								yield return filename;
						}
					}
				}
			}

			public bool Contains(string filename)
			{
				return Parent.Contains(Name + '/' + filename);
			}

			public IReadOnlyPackage OpenPackage(string filename, FS context)
			{
				return Parent.OpenPackage(Name + '/' + filename, context);
			}

			public void Dispose() { /* nothing to do */ }
		}

		sealed class StaticStreamDataSource : IStaticDataSource
		{
			readonly Stream s;
			public StaticStreamDataSource(Stream s)
			{
				this.s = s;
			}

			public Stream GetSource()
			{
				return s;
			}
		}

		public bool TryParsePackage(Stream s, string filename, FS context, out IReadOnlyPackage package)
		{
			var readSignature = s.ReadUInt32();
			s.Position -= 4;

			if (readSignature != ZipSignature)
			{
				package = null;
				return false;
			}

			package = new ReadOnlyExeFile(s, filename);
			return true;
		}
	}
}
