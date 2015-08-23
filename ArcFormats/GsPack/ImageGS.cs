//! \file       ImageGS.cs
//! \date       Thu Apr 16 16:03:19 2015
//! \brief      GsPack image format implementation.
//
// Copyright (C) 2015 by morkt
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameRes.Compression;
using GameRes.Utility;

namespace GameRes.Formats.Gs
{
    internal class PicMetaData : ImageMetaData
    {
        public uint PackedSize;
        public uint UnpackedSize;
        public uint HeaderSize;
    }

    [Export(typeof(ImageFormat))]
    public class PicFormat : ImageFormat
    {
        public override string         Tag { get { return "GsPIC"; } }
        public override string Description { get { return "GsPack image format"; } }
        public override uint     Signature { get { return 0x00040000; } }

        public PicFormat ()
        {
            Extensions = new string[] { "pic" }; // made-up
        }

        public override ImageMetaData ReadMetaData (Stream stream)
        {
            using (var input = new ArcView.Reader (stream))
            {
                var info = new PicMetaData();
                input.ReadUInt32();
                info.PackedSize = input.ReadUInt32();
                info.UnpackedSize = input.ReadUInt32();
                info.HeaderSize = input.ReadUInt32();
                if (info.HeaderSize >= stream.Length || info.PackedSize + info.HeaderSize > stream.Length)
                    return null;
                input.ReadUInt32();
                info.Width = input.ReadUInt32();
                info.Height = input.ReadUInt32();
                info.BPP = input.ReadInt32();
                return info;
            }
        }

        public override ImageData Read (Stream stream, ImageMetaData info)
        {
            var meta = info as PicMetaData;
            if (null == meta)
                throw new ArgumentException ("PicFormat.Read should be supplied with PicMetaData", "info");

            stream.Position = meta.HeaderSize;
            using (var input = new LzssStream (stream, LzssMode.Decompress, true))
            {
                BitmapPalette palette = null;
                PixelFormat format;
                if (8 == meta.BPP) // read palette
                {
                    format = PixelFormats.Indexed8;
                    palette = ReadPalette (input);
                }
                else if (24 == meta.BPP)
                    format = PixelFormats.Bgr24;
                else if (16 == meta.BPP)
                    format = PixelFormats.Bgr565;
                else
                    format = PixelFormats.Bgr32;

                int stride = (int)meta.Width*((info.BPP+7)/8);
                var pixels = new byte[stride*meta.Height];
                input.Read (pixels, 0, pixels.Length);
                if (32 == meta.BPP)
                {
                    for (int i = 3; i < pixels.Length; i += 4)
                    {
                        if (0 != pixels[i])
                        {
                            format = PixelFormats.Bgra32;
                            break;
                        }
                    }
                }
                return ImageData.Create (meta, format, palette, pixels);
            }
        }

        public override void Write (Stream file, ImageData image)
        {
            throw new NotImplementedException ("PicFormat.Write not implemented");
        }

        private static BitmapPalette ReadPalette (Stream input)
        {
            var colors = new byte[0x400];
            if (colors.Length != input.Read (colors, 0, colors.Length))
                throw new InvalidFormatException();
            var color_data = new Color[0x100];
            int n = 0;
            for (int i = 0; i < 0x400; i += 4)
                color_data[n++] = Color.FromRgb (colors[i+2], colors[i+1], colors[i]);
            return new BitmapPalette (color_data);
        }
    }
}