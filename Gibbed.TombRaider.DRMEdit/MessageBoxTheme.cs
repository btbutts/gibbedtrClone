using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Gibbed.TombRaider.DRMEdit
{
    internal static class MessageBoxTheme
    {
        // TryFindResource without an explicit theme resolves against the base/first
        // dictionary rather than the OS's actual current theme, unlike a normal
        // DynamicResource binding in XAML (which resolves against the real visual
        // tree). Passing Application.Current.ActualThemeVariant explicitly is what
        // makes error dialogs match the same dark/light background every other
        // window in this app already shows.
        public static IBrush? GetBackgroundBrush()
        {
            if (Application.Current == null)
            {
                return null;
            }

            Application.Current.TryFindResource(
                "SystemControlPageBackgroundChromeLowBrush", Application.Current.ActualThemeVariant, out var resource);
            return resource as IBrush;
        }
    }
}
