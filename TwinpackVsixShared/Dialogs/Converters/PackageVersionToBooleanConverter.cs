using System;
using System.Windows.Data;
using System.Windows;
using System.Globalization;
using Twinpack.Models;

namespace Twinpack.Dialogs
{
    public class PackageVersionToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as PackageVersionGetResponse)?.Name != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
