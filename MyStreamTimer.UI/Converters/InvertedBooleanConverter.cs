using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Xamarin.Forms;

namespace MyStreamTimer.UI.Converters
{
    /// <summary>
    /// Inverted boolen converter.
    /// </summary>
    public class InvertedBooleanConverter : IValueConverter
    {
        /// <param name="value">To be added.</param>
        /// <param name="targetType">To be added.</param>
        /// <param name="parameter">To be added.</param>
        /// <param name="culture">To be added.</param>
        /// <summary>
        /// Convert the specified value, targetType, parameter and culture.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolean)
                return !boolean;

            return value;
        }

        /// <param name="value">To be added.</param>
        /// <param name="targetType">To be added.</param>
        /// <param name="parameter">To be added.</param>
        /// <param name="culture">To be added.</param>
        /// <summary>
        /// Converts the back.
        /// </summary>
        /// <returns>The back.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value is bool boolean)
                return !boolean;

            return value;
        }
    }
}
