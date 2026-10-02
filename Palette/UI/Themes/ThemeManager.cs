using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Palette.Core;

namespace Palette.UI
{
    /// <summary>
    /// Applies the appearance to a window: Dark/Light palette (following Revit's UI theme), plus the Windows
    /// accessibility preferences
    ///  - High contrast       → palette built from the system high-contrast colours,
    ///  - Transparency off    → solid materials (no gradients/shadows).
    /// Only the palette dictionaries are swapped; shared styles stay merged. Palette dictionaries are loaded once per
    /// theme and shared by every window, so re-applying on each open is cheap.
    /// </summary>
    public sealed class ThemeManager
    {
        /// <summary>Tells the manager whether Revit currently shows its dark theme (set by the application at startup).</summary>
        public static Func<bool> DarkModeProvider { get; set; } = () => true;

        private static readonly Dictionary<bool, ResourceDictionary> PaletteCache = new Dictionary<bool, ResourceDictionary>();

        private readonly Window _window;
        private ResourceDictionary[] _applied = new ResourceDictionary[0];
        private string _appliedKey;

        public ThemeManager(Window window)
        {
            _window = window;
        }

        public static bool HighContrast => SystemParameters.HighContrast;

        /// <summary>Windows "Transparency effects" (Settings → Personalization → Colors) switched off.</summary>
        public static bool SolidMaterials
        {
            get
            {
                if (SystemParameters.HighContrast) return true;
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    {
                        var v = key?.GetValue("EnableTransparency");
                        return v is int && (int)v == 0;
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>Re-applies the appearance only when Revit's theme or the Windows accessibility settings changed.</summary>
        public void ApplyIfChanged()
        {
            var dark = SafeDark();
            var key = (HighContrast ? "hc" : dark ? "dark" : "light") + (SolidMaterials ? "+solid" : "");
            if (key == _appliedKey) return;
            try
            {
                var target = _window.Resources;
                foreach (var d in _applied) target.MergedDictionaries.Remove(d);
                // Palette dictionaries merged from XAML at parse time (design-time default).
                foreach (var d in target.MergedDictionaries.Where(IsPaletteSource).ToList()) target.MergedDictionaries.Remove(d);

                _applied = BuildDictionaries(dark);
                for (int i = 0; i < _applied.Length; i++) target.MergedDictionaries.Insert(i, _applied[i]);
                _appliedKey = key;
            }
            catch (Exception ex)
            {
                PaletteLog.Error("Applying theme failed", ex);
            }
        }

        private static bool SafeDark()
        {
            try { return DarkModeProvider(); }
            catch { return true; }
        }

        private static bool IsPaletteSource(ResourceDictionary d)
        {
            var s = d.Source?.OriginalString;
            return s != null && s.Contains("/UI/Themes/") && (s.EndsWith("DarkTheme.xaml") || s.EndsWith("LightTheme.xaml"));
        }

        private static ResourceDictionary[] BuildDictionaries(bool dark)
        {
            if (HighContrast) return new[] { BuildHighContrast() };

            ResourceDictionary palette;
            if (!PaletteCache.TryGetValue(dark, out palette))
            {
                palette = new ResourceDictionary
                {
                    Source = new Uri(dark
                        ? "pack://application:,,,/Palette;component/UI/Themes/DarkTheme.xaml"
                        : "pack://application:,,,/Palette;component/UI/Themes/LightTheme.xaml", UriKind.Absolute)
                };
                PaletteCache[dark] = palette;
            }
            var materials = SolidMaterials ? BuildSolidMaterials(palette) : new ResourceDictionary { ["Shadow.Opacity"] = dark ? 0.45 : 0.16 };
            return new[] { palette, materials };
        }

        /// <summary>Reduced transparency: materials become solid surfaces with a defined edge, no shadows.</summary>
        private static ResourceDictionary BuildSolidMaterials(ResourceDictionary palette)
        {
            var raised = palette["Surface.Raised"] as Brush;
            var stroke = palette["Surface.Stroke"] as Brush;
            var content = palette["Surface.Content"] as Brush;
            return new ResourceDictionary
            {
                ["Sidebar.Material"] = palette["Surface.Inset"],
                ["Toolbar.Material"] = palette["Window.Background"],
                ["Floating.Material"] = raised ?? content,
                ["Floating.Edge"] = stroke,
                ["Sidebar.Edge"] = stroke,
                ["Shadow.Opacity"] = 0.0
            };
        }

        /// <summary>High contrast: every brush maps to a Windows system colour so the user's HC scheme is honoured.</summary>
        private static ResourceDictionary BuildHighContrast()
        {
            Func<Color, SolidColorBrush> b = c => { var br = new SolidColorBrush(c); br.Freeze(); return br; };
            var window = b(SystemColors.WindowColor);
            var text = b(SystemColors.WindowTextColor);
            var gray = b(SystemColors.GrayTextColor);
            var highlight = b(SystemColors.HighlightColor);
            var highlightText = b(SystemColors.HighlightTextColor);
            var hot = b(SystemColors.HotTrackColor);
            var btn = b(SystemColors.ControlColor);
            var d = new ResourceDictionary();
            foreach (var k in new[] { "Window.Background", "Sidebar.Material", "Toolbar.Material", "Floating.Material", "Surface.Content",
                                      "Surface.Raised", "Surface.Inset", "Input.Fill", "Control.Fill", "PrimaryBackgroundBrush",
                                      "Row.Alternate", "Status.Ok.Subtle", "Status.Info.Subtle", "Status.Warning.Subtle",
                                      "Status.Error.Subtle", "Status.Neutral.Subtle", "Accent.Subtle" })
                d[k] = window;
            foreach (var k in new[] { "Window.Stroke", "Sidebar.Edge", "Floating.Edge", "Surface.Stroke", "Separator", "Control.Stroke",
                                      "Input.Stroke", "Input.StrokeHover", "Text.Primary", "Text.Secondary", "ForegroundBrush", "IconBrush",
                                      "Status.Ok", "Status.Info", "Status.Warning", "Status.Error", "Status.Neutral" })
                d[k] = text;
            d["Text.Tertiary"] = text;
            d["Text.Disabled"] = gray;
            foreach (var k in new[] { "Accent", "Accent.Hover", "Accent.Pressed", "Row.Selected", "Row.SelectedInactive", "Control.FillPressed" })
                d[k] = highlight;
            d["Text.OnAccent"] = highlightText;
            d["Accent.Text"] = hot;
            d["Focus.Ring"] = hot;
            d["Control.FillHover"] = btn;
            d["Control.Plain.Hover"] = btn;
            d["Control.Plain.Pressed"] = highlight;
            d["Row.Hover"] = btn;
            d["Scrim"] = b(Color.FromArgb(0x80, 0, 0, 0));
            d["Shadow.Color"] = Colors.Black;
            d["Shadow.Opacity"] = 0.0;
            return d;
        }
    }
}
