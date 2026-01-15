using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ZeroTrustMigrationAddin.Models;

namespace ZeroTrustMigrationAddin.Converters
{
    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
                return $"{d:F1}%";
            return "0%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PercentageToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is double percentage && values[1] is double totalWidth)
            {
                // Calculate width based on percentage
                return (percentage / 100.0) * totalWidth;
            }
            
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d)
                return $"${d:N0}";
            return "$0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class WorkloadStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WorkloadStatus status)
            {
                if (targetType == typeof(Brush))
                {
                    return status switch
                    {
                        WorkloadStatus.Completed => new SolidColorBrush(Color.FromRgb(16, 124, 16)), // Green
                        WorkloadStatus.InProgress => new SolidColorBrush(Color.FromRgb(255, 185, 0)), // Yellow
                        WorkloadStatus.NotStarted => new SolidColorBrush(Color.FromRgb(128, 128, 128)), // Gray
                        _ => new SolidColorBrush(Colors.Gray)
                    };
                }
                
                if (targetType == typeof(Visibility))
                {
                    if (parameter is string param && param == "NotStarted")
                        return status == WorkloadStatus.NotStarted ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AlertSeverityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AlertSeverity severity)
            {
                return severity switch
                {
                    AlertSeverity.Critical => new SolidColorBrush(Color.FromRgb(255, 230, 230)),
                    AlertSeverity.Warning => new SolidColorBrush(Color.FromRgb(255, 249, 230)),
                    AlertSeverity.Info => new SolidColorBrush(Color.FromRgb(230, 244, 255)),
                    _ => new SolidColorBrush(Colors.LightGray)
                };
            }
            return new SolidColorBrush(Colors.LightGray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BlockerSeverityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is BlockerSeverity severity)
            {
                return severity switch
                {
                    BlockerSeverity.Critical => new SolidColorBrush(Color.FromRgb(209, 52, 56)),
                    BlockerSeverity.High => new SolidColorBrush(Color.FromRgb(255, 140, 0)),
                    BlockerSeverity.Medium => new SolidColorBrush(Color.FromRgb(255, 185, 0)),
                    BlockerSeverity.Low => new SolidColorBrush(Color.FromRgb(128, 128, 128)),
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

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = value is bool b && b;
            
            // Support inverse parameter
            if (parameter is string param && param == "Inverse")
                isVisible = !isVisible;
                
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Show empty state when count is 0
            int count = value as int? ?? 0;
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Show when count is greater than 0 (inverse of CountToVisibilityConverter)
            int count = value as int? ?? 0;
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ObjectToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Show if object is not null
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Show if object is NOT null (standard behavior)
            bool isNotNull = value != null;
            
            // Support inverse parameter (show if null)
            if (parameter is string param && param == "Inverse")
                isNotNull = !isNotNull;
                
            return isNotNull ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CollectionToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool hasItems = false;

            if (value is IEnumerable collection)
            {
                // Check if collection has any items
                hasItems = collection.Cast<object>().Any();
            }

            // Support inverse parameter (show when empty)
            if (parameter is string param && param == "Inverse")
                hasItems = !hasItems;

            return hasItems ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PercentageToStrokeDashConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is double percentage)
            {
                // Circle circumference formula: π * diameter
                // For a 180px diameter circle: ~565.49
                double diameter = 180.0;
                double radius = diameter / 2.0;
                double circumference = 2 * Math.PI * radius;
                
                // Calculate stroke lengths
                double fillLength = (percentage / 100.0) * circumference;
                double gapLength = circumference - fillLength;
                
                return new System.Windows.Media.DoubleCollection { fillLength, gapLength };
            }
            
            return new System.Windows.Media.DoubleCollection { 0, 565.49 };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Inverts a boolean value. Used for button IsEnabled when agent is running.
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return false;
        }
    }

    /// <summary>
    /// Converts Risk Level string to color for Workloads tab
    /// </summary>
    public class RiskLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string riskLevel)
            {
                return riskLevel.ToLower() switch
                {
                    "low" => new SolidColorBrush(Color.FromRgb(16, 124, 16)), // Green
                    "medium" => new SolidColorBrush(Color.FromRgb(255, 185, 0)), // Yellow
                    "high" => new SolidColorBrush(Color.FromRgb(255, 87, 34)), // Orange
                    "critical" => new SolidColorBrush(Color.FromRgb(209, 52, 56)), // Red
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts Risk Level to background color for badges
    /// </summary>
    public class RiskLevelToBgColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string riskLevel)
            {
                return riskLevel.ToLower() switch
                {
                    "low" => new SolidColorBrush(Color.FromRgb(232, 245, 233)), // Light Green
                    "medium" => new SolidColorBrush(Color.FromRgb(255, 249, 230)), // Light Yellow
                    "high" => new SolidColorBrush(Color.FromRgb(255, 243, 224)), // Light Orange
                    "critical" => new SolidColorBrush(Color.FromRgb(255, 230, 230)), // Light Red
                    _ => new SolidColorBrush(Colors.LightGray)
                };
            }
            return Brushes.LightGray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts readiness score (0-100) to color
    /// </summary>
    public class ReadinessScoreToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double score)
            {
                if (score >= 80)
                    return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                else if (score >= 60)
                    return new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Yellow
                else if (score >= 40)
                    return new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
                else
                    return new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts WorkloadStatus to color for dependency visualization
    /// </summary>
    public class WorkloadStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WorkloadStatus status)
            {
                return status switch
                {
                    WorkloadStatus.Completed => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                    WorkloadStatus.InProgress => new SolidColorBrush(Color.FromRgb(33, 150, 243)), // Blue
                    WorkloadStatus.NotStarted => new SolidColorBrush(Color.FromRgb(224, 224, 224)), // Light Gray
                    _ => Brushes.Gray
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
