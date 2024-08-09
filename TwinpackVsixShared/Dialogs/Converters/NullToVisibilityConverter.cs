using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;
using System.Globalization;

namespace Twinpack.Dialogs
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;

            try
            {
                return (int)value != 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch
            {

            }

            try
            {
                if(value as Models.Api.PackageVersionGetResponse != null)
                    return (value as Models.Api.PackageVersionGetResponse).Name != null ? Visibility.Visible : Visibility.Collapsed;

                if (value as Models.Api.PackageGetResponse != null)
                    return (value as Models.Api.PackageGetResponse).Name != null ? Visibility.Visible : Visibility.Collapsed;
            }
            catch
            {

            }

            if (string.IsNullOrEmpty(value as string))
                return Visibility.Collapsed;

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
