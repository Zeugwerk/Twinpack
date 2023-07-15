using System;
using System.Windows.Data;
using System.Globalization;
using System.Net;
using System.Windows.Media.Imaging;
using System.IO;

namespace Twinpack.Dialogs
{
    public class UrlToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string imageUrl)
            {
                // Check if the URL is valid
                if (Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    try
                    {
                        // Download the image from the URL
                        WebClient client = new WebClient();
                        byte[] imageData = client.DownloadData(uri);
                        client.Dispose();

                        // Create a BitmapImage from the downloaded image data
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = new MemoryStream(imageData);
                        bitmap.EndInit();

                        return bitmap;
                    }
                    catch (Exception) { return new BitmapImage(); }
                }
            }

            // Return null or a default image if the URL is invalid or an error occurred
            return "Images/Twinpack.png";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

