using System;
using System.Windows.Data;
using System.Windows;
using System.Globalization;
using Twinpack.Protocol.Api;

namespace Twinpack.Dialogs
{
    public class CatalogIconVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;

            if (value is CatalogItemGetResponse catalogItem)
            {
                return catalogItem.Icon == null ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
