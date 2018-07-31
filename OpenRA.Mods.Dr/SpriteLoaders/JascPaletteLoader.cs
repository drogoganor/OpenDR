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
using System.Drawing;
using System.IO;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.SpriteLoaders
{
	class JascPaletteLoader : IPaletteLoader
	{
		public JascPaletteLoader()
		{
		}

        public ImmutablePalette ReadPalette(Stream s, int[] remap)
        {
            var colors = new uint[Palette.Size];
            using (var lines = s.ReadAllLines().GetEnumerator())
            {
                if (lines == null)
                    return null;

                if (!lines.MoveNext() || (lines.Current != "GIMP Palette" && lines.Current != "JASC-PAL"))
                    throw new InvalidDataException("File is not a valid GIMP or JASC palette.");

                byte r, g, b, a;
                a = 255;
                var i = 0;

                while (lines.MoveNext() && i < Palette.Size)
                {
                    // Skip until first color. Ignore # comments, Name/Columns and blank lines as well as JASC header values.
                    if (string.IsNullOrEmpty(lines.Current) || !char.IsDigit(lines.Current.Trim()[0]) || lines.Current == "0100" || lines.Current == "256")
                        continue;

                    var rgba = lines.Current.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (rgba.Length < 3)
                        throw new InvalidDataException("Invalid RGB(A) triplet/quartet: ({0})".F(string.Join(" ", rgba)));

                    if (!byte.TryParse(rgba[0], out r))
                        throw new InvalidDataException("Invalid R value: {0}".F(rgba[0]));

                    if (!byte.TryParse(rgba[1], out g))
                        throw new InvalidDataException("Invalid G value: {0}".F(rgba[1]));

                    if (!byte.TryParse(rgba[2], out b))
                        throw new InvalidDataException("Invalid B value: {0}".F(rgba[2]));

                    // Check if color has a (valid) alpha value.
                    // Note: We can't throw on "rgba.Length > 3 but parse failed", because in GIMP palettes the 'invalid' value is probably a color name string.
                    var noAlpha = rgba.Length > 3 ? !byte.TryParse(rgba[3], out a) : true;

                    // Index 0 should always be completely transparent/background color
                    if (i == 0)
                        colors[i] = 0;
                    else if (noAlpha)
                        colors[i] = (uint)Color.FromArgb(r, g, b).ToArgb();
                    else
                        colors[i] = (uint)Color.FromArgb(a, r, g, b).ToArgb();

                    i++;
                }
            }

            return new ImmutablePalette(colors);
        }
	}
}
