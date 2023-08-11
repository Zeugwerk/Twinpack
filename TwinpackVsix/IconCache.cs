using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace Twinpack
{
    public class IconCache
    {
        private static Dictionary<string, BitmapImage> _icons;

        public static BitmapImage Icon(string iconUrl)
        {
            if (iconUrl == null)
                return null;

            try
            {
                if (_icons == null)
                    _icons = new Dictionary<string, BitmapImage>();

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
