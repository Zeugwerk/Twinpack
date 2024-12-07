using System;
using System.Windows.Data;
using System.Windows;
using System.Globalization;
using Twinpack.Protocol.Api;

namespace Twinpack.Dialogs
{
    public class CatalogIconUrlVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;

            if (value is CatalogItemGetResponse catalogItem)
            {
                return catalogItem.Icon == null && catalogItem.IconUrl != null ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
