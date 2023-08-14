using Jdenticon.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace Twinpack
{
    public class IconCache
    {
        private static Dictionary<string, BitmapImage> _icons;

        public static byte[] GenerateIdenticon(string packageName)
        {
            var size = 128;
            var renderer = new PngRenderer(size, size);
            var icon = Jdenticon.Identicon.FromValue(packageName, size);
            
            icon.Style = new IdenticonStyle 
            {
                Hues = new HueCollection { { 6, HueUnit.Degrees } },
                BackColor = Color.FromRgba(42, 51, 75, 255),
                ColorLightness = Range.Create(0.64f, 0.64f),
                GrayscaleLightness = Range.Create(1.00f, 1.00f),
                ColorSaturation = 0.60f,
                GrayscaleSaturation = 0.0f
            };
            
            icon.Draw(renderer);

            using (var stream = new MemoryStream())
            {
                renderer.SavePng(stream);
                return stream.ToArray();
            }
        }

        public static BitmapImage GenerateIdenticonAsBitmapImage(string packageName)
        {
            var image = new BitmapImage();

            using (var stream = new MemoryStream(GenerateIdenticon(packageName)))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
            }

            return image;
        }

        public static BitmapImage Icon(string iconUrl)
        {
            if (_icons == null)
                _icons = new Dictionary<string, BitmapImage>();

            if (iconUrl?.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase) == false)
            {
                var img = GenerateIdenticonAsBitmapImage(iconUrl);
                return img;
            }

            if (_icons == null)
                _icons = new Dictionary<string, BitmapImage>();

            try
            {
                if (_icons.ContainsKey(iconUrl))
                    return _icons[iconUrl];

                BitmapImage img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource = new Uri(iconUrl, UriKind.Absolute);
                img.EndInit();

                _icons.Add(iconUrl, img);
                return img;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
