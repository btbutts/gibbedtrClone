using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Gibbed.TombRaider.DRMEdit.ViewModels;
using Gibbed.TombRaider.DRMEdit.Views;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;

namespace Gibbed.TombRaider.DRMEdit
{
    public partial class App : Application
    {
        public static List<string> StartupFiles { get; set; } = new List<string>();
        public static string? HelpText { get; set; }
        public static bool HelpIsError { get; set; }
        public static Services.IFilePickerService PickerService { get; private set; } = null!;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var helpText = HelpText;
                if (helpText != null)
                {
                    ShowHelpThenShutdown(desktop, helpText);
                }
                else
                {
                    PickerService = new Services.AvaloniaFilePickerService();
                    var mainWindowViewModel = new MainWindowViewModel(PickerService, new Services.PopOutWindowService());

                    var mainWindow = new MainWindow(StartupFiles)
                    {
                        DataContext = mainWindowViewModel,
                    };
                    mainWindowViewModel.GetTopLevel = () => mainWindow;

                    desktop.MainWindow = mainWindow;
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static async void ShowHelpThenShutdown(IClassicDesktopStyleApplicationLifetime desktop, string helpText)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
            {
                ContentTitle = "Gibbed.TombRaider.DRMEdit",
                ContentMessage = helpText,
                ButtonDefinitions = ButtonEnum.Ok,
                Icon = HelpIsError ? Icon.Error : Icon.Info,
                Background = MessageBoxTheme.GetBackgroundBrush(),
            });
            await box.ShowAsync();
            desktop.Shutdown();
        }
    }
}
