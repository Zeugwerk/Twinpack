using Jdenticon.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Twinpack
{
    public class IconUtils
    {
        public static byte[] GenerateIdenticon(string packageName)
        {
            var size = 128;
            var renderer = new PngRenderer(size, size);
            var icon = Jdenticon.Identicon.FromValue(packageName, size);

            icon.Style = new Jdenticon.IdenticonStyle
            {
                Hues = new HueCollection { { 116, HueUnit.Degrees } },
                BackColor = Color.Transparent,
                ColorLightness = Jdenticon.Range.Create(0.37f, 0.37f),
                GrayscaleLightness = Jdenticon.Range.Create(0.37f, 0.37f),
                ColorSaturation = 0.26f,
                GrayscaleSaturation = 0.26f
            };

            icon.Draw(renderer);

            using (var stream = new MemoryStream())
            {
                renderer.SavePng(stream);
                return stream.ToArray();
            }
        }
    }
}
