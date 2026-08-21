using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Gibbed.TombRaider.DRMEdit
{
    internal static class MessageBoxTheme
    {
        public static IBrush? GetContentBackgroundBrush()
        {
            return GetBrush("SystemControlPageBackgroundChromeLowBrush");
        }

        public static IBrush? GetContentHeaderForegroundBrush()
        {
            return GetBrush("SystemControlForegroundBaseHighBrush");
        }

        public static IBrush? GetContentBodyForegroundBrush()
        {
            return GetBrush("SystemControlForegroundBaseMediumHighBrush");
        }

        public static double? GetContentBodyFontSize()
        {
            return GetFontSize("ControlContentThemeFontSize");
        }

        public static double? GetContentHeaderFontSize()
        {
            var bodySize = GetContentBodyFontSize();
            return bodySize.HasValue ? bodySize.Value + 2.0 : (double?)null;
        }

        // TryFindResource without an explicit theme resolves against the base/first
        // dictionary rather than the OS's actual current theme, unlike a normal
        // DynamicResource binding in XAML (which resolves against the real visual
        // tree). Passing Application.Current.ActualThemeVariant explicitly is what
        // makes error dialogs match the same dark/light appearance every other
        // window in this app already shows.
        private static IBrush? GetBrush(string resourceKey)
        {
            if (Application.Current == null)
            {
                return null;
            }

            Application.Current.TryFindResource(
                resourceKey, Application.Current.ActualThemeVariant, out var resource);
            return resource as IBrush;
        }

        private static double? GetFontSize(string resourceKey)
        {
            if (Application.Current == null)
            {
                return null;
            }

            Application.Current.TryFindResource(
                resourceKey, Application.Current.ActualThemeVariant, out var resource);
            return resource is double size ? size : (double?)null;
        }
    }
}
