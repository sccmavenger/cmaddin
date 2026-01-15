using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ZeroTrustMigrationAddin.Services;

namespace ZeroTrustMigrationAddin.Converters
{
    public class RecommendationPriorityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RecommendationPriority priority)
            {
                return priority switch
                {
                    RecommendationPriority.Critical => new SolidColorBrush(Color.FromRgb(220, 53, 69)),   // Red
                    RecommendationPriority.High => new SolidColorBrush(Color.FromRgb(255, 193, 7)),       // Yellow
                    RecommendationPriority.Medium => new SolidColorBrush(Color.FromRgb(0, 123, 255)),     // Blue
                    RecommendationPriority.Low => new SolidColorBrush(Color.FromRgb(108, 117, 125)),      // Gray
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }

            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
