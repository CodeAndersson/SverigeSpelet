using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SverigeSpelet
{
    public class BoolToEmojiConverter : IValueConverter
    {
        public static BoolToEmojiConverter Instance { get; } = new BoolToEmojiConverter();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isCorrect)
            {
                return isCorrect ? "✅" : "❌";
            }
            return "❓";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
