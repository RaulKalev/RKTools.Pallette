using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace Palette.UI
{
    /// <summary>true → Collapsed, false → Visible.</summary>
    public sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool && (bool)value ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility && (Visibility)value != Visibility.Visible;
    }

    /// <summary>Non-null (and non-empty string) → Visible.</summary>
    public sealed class NotEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var visible = value != null && !(value is string && string.IsNullOrWhiteSpace((string)value));
            if (parameter as string == "invert") visible = !visible;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Inverts a bool.</summary>
    public sealed class NotConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !(value is bool && (bool)value);
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !(value is bool && (bool)value);
    }

    /// <summary>
    /// Material Design icon name (string) → <see cref="PackIconKind"/>. Parsed once per name and cached, so a list of
    /// results never re-parses enum names; unknown or missing names fall back to a neutral icon.
    /// </summary>
    public sealed class IconKindConverter : IValueConverter
    {
        private static readonly Dictionary<string, PackIconKind> Cache = new Dictionary<string, PackIconKind>(StringComparer.OrdinalIgnoreCase);

        public const PackIconKind Fallback = PackIconKind.ChevronRightCircleOutline;

        public static PackIconKind Resolve(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Fallback;
            lock (Cache)
            {
                PackIconKind kind;
                if (!Cache.TryGetValue(name, out kind))
                {
                    if (!Enum.TryParse(name, true, out kind)) kind = Fallback;
                    Cache[name] = kind;
                }
                return kind;
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Resolve(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
