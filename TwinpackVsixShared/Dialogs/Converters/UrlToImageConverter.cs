﻿using System;
using System.Windows.Data;
using System.Globalization;

namespace Twinpack.Dialogs
{
    public class UrlToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return IconCache.Icon((string)value, parameter != null && (parameter as string).Contains("Beckhoff"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

