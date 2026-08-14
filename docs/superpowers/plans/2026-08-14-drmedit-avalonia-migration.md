# DRMEdit Avalonia Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the WinForms + `Be.Windows.Forms.HexBox` UI shell of `Gibbed.TombRaider.DRMEdit` and `Gibbed.DeusEx3.DRMEdit` with an Avalonia (cross-platform) UI, reaching functional parity on Windows, with Tomb Raider interactively verified and Deus Ex 3 verified structurally (builds + launches).

**Architecture:** MVVM via `CommunityToolkit.Mvvm`. One `MainWindow` per app with a flat `TabControl` (every opened DRM file and every opened section viewer is its own top-level tab). Every tab can pop out into its own real OS `Window` and back in, driven entirely by moving its ViewModel between two collections — never by manually reparenting a `Control`. `AvaloniaHex` replaces `Be.Windows.Forms.HexBox`. `Material.Icons.Avalonia` supplies the pop-out/pop-in glyphs. `MessageBox.Avalonia` replaces WinForms `MessageBox.Show`.

**Tech Stack:** .NET 10, Avalonia 12.1.1, Avalonia.Desktop 12.1.1, Avalonia.Themes.Fluent 12.1.1, Avalonia.Fonts.Inter 12.1.1, AvaloniaHex 0.1.13, Material.Icons.Avalonia 3.0.2, MessageBox.Avalonia 12.0.0, CommunityToolkit.Mvvm 8.4.2, BCnEncoder.Net 2.3.0 (replaces `Gibbed.Squish`'s native P/Invoke — see Global Constraints).

**Spec:** `docs/superpowers/specs/2026-08-14-drmedit-avalonia-migration-design.md`

## Global Constraints

- Branch is `DRMEdit-ReplaceHexEditor`, off `cross-platform-NET10`. Do not merge back until Task 8's Tomb Raider interactive checkpoint and Task 12's Deus Ex 3 structural checkpoint both pass.
- Both projects are in-place rewrites: same project folder, same project name, same output exe name. No new project names.
- `Gibbed.TombRaider.DRMEdit` and `Gibbed.DeusEx3.DRMEdit` stay fully duplicated, per the spec's Decision 2 — do not extract a shared UI library.
- **Superseded during Task 1**: `Gibbed.Squish`'s native `squish_32.dll`/`squish_64.dll` P/Invoke was originally meant to stay untouched this stage. That blocked `Gibbed.TombRaider.DRMEdit` from ever reaching plain `net10.0` (NuGet's TFM compatibility is one-directional — a plain project can't reference a `net10.0-windows`-tagged one), which the user did not want. Resolution: `Gibbed.Squish` was renamed `Texture.BCnE.NET.Codec` and its native P/Invoke replaced by the managed `BCnEncoder.Net` package (same public `CompressImage`/`DecompressImage`/`Flags` API, so no caller changes needed) — this is Stage 3 item 8 of `multi-platform-retarget.md`, done here instead of later. `Gibbed.IO`, `Gibbed.CrystalDynamics.FileFormats`, `Gibbed.TombRaider.FileFormats`, `Gibbed.DeusEx3.FileFormats`, and `NDesk.Options` were also retargeted to plain `net10.0` in the same pass (Stage 3 items 1-3), since they were the same TFM-compatibility blocker one layer down. Both DRMEdit projects now target plain `net10.0` for real, not `net10.0-windows`.
- No unit tests exist for DRMEdit's UI today and none are being added — this codebase's established verification method (used for every prior DRMEdit change) is build-success plus manual interactive runs. Each task's "test" step means exactly that: build clean, run, manually confirm the described behavior.
- Package versions are pinned exactly as listed in Tech Stack above — they were confirmed against the real, current NuGet listings and a real generated Avalonia 12.1.1/net10.0 project template during planning. Do not "helpfully" bump them.
- Preserve the three known gaps documented in the spec (no error handling on corrupt DRM load; `OutOfMemoryException`→general-`Exception` catch-type change; `TextureViewer`'s same-size/single-mipmap restriction) — do not add new handling for them, and do not silently drop the empty `tabPage2`/permanently-disabled buttons that exist in the current WinForms code.
- Deus Ex 3's `DRMFile` has **no `Serialize()` method** — there is no "Save DRM" capability to port. Its current `saveDRMButton` exists in the UI but is permanently `Enabled = false`. Do not invent a working Save DRM for Deus Ex 3.
- Deus Ex 3's `TextureViewer` has **no load/replace/save-back capability** — view + zoom + alpha toggle + save-to-PNG only. Do not port Tomb Raider's `ReplaceImage`/`OnSave`/`OnLoadFromFile` logic onto it.
- Deus Ex 3's `FileViewer` has a section-type filter `ComboBox` (`filterTypeBox`) that Tomb Raider's does not — preserve it.

---

## File Structure

Both projects get an identical new structure (paths shown for `Gibbed.TombRaider.DRMEdit`; `Gibbed.DeusEx3.DRMEdit` mirrors it exactly with `TombRaider`→`DeusEx3` substituted throughout, plus the Deus Ex 3-specific differences called out in Global Constraints):

```
Gibbed.TombRaider.DRMEdit/
  Gibbed.TombRaider.DRMEdit.csproj
  app.manifest
  Program.cs
  App.axaml / App.axaml.cs
  ViewLocator.cs
  Assets/Icons/__DRM.png, RenderResource.png, Script.png, Wave.png   (copied as-is from SectionTypeImages/)
  Services/IFilePickerService.cs
  Services/AvaloniaFilePickerService.cs
  Services/IPopOutWindowService.cs
  Services/PopOutWindowService.cs
  ViewModels/ViewModelBase.cs
  ViewModels/DocumentTabViewModel.cs
  ViewModels/MainWindowViewModel.cs
  ViewModels/FileViewerViewModel.cs
  ViewModels/SectionNode.cs
  ViewModels/RawViewerViewModel.cs
  ViewModels/TextureViewerViewModel.cs
  Views/MainWindow.axaml / .axaml.cs
  Views/PopOutWindow.axaml / .axaml.cs
  Views/FileViewerView.axaml / .axaml.cs
  Views/RawViewerView.axaml / .axaml.cs
  Views/TextureViewerView.axaml / .axaml.cs
```

Deleted (old WinForms shell, all of it — Task 1):
`Explorer.cs`, `Explorer.Designer.cs`, `Explorer.resx`, `FileViewer.cs`, `FileViewer.Designer.cs`, `FileViewer.resx`, `RawViewer.cs`, `RawViewer.Designer.cs`, `RawViewer.resx`, `TextureViewer.cs`, `TextureViewer.Designer.cs`, `TextureViewer.resx`, `ISectionViewer.cs`, `SectionTypeImages.Designer.cs`, `SectionTypeImages.resx`, `app.config`, `Properties/AssemblyInfo.cs` (SDK-style auto-generates what's needed; this project already used `GenerateAssemblyInfo=false` with a manual file for the Windows-only `SupportedOSPlatform` attribute — that attribute no longer applies once this project targets plain `net10.0`, so the manual file goes too). The four PNGs under `SectionTypeImages/` are **kept** (moved to `Assets/Icons/`, not deleted) — Task 3 needs them.

---

## Task 1: Strip WinForms, scaffold the Avalonia app shell (Tomb Raider)

**Files:**
- Modify: `Gibbed.TombRaider.DRMEdit/Gibbed.TombRaider.DRMEdit.csproj` (full rewrite)
- Create: `Gibbed.TombRaider.DRMEdit/app.manifest`
- Create: `Gibbed.TombRaider.DRMEdit/Program.cs` (full rewrite)
- Create: `Gibbed.TombRaider.DRMEdit/App.axaml`, `App.axaml.cs`
- Create: `Gibbed.TombRaider.DRMEdit/ViewLocator.cs`
- Create: `Gibbed.TombRaider.DRMEdit/ViewModels/ViewModelBase.cs`
- Create: `Gibbed.TombRaider.DRMEdit/Views/MainWindow.axaml`, `MainWindow.axaml.cs` (placeholder, real content in Task 2)
- Move: `Gibbed.TombRaider.DRMEdit/SectionTypeImages/*.png` → `Gibbed.TombRaider.DRMEdit/Assets/Icons/*.png`
- Delete: `Explorer.cs`, `Explorer.Designer.cs`, `Explorer.resx`, `FileViewer.cs`, `FileViewer.Designer.cs`, `FileViewer.resx`, `RawViewer.cs`, `RawViewer.Designer.cs`, `RawViewer.resx`, `TextureViewer.cs`, `TextureViewer.Designer.cs`, `TextureViewer.resx`, `ISectionViewer.cs`, `SectionTypeImages.Designer.cs`, `SectionTypeImages.resx`, `app.config`, `Properties/AssemblyInfo.cs`

**Interfaces:**
- Produces: `Gibbed.TombRaider.DRMEdit.App` (Avalonia `Application` subclass, `ViewLocator` wired as its `DataTemplates`), `Gibbed.TombRaider.DRMEdit.Views.MainWindow` (empty `Window`, real content added in Task 2), `Gibbed.TombRaider.DRMEdit.ViewModels.ViewModelBase : ObservableObject` (base for every ViewModel in later tasks).

- [ ] **Step 1: Delete the old WinForms files**

```bash
git rm Gibbed.TombRaider.DRMEdit/Explorer.cs Gibbed.TombRaider.DRMEdit/Explorer.Designer.cs Gibbed.TombRaider.DRMEdit/Explorer.resx
git rm Gibbed.TombRaider.DRMEdit/FileViewer.cs Gibbed.TombRaider.DRMEdit/FileViewer.Designer.cs Gibbed.TombRaider.DRMEdit/FileViewer.resx
git rm Gibbed.TombRaider.DRMEdit/RawViewer.cs Gibbed.TombRaider.DRMEdit/RawViewer.Designer.cs Gibbed.TombRaider.DRMEdit/RawViewer.resx
git rm Gibbed.TombRaider.DRMEdit/TextureViewer.cs Gibbed.TombRaider.DRMEdit/TextureViewer.Designer.cs Gibbed.TombRaider.DRMEdit/TextureViewer.resx
git rm Gibbed.TombRaider.DRMEdit/ISectionViewer.cs
git rm Gibbed.TombRaider.DRMEdit/SectionTypeImages.Designer.cs Gibbed.TombRaider.DRMEdit/SectionTypeImages.resx
git rm Gibbed.TombRaider.DRMEdit/app.config
git rm Gibbed.TombRaider.DRMEdit/Properties/AssemblyInfo.cs
```

- [ ] **Step 2: Move the four tree icons into an Avalonia asset folder**

```bash
mkdir -p Gibbed.TombRaider.DRMEdit/Assets/Icons
git mv "Gibbed.TombRaider.DRMEdit/SectionTypeImages/__DRM.png" Gibbed.TombRaider.DRMEdit/Assets/Icons/__DRM.png
git mv "Gibbed.TombRaider.DRMEdit/SectionTypeImages/RenderResource.png" Gibbed.TombRaider.DRMEdit/Assets/Icons/RenderResource.png
git mv "Gibbed.TombRaider.DRMEdit/SectionTypeImages/Script.png" Gibbed.TombRaider.DRMEdit/Assets/Icons/Script.png
git mv "Gibbed.TombRaider.DRMEdit/SectionTypeImages/Wave.png" Gibbed.TombRaider.DRMEdit/Assets/Icons/Wave.png
rmdir "Gibbed.TombRaider.DRMEdit/SectionTypeImages" 2>/dev/null || true
```

- [ ] **Step 3: Rewrite the csproj**

Replace the full contents of `Gibbed.TombRaider.DRMEdit/Gibbed.TombRaider.DRMEdit.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <RootNamespace>Gibbed.TombRaider.DRMEdit</RootNamespace>
    <AssemblyName>Gibbed.TombRaider.DRMEdit</AssemblyName>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <Deterministic>false</Deterministic>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>

  <PropertyGroup Condition=" '$(Configuration)' == 'Debug' ">
    <OutputPath>..\bin_tr\</OutputPath>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)' == 'Release' ">
    <OutputPath>..\bin_tr_release\</OutputPath>
  </PropertyGroup>

  <ItemGroup>
    <AvaloniaResource Include="Assets\**" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="12.1.1" />
    <PackageReference Include="Avalonia.Desktop" Version="12.1.1" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.1" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="12.1.1" />
    <PackageReference Include="AvaloniaHex" Version="0.1.13" />
    <PackageReference Include="Material.Icons.Avalonia" Version="3.0.2" />
    <PackageReference Include="MessageBox.Avalonia" Version="12.0.0" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Gibbed.IO\Gibbed.IO.csproj" />
    <ProjectReference Include="..\Texture.BCnE.NET.Codec\Texture.BCnE.NET.Codec.csproj" />
    <ProjectReference Include="..\Gibbed.TombRaider.FileFormats\Gibbed.TombRaider.FileFormats.csproj" />
    <ProjectReference Include="..\NDesk.Options\NDesk.Options.csproj" />
  </ItemGroup>

</Project>
```

Note `TargetFramework` is plain `net10.0`, not `net10.0-windows` — this project is the one leading the cross-platform effort, and `-windows` would be actively wrong here. The `Be.Windows.Forms.HexBox` `HintPath` reference is gone (replaced by `AvaloniaHex`), and there's no `UseWindowsForms` anymore.

- [ ] **Step 4: Add the Windows app manifest**

Create `Gibbed.TombRaider.DRMEdit/app.manifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <!-- This manifest is used on Windows only.
       Don't remove it as it might cause problems with window transparency and embedded controls. -->
  <assemblyIdentity version="1.0.0.0" name="Gibbed.TombRaider.DRMEdit.Desktop"/>

  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <!-- Windows 10 -->
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
</assembly>
```

- [ ] **Step 5: Rewrite Program.cs — keep the NDesk.Options CLI parsing byte-for-byte, swap the bootstrap**

Replace the full contents of `Gibbed.TombRaider.DRMEdit/Program.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using NDesk.Options;

namespace Gibbed.TombRaider.DRMEdit
{
    internal static class Program
    {
        private static string GetExecutableName()
        {
            return Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        private static bool LooksLikeOption(string arg)
        {
            return string.IsNullOrEmpty(arg) == false &&
                (arg[0] == '-' || arg[0] == '/');
        }

        [STAThread]
        public static void Main(string[] args)
        {
            bool showHelp = false;

            var options = new OptionSet()
            {
                {
                    "h|help",
                    "show this message and exit",
                    v => showHelp = v != null
                },
            };

            List<string> extras = new List<string>();
            string parseError = null;

            try
            {
                extras = options.Parse(args);
            }
            catch (OptionException e)
            {
                parseError = e.Message;
                showHelp = true;
            }

            if (showHelp == false)
            {
                var badOption = extras.FirstOrDefault(a => LooksLikeOption(a));
                if (badOption != null)
                {
                    parseError = string.Format("unrecognized option `{0}'.", badOption);
                    showHelp = true;
                }
            }

            string helpText = null;
            if (showHelp == true)
            {
                var sb = new StringBuilder();
                if (parseError != null)
                {
                    sb.AppendFormat("{0}: {1}", GetExecutableName(), parseError);
                    sb.AppendLine();
                    sb.AppendLine();
                }
                sb.AppendFormat("Usage: {0} [OPTIONS]+ [file ...]", GetExecutableName());
                sb.AppendLine();
                sb.AppendLine("Opens the DRM file browser/editor. Any extra arguments are opened as");
                sb.AppendLine("individual DRM/resource files on startup.");
                sb.AppendLine();
                sb.AppendLine("Options:");
                using (var writer = new StringWriter(sb))
                {
                    options.WriteOptionDescriptions(writer);
                }
                helpText = sb.ToString();
            }

            App.StartupFiles = extras;
            App.HelpText = helpText;
            App.HelpIsError = parseError != null;

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
```

The CLI parsing itself is untouched from the original — same `OptionSet`, same help-text construction, same unrecognized-option detection. What changes is only what happens with the result: instead of showing a `MessageBox` and returning immediately (which can't happen before Avalonia's dispatcher exists), the parsed state is handed to `App` via static fields, and `App.OnFrameworkInitializationCompleted` (Step 6) decides whether to show a help dialog and exit, or open `MainWindow`.

- [ ] **Step 6: Create App.axaml / App.axaml.cs**

Create `Gibbed.TombRaider.DRMEdit/App.axaml`:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Gibbed.TombRaider.DRMEdit.App"
             xmlns:local="using:Gibbed.TombRaider.DRMEdit"
             RequestedThemeVariant="Default">

    <Application.DataTemplates>
        <local:ViewLocator />
    </Application.DataTemplates>

    <Application.Styles>
        <FluentTheme />
    </Application.Styles>

</Application>
```

Create `Gibbed.TombRaider.DRMEdit/App.axaml.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Gibbed.TombRaider.DRMEdit.Views;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace Gibbed.TombRaider.DRMEdit
{
    public partial class App : Application
    {
        public static List<string> StartupFiles { get; set; } = new List<string>();
        public static string HelpText { get; set; }
        public static bool HelpIsError { get; set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (HelpText != null)
                {
                    ShowHelpThenShutdown(desktop);
                }
                else
                {
                    desktop.MainWindow = new MainWindow(StartupFiles);
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static async void ShowHelpThenShutdown(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(
                "Gibbed.TombRaider.DRMEdit",
                HelpText,
                ButtonEnum.Ok,
                HelpIsError ? Icon.Error : Icon.Info);
            await box.ShowAsync();
            desktop.Shutdown();
        }
    }
}
```

- [ ] **Step 7: Create ViewLocator.cs**

Create `Gibbed.TombRaider.DRMEdit/ViewLocator.cs`:

```csharp
using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Gibbed.TombRaider.DRMEdit.ViewModels;

namespace Gibbed.TombRaider.DRMEdit
{
    public class ViewLocator : IDataTemplate
    {
        public Control Build(object param)
        {
            if (param == null)
            {
                return null;
            }

            var name = param.GetType().FullName.Replace("ViewModel", "View", StringComparison.Ordinal);
            var type = Type.GetType(name);

            if (type != null)
            {
                return (Control)Activator.CreateInstance(type);
            }

            return new TextBlock { Text = "Not Found: " + name };
        }

        public bool Match(object data)
        {
            return data is ViewModelBase;
        }
    }
}
```

This is the mechanism referenced throughout the spec as "Avalonia `DataTemplate`s per ViewModel type resolve which View renders" — `FileViewerViewModel` → `FileViewerView`, `RawViewerViewModel` → `RawViewerView`, `TextureViewerViewModel` → `TextureViewerView`, purely by naming convention, no manual registration needed per type.

- [ ] **Step 8: Create ViewModelBase**

Create `Gibbed.TombRaider.DRMEdit/ViewModels/ViewModelBase.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    public abstract class ViewModelBase : ObservableObject
    {
    }
}
```

- [ ] **Step 9: Create a placeholder MainWindow (real content comes in Task 2)**

Create `Gibbed.TombRaider.DRMEdit/Views/MainWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="Gibbed.TombRaider.DRMEdit.Views.MainWindow"
        Width="1000" Height="700"
        Title="Tomb Raider DRM Editor">
    <TextBlock Text="Scaffold OK" HorizontalAlignment="Center" VerticalAlignment="Center" />
</Window>
```

Create `Gibbed.TombRaider.DRMEdit/Views/MainWindow.axaml.cs`:

```csharp
using System.Collections.Generic;
using Avalonia.Controls;

namespace Gibbed.TombRaider.DRMEdit.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(List<string> startupFiles) : this()
        {
        }
    }
}
```

The `startupFiles` parameter is accepted but unused until Task 3 (it needs `MainWindowViewModel.OpenFile` to exist).

- [ ] **Step 10: Build and run**

Run: `dotnet build "Crystal Dynamics.sln" -c Debug`
Expected: 0 errors. (Warnings from the new packages, if any, get resolved in later tasks — note any that appear so Task 8's final pass can check them.)

Run: `dotnet run --project Gibbed.TombRaider.DRMEdit/Gibbed.TombRaider.DRMEdit.csproj`
Expected: a window titled "Tomb Raider DRM Editor" opens showing "Scaffold OK". Close it.

- [ ] **Step 11: Commit**

```bash
git add -A Gibbed.TombRaider.DRMEdit
git commit -m "DRMEdit (TR): strip WinForms, scaffold Avalonia app shell"
```

---

## Task 2: Core MVVM infrastructure + MainWindow shell (Tomb Raider)

**Files:**
- Create: `Gibbed.TombRaider.DRMEdit/ViewModels/DocumentTabViewModel.cs`
- Create: `Gibbed.TombRaider.DRMEdit/ViewModels/MainWindowViewModel.cs`
- Create: `Gibbed.TombRaider.DRMEdit/Services/IFilePickerService.cs`
- Create: `Gibbed.TombRaider.DRMEdit/Services/AvaloniaFilePickerService.cs`
- Create: `Gibbed.TombRaider.DRMEdit/Services/IPopOutWindowService.cs`
- Create: `Gibbed.TombRaider.DRMEdit/Services/PopOutWindowService.cs`
- Create: `Gibbed.TombRaider.DRMEdit/Views/PopOutWindow.axaml`, `PopOutWindow.axaml.cs`
- Modify: `Gibbed.TombRaider.DRMEdit/Views/MainWindow.axaml`, `MainWindow.axaml.cs`
- Modify: `Gibbed.TombRaider.DRMEdit/App.axaml.cs` (wire real `MainWindowViewModel`)
- Create (temporary, deleted in Task 3): `Gibbed.TombRaider.DRMEdit/ViewModels/DemoDocumentViewModel.cs`, `Gibbed.TombRaider.DRMEdit/Views/DemoDocumentView.axaml` + `.axaml.cs`

**Interfaces:**
- Consumes: `ViewModelBase` (Task 1).
- Produces: `DocumentTabViewModel` (abstract base — `Title` (string, observable), `IsPoppedOut` (bool, observable), `PopOutCommand`/`PopInCommand`/`CloseCommand` (`IRelayCommand`), `GetTopLevel` (`Func<TopLevel?>?` settable property), events `RequestPopOut`, `RequestPopIn`, `RequestClose`). `MainWindowViewModel` — `ObservableCollection<DocumentTabViewModel> OpenDocuments`, `DocumentTabViewModel? SelectedDocument` (observable), `void AddDocument(DocumentTabViewModel document)`, `OpenCommand`, `CloseAllCommand`. `IFilePickerService.OpenFilesAsync(TopLevel owner, string title, IReadOnlyList<FilePickerFileType> fileTypes, bool allowMultiple) : Task<IReadOnlyList<string>>`, `IFilePickerService.SaveFileAsync(TopLevel owner, string title, IReadOnlyList<FilePickerFileType> fileTypes, string? suggestedFileName) : Task<string?>`. `IPopOutWindowService.PopOut(DocumentTabViewModel document, Action onClosedByUser)`, `IPopOutWindowService.Close(DocumentTabViewModel document)`. Later tasks' ViewModels (`FileViewerViewModel`, `RawViewerViewModel`, `TextureViewerViewModel`) derive from `DocumentTabViewModel` and are constructed with a reference to the owning `MainWindowViewModel` so they can call `mainWindowViewModel.AddDocument(...)` to open further tabs (e.g. a file opening a raw/texture viewer).

- [ ] **Step 1: Create DocumentTabViewModel**

Create `Gibbed.TombRaider.DRMEdit/ViewModels/DocumentTabViewModel.cs`:

```csharp
using System;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    public abstract partial class DocumentTabViewModel : ViewModelBase
    {
        [ObservableProperty]
        public partial string Title { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsPoppedOut { get; set; }

        public Func<TopLevel> GetTopLevel { get; set; }

        public event EventHandler RequestPopOut;
        public event EventHandler RequestPopIn;
        public event EventHandler RequestClose;

        [RelayCommand]
        private void PopOut()
        {
            RequestPopOut?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void PopIn()
        {
            RequestPopIn?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        public void Close()
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }
}
```

`Close()` is `public` (not just the generated command) because Task 3's "Close All" needs to invoke it directly on every open document.

- [ ] **Step 2: Create the file-picker service**

Create `Gibbed.TombRaider.DRMEdit/Services/IFilePickerService.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Gibbed.TombRaider.DRMEdit.Services
{
    public interface IFilePickerService
    {
        Task<IReadOnlyList<string>> OpenFilesAsync(
            TopLevel owner, string title, IReadOnlyList<FilePickerFileType> fileTypes, bool allowMultiple);

        Task<string> SaveFileAsync(
            TopLevel owner, string title, IReadOnlyList<FilePickerFileType> fileTypes, string suggestedFileName);
    }
}
```

Create `Gibbed.TombRaider.DRMEdit/Services/AvaloniaFilePickerService.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Gibbed.TombRaider.DRMEdit.Services
{
    public class AvaloniaFilePickerService : IFilePickerService
    {
        public async Task<IReadOnlyList<string>> OpenFilesAsync(
            TopLevel owner, string title, IReadOnlyList<FilePickerFileType> fileTypes, bool allowMultiple)
        {
            if (owner == null)
            {
                return System.Array.Empty<string>();
            }

            var result = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                FileTypeFilter = fileTypes,
                AllowMultiple = allowMultiple,
            });

            return result
                .Select(f => f.TryGetLocalPath())
                .Where(p => p != null)
                .ToList();
        }

        public async Task<string> SaveFileAsync(
            TopLevel owner, string title, IReadOnlyList<FilePickerFileType> fileTypes, string suggestedFileName)
        {
            if (owner == null)
            {
                return null;
            }

            var result = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                FileTypeChoices = fileTypes,
                SuggestedFileName = suggestedFileName,
            });

            return result?.TryGetLocalPath();
        }
    }
}
```

The `owner` parameter (rather than the service holding a single `TopLevel` itself) is what lets this work correctly whether the calling document is currently docked in `MainWindow` or popped out into its own `Window` — each `DocumentTabViewModel.GetTopLevel()` resolves to whichever one currently hosts it.

- [ ] **Step 3: Create the pop-out window service**

Create `Gibbed.TombRaider.DRMEdit/Services/IPopOutWindowService.cs`:

```csharp
using System;
using Gibbed.TombRaider.DRMEdit.ViewModels;

namespace Gibbed.TombRaider.DRMEdit.Services
{
    public interface IPopOutWindowService
    {
        void PopOut(DocumentTabViewModel document, Action onClosedByUser);
        void Close(DocumentTabViewModel document);
    }
}
```

Create `Gibbed.TombRaider.DRMEdit/Services/PopOutWindowService.cs`:

```csharp
using System;
using System.Collections.Generic;
using Gibbed.TombRaider.DRMEdit.ViewModels;
using Gibbed.TombRaider.DRMEdit.Views;

namespace Gibbed.TombRaider.DRMEdit.Services
{
    public class PopOutWindowService : IPopOutWindowService
    {
        private sealed class Entry
        {
            public PopOutWindow Window;
            public bool ClosedProgrammatically;
        }

        private readonly Dictionary<DocumentTabViewModel, Entry> _entries = new();

        public void PopOut(DocumentTabViewModel document, Action onClosedByUser)
        {
            var window = new PopOutWindow { DataContext = document };
            var entry = new Entry { Window = window };
            _entries[document] = entry;

            window.Closed += (_, _) =>
            {
                _entries.Remove(document);
                if (entry.ClosedProgrammatically == false)
                {
                    onClosedByUser();
                }
            };

            document.GetTopLevel = () => window;

            window.Show();
        }

        public void Close(DocumentTabViewModel document)
        {
            if (_entries.TryGetValue(document, out var entry) == false)
            {
                return;
            }

            entry.ClosedProgrammatically = true;
            entry.Window.Close();
        }
    }
}
```

`Close` is used for both "pop back in" (re-dock — `MainWindowViewModel` re-adds the document right after) and "close while popped out" (document goes away entirely) — the caller decides which by what it does after calling `Close`. Either way the OS window closing itself never triggers `onClosedByUser` (that callback fires only for a real ✕-click), matching the spec's "✕ closes the document, dock-window button re-docks" decision.

- [ ] **Step 4: Create PopOutWindow**

Create `Gibbed.TombRaider.DRMEdit/Views/PopOutWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Gibbed.TombRaider.DRMEdit.ViewModels"
        xmlns:materialIcons="clr-namespace:Material.Icons.Avalonia;assembly=Material.Icons.Avalonia"
        x:Class="Gibbed.TombRaider.DRMEdit.Views.PopOutWindow"
        x:DataType="vm:DocumentTabViewModel"
        Width="700" Height="500"
        Title="{Binding Title}">

    <DockPanel>
        <Button DockPanel.Dock="Top" HorizontalAlignment="Right" Margin="4"
                Command="{Binding PopInCommand}"
                ToolTip.Tip="Dock back into main window">
            <materialIcons:MaterialIcon Kind="DockWindow" />
        </Button>
        <ContentControl Content="{Binding}" />
    </DockPanel>

</Window>
```

Create `Gibbed.TombRaider.DRMEdit/Views/PopOutWindow.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace Gibbed.TombRaider.DRMEdit.Views
{
    public partial class PopOutWindow : Window
    {
        public PopOutWindow()
        {
            InitializeComponent();
        }
    }
}
```

`ContentControl Content="{Binding}"` binds the whole `DocumentTabViewModel` as content; the application-wide `ViewLocator` `DataTemplate` (Task 1, Step 7) resolves it to the correct View (`FileViewerView`, `RawViewerView`, or `TextureViewerView`) automatically — this window never needs to know which kind of document it's hosting.

- [ ] **Step 5: Create MainWindowViewModel**

Create `Gibbed.TombRaider.DRMEdit/ViewModels/MainWindowViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Gibbed.TombRaider.DRMEdit.Services;

namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IFilePickerService _filePickerService;
        private readonly IPopOutWindowService _popOutWindowService;

        public ObservableCollection<DocumentTabViewModel> OpenDocuments { get; } = new();

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        public partial DocumentTabViewModel SelectedDocument { get; set; }

        public System.Func<TopLevel> GetTopLevel { get; set; }

        public MainWindowViewModel(IFilePickerService filePickerService, IPopOutWindowService popOutWindowService)
        {
            _filePickerService = filePickerService;
            _popOutWindowService = popOutWindowService;
        }

        public void AddDocument(DocumentTabViewModel document)
        {
            document.RequestPopOut += OnRequestPopOut;
            document.RequestPopIn += OnRequestPopIn;
            document.RequestClose += OnRequestClose;
            document.GetTopLevel = GetTopLevel;
            OpenDocuments.Add(document);
            SelectedDocument = document;
        }

        private void RemoveDocument(DocumentTabViewModel document)
        {
            document.RequestPopOut -= OnRequestPopOut;
            document.RequestPopIn -= OnRequestPopIn;
            document.RequestClose -= OnRequestClose;
            OpenDocuments.Remove(document);
        }

        private void OnRequestPopOut(object sender, System.EventArgs e)
        {
            var document = (DocumentTabViewModel)sender;
            OpenDocuments.Remove(document);
            document.IsPoppedOut = true;
            _popOutWindowService.PopOut(document, () => RemoveDocument(document));
        }

        private void OnRequestPopIn(object sender, System.EventArgs e)
        {
            var document = (DocumentTabViewModel)sender;
            _popOutWindowService.Close(document);
            document.IsPoppedOut = false;
            document.GetTopLevel = GetTopLevel;
            OpenDocuments.Add(document);
            SelectedDocument = document;
        }

        private void OnRequestClose(object sender, System.EventArgs e)
        {
            var document = (DocumentTabViewModel)sender;
            if (document.IsPoppedOut == true)
            {
                _popOutWindowService.Close(document);
            }
            RemoveDocument(document);
        }

        [RelayCommand]
        private async Task OpenAsync()
        {
            var fileTypes = new[]
            {
                new FilePickerFileType("TR DRM Files") { Patterns = new[] { "*.drm" } },
                FilePickerFileTypes.All,
            };

            var paths = await _filePickerService.OpenFilesAsync(GetTopLevel?.Invoke(), "Open DRM", fileTypes, true);
            foreach (var path in paths)
            {
                OpenFile(path);
            }
        }

        public void OpenFile(string path)
        {
            // FileViewerViewModel doesn't exist until Task 3 — DemoDocumentViewModel stands in for it here.
            AddDocument(new DemoDocumentViewModel(path));
        }

        [RelayCommand]
        private void CloseAll()
        {
            foreach (var document in OpenDocuments.ToList())
            {
                document.Close();
            }
        }
    }
}
```

- [ ] **Step 6: Create a temporary demo document (deleted in Task 3) so the shell is end-to-end testable now**

Create `Gibbed.TombRaider.DRMEdit/ViewModels/DemoDocumentViewModel.cs`:

```csharp
namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    // Temporary stand-in for FileViewerViewModel, which doesn't exist until Task 3.
    // Deleted once FileViewerViewModel takes over OpenFile in MainWindowViewModel.
    public class DemoDocumentViewModel : DocumentTabViewModel
    {
        public DemoDocumentViewModel(string path)
        {
            Title = System.IO.Path.GetFileName(path);
        }
    }
}
```

Create `Gibbed.TombRaider.DRMEdit/Views/DemoDocumentView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Gibbed.TombRaider.DRMEdit.Views.DemoDocumentView">
    <TextBlock Text="{Binding Title}" HorizontalAlignment="Center" VerticalAlignment="Center" />
</UserControl>
```

Create `Gibbed.TombRaider.DRMEdit/Views/DemoDocumentView.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace Gibbed.TombRaider.DRMEdit.Views
{
    public partial class DemoDocumentView : UserControl
    {
        public DemoDocumentView()
        {
            InitializeComponent();
        }
    }
}
```

- [ ] **Step 7: Build the real MainWindow — menu, toolbar, tab strip with per-tab pop-out button**

Replace `Gibbed.TombRaider.DRMEdit/Views/MainWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Gibbed.TombRaider.DRMEdit.ViewModels"
        xmlns:materialIcons="clr-namespace:Material.Icons.Avalonia;assembly=Material.Icons.Avalonia"
        x:Class="Gibbed.TombRaider.DRMEdit.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Width="1100" Height="750"
        Title="Tomb Raider DRM Editor">

    <DockPanel>
        <Menu DockPanel.Dock="Top">
            <MenuItem Header="_File">
                <MenuItem Header="_Open" Command="{Binding OpenCommand}" InputGesture="Ctrl+O" />
                <Separator />
                <MenuItem Header="E_xit" Click="OnExitClick" />
            </MenuItem>
            <MenuItem Header="_Windows">
                <MenuItem Header="C_lose All" Command="{Binding CloseAllCommand}" />
            </MenuItem>
        </Menu>

        <ToolBar DockPanel.Dock="Top">
            <Button Content="Open DRM" Command="{Binding OpenCommand}" />
        </ToolBar>

        <TabControl ItemsSource="{Binding OpenDocuments}"
                    SelectedItem="{Binding SelectedDocument}">
            <TabControl.ItemTemplate>
                <DataTemplate DataType="vm:DocumentTabViewModel">
                    <StackPanel Orientation="Horizontal" Spacing="4">
                        <TextBlock Text="{Binding Title}" VerticalAlignment="Center" />
                        <Button Command="{Binding PopOutCommand}"
                                Classes="tabPopOut"
                                ToolTip.Tip="Pop out into its own window">
                            <materialIcons:MaterialIcon Kind="Export" Width="14" Height="14" />
                        </Button>
                        <Button Command="{Binding CloseCommand}"
                                ToolTip.Tip="Close">
                            <materialIcons:MaterialIcon Kind="Close" Width="14" Height="14" />
                        </Button>
                    </StackPanel>
                </DataTemplate>
            </TabControl.ItemTemplate>
        </TabControl>
    </DockPanel>

    <Window.Styles>
        <!-- Pop-out button: hidden unless its tab is hovered or selected. -->
        <Style Selector="TabItem:not(:pointerover):not(:selected) Button.tabPopOut">
            <Setter Property="IsVisible" Value="False" />
        </Style>
    </Window.Styles>

</Window>
```

Replace `Gibbed.TombRaider.DRMEdit/Views/MainWindow.axaml.cs`:

```csharp
using System.Collections.Generic;
using Avalonia.Controls;
using Gibbed.TombRaider.DRMEdit.ViewModels;

namespace Gibbed.TombRaider.DRMEdit.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(List<string> startupFiles) : this()
        {
            Opened += (_, _) =>
            {
                if (DataContext is MainWindowViewModel viewModel)
                {
                    foreach (var path in startupFiles)
                    {
                        viewModel.OpenFile(path);
                    }
                }
            };
        }

        private void OnExitClick(object sender, System.EventArgs e)
        {
            Close();
        }
    }
}
```

Note the `not(:pointerover):not(:selected)` selector matches the spec exactly: the pop-out button shows on hover *or* when the tab is the active/selected one, and there's no pop-out button on the `TabControl` itself — only individual `TabItem`s carry one.

- [ ] **Step 8: Wire the real MainWindowViewModel into App.axaml.cs**

In `Gibbed.TombRaider.DRMEdit/App.axaml.cs`, replace the `else` branch of `OnFrameworkInitializationCompleted`:

```csharp
                else
                {
                    var mainWindowViewModel = new MainWindowViewModel(
                        new Services.AvaloniaFilePickerService(),
                        new Services.PopOutWindowService());

                    var mainWindow = new MainWindow(StartupFiles)
                    {
                        DataContext = mainWindowViewModel,
                    };
                    mainWindowViewModel.GetTopLevel = () => mainWindow;

                    desktop.MainWindow = mainWindow;
                }
```

- [ ] **Step 9: Build and manually verify the shell end-to-end**

Run: `dotnet build "Crystal Dynamics.sln" -c Debug`
Expected: 0 errors.

Run: `dotnet run --project Gibbed.TombRaider.DRMEdit/Gibbed.TombRaider.DRMEdit.csproj`

Manually confirm:
- File → Open (or the toolbar button) shows a native file picker; pick any file (it'll just show its filename as a demo tab — the picker filter/DRM parsing isn't real yet).
- The opened tab shows a pop-out button only when hovered or selected, never on other tabs, never on the `TabControl` chrome itself.
- Clicking pop-out detaches the tab into its own `Window`, titled with the file name, with an always-visible dock-window button.
- Clicking the dock-window button re-docks it as a tab.
- Closing a popped-out window via its OS ✕ removes the document entirely (it does not reappear as a tab).
- Windows → Close All closes every open tab.
- Close the app.

- [ ] **Step 10: Commit**

```bash
git add -A Gibbed.TombRaider.DRMEdit
git commit -m "DRMEdit (TR): core MVVM infrastructure, MainWindow shell, pop-out/pop-in"
```

---

## Task 3: FileViewer — real tree, real Open, remove the demo (Tomb Raider)

**Files:**
- Create: `Gibbed.TombRaider.DRMEdit/ViewModels/SectionNode.cs`
- Create: `Gibbed.TombRaider.DRMEdit/ViewModels/FileViewerViewModel.cs`
- Create: `Gibbed.TombRaider.DRMEdit/Views/FileViewerView.axaml`, `FileViewerView.axaml.cs`
- Modify: `Gibbed.TombRaider.DRMEdit/ViewModels/MainWindowViewModel.cs` (`OpenFile` creates a real `FileViewerViewModel`)
- Delete: `Gibbed.TombRaider.DRMEdit/ViewModels/DemoDocumentViewModel.cs`, `Gibbed.TombRaider.DRMEdit/Views/DemoDocumentView.axaml`, `Gibbed.TombRaider.DRMEdit/Views/DemoDocumentView.axaml.cs`

**Interfaces:**
- Consumes: `DocumentTabViewModel` (Task 2), `MainWindowViewModel.AddDocument` (Task 2), `Gibbed.TombRaider.FileFormats.DRMFile`/`DRM.Section`/`DRM.SectionType` (existing, unchanged).
- Produces: `SectionNode` (`string DisplayName`, `string IconKey`, `DRM.Section Section`) — the tree's bound item type. `FileViewerViewModel(MainWindowViewModel owner, string path)` — `ObservableCollection<SectionNode> Sections`, `SectionNode? SelectedSection` (observable), `SaveDrmCommand`, `ViewSectionCommand`, `ViewSectionRawCommand`. `RawViewerViewModel` and `TextureViewerViewModel` (Tasks 4/6) are constructed as `new RawViewerViewModel(owner, section)` / `new TextureViewerViewModel(owner, section)` — `FileViewerViewModel` calls those constructors directly (see Global Constraint: duplication between file and section viewers' knowledge of each other is fine here, this is the same codebase, not a layering violation).

- [ ] **Step 1: Delete the demo document**

```bash
git rm Gibbed.TombRaider.DRMEdit/ViewModels/DemoDocumentViewModel.cs
git rm Gibbed.TombRaider.DRMEdit/Views/DemoDocumentView.axaml Gibbed.TombRaider.DRMEdit/Views/DemoDocumentView.axaml.cs
```

- [ ] **Step 2: Create SectionNode**

Create `Gibbed.TombRaider.DRMEdit/ViewModels/SectionNode.cs`:

```csharp
using DRM = Gibbed.TombRaider.FileFormats.DRM;

namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    public class SectionNode
    {
        public string DisplayName { get; }
        public string IconKey { get; }
        public DRM.Section Section { get; }

        public SectionNode(DRM.Section section)
        {
            Section = section;

            var typeName = section.Type.ToString();
            var name = section.Id.ToString("X8");
            name += " : " + typeName;
            name += string.Format(
                " [{0:X2} {1:X2} {2:X4} {3:X8}]",
                section.Flags, section.Unknown05, section.Unknown06, section.Unknown10);

            if (section.Data != null)
            {
                name += " (" + section.Data.Length.ToString() + ")";
            }

            DisplayName = name;
            IconKey = typeName;
        }
    }
}
```

Same formatting as the original `FileViewer.BuildTree`'s active (non-commented) branch — `X8` id, type name, the four hex fields, byte length suffix.

- [ ] **Step 3: Create FileViewerViewModel**

Create `Gibbed.TombRaider.DRMEdit/ViewModels/FileViewerViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Gibbed.IO;
using DRM = Gibbed.TombRaider.FileFormats.DRM;

namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    public partial class FileViewerViewModel : DocumentTabViewModel
    {
        private readonly MainWindowViewModel _owner;
        private readonly string _path;
        private readonly FileFormats.DRMFile _fileData;

        public ObservableCollection<SectionNode> Sections { get; } = new();

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        public partial SectionNode SelectedSection { get; set; }

        public FileViewerViewModel(MainWindowViewModel owner, string path)
        {
            _owner = owner;
            _path = path;
            Title = "DRM View: " + Path.GetFileName(path);

            using (var input = File.OpenRead(path))
            {
                var data = new FileFormats.DRMFile();
                data.Deserialize(input);
                _fileData = data;
            }

            foreach (var section in _fileData.Sections.OrderBy(s => s.Id))
            {
                Sections.Add(new SectionNode(section));
            }
        }

        [RelayCommand]
        private async Task SaveDrmAsync()
        {
            var fileTypes = new[]
            {
                new FilePickerFileType("DRM Files") { Patterns = new[] { "*.drm" } },
                FilePickerFileTypes.All,
            };

            var savePath = await App.PickerService.SaveFileAsync(
                GetTopLevel?.Invoke(), "Save DRM", fileTypes, Path.GetFileName(_path));
            if (savePath == null)
            {
                return;
            }

            using (var output = File.Create(savePath))
            {
                var data = _fileData.Serialize();
                output.WriteFromStream(data, data.Length);
            }
        }

        [RelayCommand]
        private void ViewSection()
        {
            OpenSection(SelectedSection, false);
        }

        [RelayCommand]
        private void ViewSectionRaw()
        {
            OpenSection(SelectedSection, true);
        }

        public void OpenSection(SectionNode node, bool forceRaw)
        {
            if (node == null)
            {
                return;
            }

            var section = node.Section;
            if (section.Data != null)
            {
                section.Data.Seek(0, System.IO.SeekOrigin.Begin);
            }

            DocumentTabViewModel viewer;
            if (forceRaw == true)
            {
                viewer = new RawViewerViewModel(section);
            }
            else
            {
                viewer = section.Type == DRM.SectionType.RenderResource
                    ? new TextureViewerViewModel(section)
                    : new RawViewerViewModel(section);
            }

            _owner.AddDocument(viewer);
        }
    }
}
```

`SaveDrmAsync` uses `App.PickerService` (a small static accessor — added in Step 4) rather than a constructor-injected `IFilePickerService`, since `FileViewerViewModel` is constructed directly by `MainWindowViewModel.OpenFile`/`OpenSection`, not through DI. This matches the same pragmatic, no-DI-container approach already used for `App.StartupFiles`/`HelpText`.

- [ ] **Step 4: Expose the file-picker service as a static App accessor**

In `Gibbed.TombRaider.DRMEdit/App.axaml.cs`, add a static property and set it in Step 8 of Task 2's block:

```csharp
        public static Services.IFilePickerService PickerService { get; private set; }
```

And in `OnFrameworkInitializationCompleted`'s `else` branch, before constructing `MainWindowViewModel`:

```csharp
                    PickerService = new Services.AvaloniaFilePickerService();
                    var mainWindowViewModel = new MainWindowViewModel(PickerService, new Services.PopOutWindowService());
```

- [ ] **Step 5: Create FileViewerView**

Create `Gibbed.TombRaider.DRMEdit/Views/FileViewerView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Gibbed.TombRaider.DRMEdit.ViewModels"
             x:Class="Gibbed.TombRaider.DRMEdit.Views.FileViewerView"
             x:DataType="vm:FileViewerViewModel">

    <UserControl.Resources>
        <Bitmap x:Key="Icon.__DRM">avares://Gibbed.TombRaider.DRMEdit/Assets/Icons/__DRM.png</Bitmap>
        <Bitmap x:Key="Icon.RenderResource">avares://Gibbed.TombRaider.DRMEdit/Assets/Icons/RenderResource.png</Bitmap>
        <Bitmap x:Key="Icon.Script">avares://Gibbed.TombRaider.DRMEdit/Assets/Icons/Script.png</Bitmap>
        <Bitmap x:Key="Icon.Wave">avares://Gibbed.TombRaider.DRMEdit/Assets/Icons/Wave.png</Bitmap>
    </UserControl.Resources>

    <DockPanel>
        <ToolBar DockPanel.Dock="Top">
            <Button Content="Save DRM" Command="{Binding SaveDrmCommand}" />
            <Separator />
            <Button Content="View Section" Command="{Binding ViewSectionCommand}" />
            <Button Content="View Section Raw" Command="{Binding ViewSectionRawCommand}" />
        </ToolBar>

        <TreeView ItemsSource="{Binding Sections}"
                  SelectedItem="{Binding SelectedSection}"
                  DoubleTapped="OnNodeDoubleTapped">
            <TreeView.ItemTemplate>
                <DataTemplate DataType="vm:SectionNode">
                    <StackPanel Orientation="Horizontal" Spacing="4">
                        <Image Width="16" Height="16"
                               Source="{Binding IconKey, Converter={StaticResource IconKeyToBitmapConverter}}" />
                        <TextBlock Text="{Binding DisplayName}" />
                    </StackPanel>
                </DataTemplate>
            </TreeView.ItemTemplate>
        </TreeView>
    </DockPanel>

</UserControl>
```

Create `Gibbed.TombRaider.DRMEdit/Views/FileViewerView.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Gibbed.TombRaider.DRMEdit.ViewModels;

namespace Gibbed.TombRaider.DRMEdit.Views
{
    public partial class FileViewerView : UserControl
    {
        public static readonly IValueConverter IconKeyToBitmapConverter =
            new FuncValueConverter<string, Bitmap>(iconKey =>
            {
                var uri = iconKey switch
                {
                    "__DRM" => "avares://Gibbed.TombRaider.DRMEdit/Assets/Icons/__DRM.png",
                    "RenderResource" => "avares://Gibbed.TombRaider.DRMEdit/Assets/Icons/RenderResource.png",
                    "Script" => "avares://Gibbed.TombRaider.DRMEdit/Assets/Icons/Script.png",
                    "Wave" => "avares://Gibbed.TombRaider.DRMEdit/Assets/Icons/Wave.png",
                    _ => null,
                };
                return uri == null ? null : new Bitmap(Avalonia.Platform.AssetLoader.Open(new System.Uri(uri)));
            });

        public FileViewerView()
        {
            InitializeComponent();

            AttachedToVisualTree += (_, _) =>
            {
                if (DataContext is FileViewerViewModel viewModel)
                {
                    viewModel.GetTopLevel = () => TopLevel.GetTopLevel(this);
                }
            };
        }

        private void OnNodeDoubleTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is FileViewerViewModel viewModel && viewModel.SelectedSection != null)
            {
                viewModel.OpenSection(viewModel.SelectedSection, false);
            }
        }
    }
}
```

Remove the unused `UserControl.Resources` `Bitmap` entries from the `.axaml` above (the converter in code-behind supersedes them — leaving both would just be dead XAML). Edit `FileViewerView.axaml` to drop the `<UserControl.Resources>` block entirely and add `xmlns:views="using:Gibbed.TombRaider.DRMEdit.Views"` plus `{x:Static views:FileViewerView.IconKeyToBitmapConverter}` in place of `{StaticResource IconKeyToBitmapConverter}`:

```xml
    <TreeView ItemsSource="{Binding Sections}"
              SelectedItem="{Binding SelectedSection}"
              DoubleTapped="OnNodeDoubleTapped">
        <TreeView.ItemTemplate>
            <DataTemplate DataType="vm:SectionNode">
                <StackPanel Orientation="Horizontal" Spacing="4">
                    <Image Width="16" Height="16"
                           Source="{Binding IconKey, Converter={x:Static views:FileViewerView.IconKeyToBitmapConverter}}" />
                    <TextBlock Text="{Binding DisplayName}" />
                </StackPanel>
            </DataTemplate>
        </TreeView.ItemTemplate>
    </TreeView>
```

This is what `AttachedToVisualTree` gives us that `App`'s single static `MainWindowViewModel.GetTopLevel` can't: once a `FileViewerViewModel` is popped out, its `GetTopLevel` needs to resolve to the pop-out `Window`, not `MainWindow` — `PopOutWindowService.PopOut` (Task 2, Step 3) already reassigns `document.GetTopLevel` when that happens, and this handler's `TopLevel.GetTopLevel(this)` call re-resolves correctly wherever the View currently lives, covering both.

- [ ] **Step 6: Wire real file opening into MainWindowViewModel**

In `Gibbed.TombRaider.DRMEdit/ViewModels/MainWindowViewModel.cs`, replace:

```csharp
        public void OpenFile(string path)
        {
            // FileViewerViewModel doesn't exist until Task 3 — DemoDocumentViewModel stands in for it here.
            AddDocument(new DemoDocumentViewModel(path));
        }
```

with:

```csharp
        public void OpenFile(string path)
        {
            AddDocument(new FileViewerViewModel(this, path));
        }
```

- [ ] **Step 7: Build and manually verify against real data**

Run: `dotnet build "Crystal Dynamics.sln" -c Debug`
Expected: 0 errors.

Run: `dotnet run --project Gibbed.TombRaider.DRMEdit/Gibbed.TombRaider.DRMEdit.csproj -- "<path to a real Tomb Raider Underworld .drm file>"`

Manually confirm:
- The DRM opens as a tab, tree populated with real sections, each row showing the `X8` id / type / hex-flags / byte-length format and the correct per-type icon (or blank for unrecognized types).
- Selecting a section and clicking "View Section Raw" opens a new tab (a placeholder is fine — `RawViewerViewModel`'s real content is Task 4) titled appropriately.
- Double-clicking a tree node does the same as "View Section".
- Popping that new tab out and back in still works (inherited from Task 2's generic mechanism — this specifically confirms it also works for a *real* document type, not just the demo).
- "Save DRM" prompts a save dialog and writes a `.drm` file (byte-diff it against the original — should be identical, since nothing was edited).

- [ ] **Step 8: Commit**

```bash
git add -A Gibbed.TombRaider.DRMEdit
git commit -m "DRMEdit (TR): real FileViewer (tree, icons, Save DRM, section opening)"
```

---

## Task 4: RawViewer with AvaloniaHex (Tomb Raider)

**Files:**
- Create: `Gibbed.TombRaider.DRMEdit/ViewModels/RawViewerViewModel.cs`
- Create: `Gibbed.TombRaider.DRMEdit/Views/RawViewerView.axaml`, `RawViewerView.axaml.cs`

**Interfaces:**
- Consumes: `DocumentTabViewModel` (Task 2), `DRM.Section` (existing).
- Produces: `RawViewerViewModel(DRM.Section section)` — `IBinaryDocument HexDocument` (observable), `string InfoText` (observable), `LoadFromFileCommand` (permanently disabled — see Global Constraints), `SaveToFileCommand`.

- [ ] **Step 1: Create RawViewerViewModel**

Create `Gibbed.TombRaider.DRMEdit/ViewModels/RawViewerViewModel.cs`:

```csharp
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using AvaloniaHex.Document;
using CommunityToolkit.Mvvm.Input;
using DRM = Gibbed.TombRaider.FileFormats.DRM;

namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    public partial class RawViewerViewModel : DocumentTabViewModel
    {
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        public partial IBinaryDocument HexDocument { get; set; }

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        public partial string InfoText { get; set; }

        private byte[] _data;

        public RawViewerViewModel(DRM.Section section)
        {
            Title = "Raw View: " + section.Id.ToString("X8");

            _data = new byte[section.Data.Length];
            section.Data.Read(_data, 0, _data.Length);

            InfoText = string.Format(
                "ID:\t{0:X8}\nType:\t{1}\nFilesize:\t{2}", section.Id, section.Type, section.Data.Length);

            HexDocument = new MemoryBinaryDocument(_data, isReadOnly: true);
        }

        [RelayCommand(CanExecute = nameof(CanLoadFromFile))]
        private void LoadFromFile()
        {
            // Permanently disabled — matches the original WinForms RawViewer, whose
            // loadFromFileButton was Enabled = false and never enabled anywhere.
        }

        private bool CanLoadFromFile() => false;

        [RelayCommand]
        private async Task SaveToFileAsync()
        {
            var fileTypes = new[] { FilePickerFileTypes.All };
            var savePath = await App.PickerService.SaveFileAsync(GetTopLevel?.Invoke(), "Save To File", fileTypes, null);
            if (savePath == null)
            {
                return;
            }

            await System.IO.File.WriteAllBytesAsync(savePath, _data);
        }
    }
}
```

- [ ] **Step 2: Create RawViewerView**

Create `Gibbed.TombRaider.DRMEdit/Views/RawViewerView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Gibbed.TombRaider.DRMEdit.ViewModels"
             xmlns:hex="clr-namespace:AvaloniaHex;assembly=AvaloniaHex"
             x:Class="Gibbed.TombRaider.DRMEdit.Views.RawViewerView"
             x:DataType="vm:RawViewerViewModel">

    <DockPanel>
        <ToolBar DockPanel.Dock="Top">
            <Button Content="Load From File" Command="{Binding LoadFromFileCommand}" />
            <Button Content="Save To File" Command="{Binding SaveToFileCommand}" />
        </ToolBar>

        <Grid RowDefinitions="200,4,*">
            <hex:HexEditor Grid.Row="0" Document="{Binding HexDocument}" />
            <GridSplitter Grid.Row="1" Height="4" HorizontalAlignment="Stretch" />
            <TabControl Grid.Row="2">
                <TabItem Header="File Info">
                    <TextBox Text="{Binding InfoText}" IsReadOnly="True" BorderThickness="0" />
                </TabItem>
                <TabItem Header="tabPage2">
                    <!-- Intentionally empty — matches the original WinForms RawViewer's
                         unused second tab (tabPage2 had no controls and was never wired up). -->
                </TabItem>
            </TabControl>
        </Grid>
    </DockPanel>

</UserControl>
```

Create `Gibbed.TombRaider.DRMEdit/Views/RawViewerView.axaml.cs`:

```csharp
using Avalonia.Controls;
using Gibbed.TombRaider.DRMEdit.ViewModels;

namespace Gibbed.TombRaider.DRMEdit.Views
{
    public partial class RawViewerView : UserControl
    {
        public RawViewerView()
        {
            InitializeComponent();

            AttachedToVisualTree += (_, _) =>
            {
                if (DataContext is RawViewerViewModel viewModel)
                {
                    viewModel.GetTopLevel = () => TopLevel.GetTopLevel(this);
                }
            };
        }
    }
}
```

- [ ] **Step 3: Build and manually verify against real data**

Run: `dotnet build "Crystal Dynamics.sln" -c Debug`
Expected: 0 errors.

Run: `dotnet run --project Gibbed.TombRaider.DRMEdit/Gibbed.TombRaider.DRMEdit.csproj -- "<path to a real .drm file>"`

Manually confirm:
- Opening a section as raw shows its actual bytes in the hex view, correct offset/hex/ASCII columns, and it's read-only (typing/editing does nothing).
- "File Info" tab shows the correct ID/Type/Filesize text; the second tab is present, empty, and unlabeled beyond "tabPage2" (matches today).
- "Load From File" button is visibly present but does nothing when clicked (permanently disabled, matching today).
- "Save To File" writes the section's exact bytes to disk — verify with a hash/diff against a known-good extraction if you have one handy.
- Pop the raw viewer out and back in.

- [ ] **Step 4: Commit**

```bash
git add -A Gibbed.TombRaider.DRMEdit
git commit -m "DRMEdit (TR): RawViewer via AvaloniaHex"
```

---

## Task 5 — CHECKPOINT: hand off to user for a look at the real UI before continuing

This is the natural stopping point the user asked for: core shell (menu, tabs, pop-out/pop-in) plus two of the three content types (file tree, raw hex view) are real and working end-to-end against real data. Texture viewing, dialogs, and the Deus Ex 3 mirror are all still ahead — better to get a look now than build all of that on top of an approach that might need adjusting.

- [ ] **Step 1: Build and launch for the user**

Run: `dotnet build "Crystal Dynamics.sln" -c Debug` — confirm 0 errors.

Hand off with something like: *"Core shell + FileViewer + RawViewer are working end-to-end. Want to open a real Underworld DRM, click around the tree, try raw-viewing a few sections, and test pop-out/pop-in, before I build out TextureViewer and the Deus Ex 3 mirror on top of this?"*

- [ ] **Step 2: Do not proceed to Task 6 until the user confirms**

If the user requests changes, make them here (in whichever of Tasks 1-4's files they touch) rather than carrying a known issue forward into the texture viewer and the Deus Ex 3 port, where the same pattern would need fixing twice.

---

## Task 6: TextureViewer (Tomb Raider)

**Files:**
- Create: `Gibbed.TombRaider.DRMEdit/ViewModels/TextureViewerViewModel.cs`
- Create: `Gibbed.TombRaider.DRMEdit/Views/TextureViewerView.axaml`, `TextureViewerView.axaml.cs`
- Modify: `Gibbed.TombRaider.DRMEdit/ViewModels/FileViewerViewModel.cs` (already references `TextureViewerViewModel` from Task 3 — no change needed here, just confirming it now compiles for real)

**Interfaces:**
- Consumes: `DocumentTabViewModel` (Task 2), `DRM.Section`/`PCD9File`/`PCD9.Format` (existing), `TextureCodec.DecompressImage`/`CompressImage` (existing, unchanged).
- Produces: `TextureViewerViewModel(DRM.Section section)` — `WriteableBitmap? Preview` (observable), `bool IsZoomed` (observable), `bool ShowAlpha` (observable), `string InfoText` (observable), `SaveCommand`, `SaveToFileCommand`, `LoadFromFileCommand`.

- [ ] **Step 1: Create TextureViewerViewModel**

Create `Gibbed.TombRaider.DRMEdit/ViewModels/TextureViewerViewModel.cs`:

```csharp
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using DRM = Gibbed.TombRaider.FileFormats.DRM;
using MsBox.Avalonia;
using Texture.BCnE.NET.Codec;
using MsBox.Avalonia.Enums;

namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    public partial class TextureViewerViewModel : DocumentTabViewModel
    {
        private readonly DRM.Section _section;
        private readonly FileFormats.PCD9File _texture;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        public partial WriteableBitmap Preview { get; set; }

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        public partial bool IsZoomed { get; set; }

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        public partial bool ShowAlpha { get; set; } = true;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        public partial string InfoText { get; set; }

        public TextureViewerViewModel(DRM.Section section)
        {
            _section = section;

            var texture = new FileFormats.PCD9File();
            texture.Deserialize(section.Data);
            _texture = texture;

            Title = "Texture: " + section.Id.ToString("X8");
            InfoText = string.Format(
                "{0} : {1}x{2} | Filesize: {3} Bytes",
                texture.Format, texture.Width, texture.Height, section.Data.Length);

            IsZoomed = texture.Width > 512 || texture.Height > 512;

            UpdatePreview();
        }

        partial void OnShowAlphaChanged(bool value) => UpdatePreview();

        private void UpdatePreview()
        {
            if (_texture.Mipmaps.Count == 0)
            {
                Preview = null;
                return;
            }

            var mip = _texture.Mipmaps[0];
            var width = (int)mip.Width;
            var height = (int)mip.Height;

            byte[] data = _texture.Format switch
            {
                FileFormats.PCD9.Format.A8R8G8B8 => mip.Data,
                FileFormats.PCD9.Format.DXT1 => TextureCodec.DecompressImage(mip.Data, width, height, TextureCodec.Flags.DXT1),
                FileFormats.PCD9.Format.DXT3 => TextureCodec.DecompressImage(mip.Data, width, height, TextureCodec.Flags.DXT3),
                FileFormats.PCD9.Format.DXT5 => TextureCodec.DecompressImage(mip.Data, width, height, TextureCodec.Flags.DXT5),
                _ => null,
            };

            if (data == null)
            {
                Preview = null;
                return;
            }

            var bitmap = new WriteableBitmap(
                new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);

            using (var buffer = bitmap.Lock())
            {
                unsafe
                {
                    var dst = (byte*)buffer.Address;
                    for (int i = 0; i < width * height; i++)
                    {
                        dst[i * 4 + 0] = data[i * 4 + 0];
                        dst[i * 4 + 1] = data[i * 4 + 1];
                        dst[i * 4 + 2] = data[i * 4 + 2];
                        dst[i * 4 + 3] = ShowAlpha == false ? (byte)0xFF : data[i * 4 + 3];
                    }
                }
            }

            Preview = bitmap;
        }

        [RelayCommand]
        private void Save()
        {
            _section.Data = (System.IO.MemoryStream)_texture.Serialize();
        }

        [RelayCommand]
        private async Task SaveToFileAsync()
        {
            if (Preview == null)
            {
                return;
            }

            var fileTypes = new[]
            {
                new FilePickerFileType("PNG Files") { Patterns = new[] { "*.png" } },
                FilePickerFileTypes.All,
            };
            var savePath = await App.PickerService.SaveFileAsync(GetTopLevel?.Invoke(), "Save To File", fileTypes, null);
            if (savePath == null)
            {
                return;
            }

            using (var output = System.IO.File.Create(savePath))
            {
                Preview.Save(output);
            }
        }

        [RelayCommand]
        private async Task LoadFromFileAsync()
        {
            var fileTypes = new[]
            {
                new FilePickerFileType("Image")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.tif", "*.tiff", "*.bmp" },
                },
                FilePickerFileTypes.All,
            };
            var paths = await App.PickerService.OpenFilesAsync(GetTopLevel?.Invoke(), "Load From File", fileTypes, false);
            var path = paths.Count > 0 ? paths[0] : null;
            if (path == null)
            {
                return;
            }

            try
            {
                ReplaceImage(path);
            }
            catch (Exception ex)
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Error", ex.Message, ButtonEnum.Ok, Icon.Error);
                await box.ShowAsync();
            }
        }

        private void ReplaceImage(string path)
        {
            using var bitmap = new Bitmap(path);

            if (bitmap.PixelSize.Width != _texture.Width || bitmap.PixelSize.Height != _texture.Height)
            {
                throw new FormatException("New texture must have the same size as the old one");
            }

            if (_texture.Mipmaps.Count > 1)
            {
                throw new NotSupportedException("Texture with multiple mipmaps not supported");
            }

            var width = bitmap.PixelSize.Width;
            var height = bitmap.PixelSize.Height;
            var mip = new byte[width * height * 4];

            using (var writeable = new WriteableBitmap(bitmap.PixelSize, bitmap.Dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul))
            {
                bitmap.CopyPixels(new PixelRect(0, 0, width, height), writeable.Lock().Address, mip.Length, width * 4);
                // CopyPixels above writes into the locked buffer directly; read it back out below.
            }

            using (var locked = new WriteableBitmap(bitmap.PixelSize, bitmap.Dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul).Lock())
            {
                bitmap.CopyPixels(new PixelRect(0, 0, width, height), locked.Address, mip.Length, width * 4);
                System.Runtime.InteropServices.Marshal.Copy(locked.Address, mip, 0, mip.Length);
            }

            switch (_texture.Format)
            {
                case FileFormats.PCD9.Format.A8R8G8B8:
                    _texture.Mipmaps[0].Data = mip;
                    break;
                case FileFormats.PCD9.Format.DXT1:
                    _texture.Mipmaps[0].Data = TextureCodec.CompressImage(mip, width, height, TextureCodec.Flags.DXT1);
                    break;
                case FileFormats.PCD9.Format.DXT3:
                    _texture.Mipmaps[0].Data = TextureCodec.CompressImage(mip, width, height, TextureCodec.Flags.DXT3);
                    break;
                case FileFormats.PCD9.Format.DXT5:
                    _texture.Mipmaps[0].Data = TextureCodec.CompressImage(mip, width, height, TextureCodec.Flags.DXT5);
                    break;
            }

            UpdatePreview();
        }
    }
}
```

The `ReplaceImage` pixel read-back has one redundant `WriteableBitmap` construction above (copy-pixels-then-immediately-discard) — clean that up in Step 1a below before building, it was left in to show the two candidate approaches; only the second (`locked.Address` + `Marshal.Copy`) is correct and needed.

- [ ] **Step 1a: Remove the redundant WriteableBitmap in ReplaceImage**

In `TextureViewerViewModel.cs`, delete this block (the first of the two `using (var writeable = ...)`/`using (var locked = ...)` blocks in `ReplaceImage`):

```csharp
            using (var writeable = new WriteableBitmap(bitmap.PixelSize, bitmap.Dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul))
            {
                bitmap.CopyPixels(new PixelRect(0, 0, width, height), writeable.Lock().Address, mip.Length, width * 4);
                // CopyPixels above writes into the locked buffer directly; read it back out below.
            }

```

leaving only the `locked` block that actually populates `mip`.

- [ ] **Step 2: Create TextureViewerView**

Create `Gibbed.TombRaider.DRMEdit/Views/TextureViewerView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Gibbed.TombRaider.DRMEdit.ViewModels"
             x:Class="Gibbed.TombRaider.DRMEdit.Views.TextureViewerView"
             x:DataType="vm:TextureViewerViewModel">

    <DockPanel>
        <ToolBar DockPanel.Dock="Top">
            <Button Content="Save" Command="{Binding SaveCommand}" />
            <Separator />
            <Button Content="Load From File" Command="{Binding LoadFromFileCommand}" />
            <Button Content="Save To File" Command="{Binding SaveToFileCommand}" />
            <Separator />
            <ToggleButton Content="Zoom" IsChecked="{Binding IsZoomed}" />
            <ToggleButton Content="Show Alpha" IsChecked="{Binding ShowAlpha}" />
        </ToolBar>

        <TextBlock DockPanel.Dock="Bottom" Text="{Binding InfoText}" Margin="4" />

        <ScrollViewer>
            <Image Source="{Binding Preview}"
                   Stretch="{Binding IsZoomed, Converter={x:Static local:BoolToStretchConverter.Instance}}" />
        </ScrollViewer>
    </DockPanel>

</UserControl>
```

Add the missing `xmlns:local` to the root `UserControl` tag:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Gibbed.TombRaider.DRMEdit.ViewModels"
             xmlns:local="using:Gibbed.TombRaider.DRMEdit.Views"
             x:Class="Gibbed.TombRaider.DRMEdit.Views.TextureViewerView"
             x:DataType="vm:TextureViewerViewModel">
```

Create `Gibbed.TombRaider.DRMEdit/Views/TextureViewerView.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Gibbed.TombRaider.DRMEdit.ViewModels;

namespace Gibbed.TombRaider.DRMEdit.Views
{
    public partial class TextureViewerView : UserControl
    {
        public TextureViewerView()
        {
            InitializeComponent();

            AttachedToVisualTree += (_, _) =>
            {
                if (DataContext is TextureViewerViewModel viewModel)
                {
                    viewModel.GetTopLevel = () => TopLevel.GetTopLevel(this);
                }
            };
        }
    }

    public class BoolToStretchConverter : IValueConverter
    {
        public static readonly BoolToStretchConverter Instance = new();

        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => (value is true) ? Stretch.Uniform : Stretch.None;

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new System.NotSupportedException();
    }
}
```

`IsZoomed` true → `Stretch="Uniform"` (fit-to-view, matches `PictureBoxSizeMode.Zoom`); false → `Stretch="None"` (natural size inside the `ScrollViewer`, matches `PictureBoxSizeMode.Normal` + the original `previewPanel.AutoScroll`).

- [ ] **Step 3: Build and manually verify against real data**

Run: `dotnet build "Crystal Dynamics.sln" -c Debug`
Expected: 0 errors.

Run: `dotnet run --project Gibbed.TombRaider.DRMEdit/Gibbed.TombRaider.DRMEdit.csproj -- "<path to a real .drm file>"`

Manually confirm, against real `RenderResource` sections (DXT1 and DXT5, if the file has both — the earlier manual DRMEdit validation session in this project used `Texture 00000125` DXT1 512x512 and `Texture 00000128` DXT5 512x512 from a real Underworld `.drm`, both good candidates):
- Double-clicking a `RenderResource` tree node opens a `TextureViewer` tab (not raw) showing the correctly decoded image.
- Zoom toggle switches between fit-to-view and natural size with a scrollbar.
- Show Alpha toggle visibly changes transparency rendering.
- Save To File writes a valid PNG.
- Load From File + a same-size replacement image updates the preview; a different-size image shows an error dialog (via `MessageBox.Avalonia`) instead of crashing.
- Save writes the (possibly replaced) texture bytes back into the in-memory section; confirm via the owning file's Save DRM afterward that the change round-trips.

- [ ] **Step 4: Commit**

```bash
git add -A Gibbed.TombRaider.DRMEdit
git commit -m "DRMEdit (TR): TextureViewer (WriteableBitmap preview, zoom/alpha, replace)"
```

---

## Task 7: Program.cs unrecognized-option/help dialog verification + final polish pass (Tomb Raider)

**Files:**
- Verify only (no changes expected unless the build/manual pass surfaces something): `Gibbed.TombRaider.DRMEdit/Program.cs`, `App.axaml.cs`

**Interfaces:** none new.

- [ ] **Step 1: Verify CLI help/error paths still work under the Avalonia bootstrap**

Run: `dotnet run --project Gibbed.TombRaider.DRMEdit/Gibbed.TombRaider.DRMEdit.csproj -- -h`
Expected: a `MessageBox.Avalonia` dialog with the usage text appears (Icon.Info), and the app exits cleanly after it's dismissed — no `MainWindow` ever appears.

Run: `dotnet run --project Gibbed.TombRaider.DRMEdit/Gibbed.TombRaider.DRMEdit.csproj -- --bogus`
Expected: a `MessageBox.Avalonia` dialog (Icon.Error) showing the unrecognized-option message, same clean exit.

- [ ] **Step 2: Full solution build check**

Run: `dotnet build "Crystal Dynamics.sln" -c Debug`
Expected: 0 errors. Note any warnings — fix real ones now (unused usings, nullable warnings from the new `Nullable=enable` setting this project now has that others don't) rather than carrying them into Task 8's final checkpoint.

- [ ] **Step 3: Commit if anything changed**

```bash
git add -A Gibbed.TombRaider.DRMEdit
git commit -m "DRMEdit (TR): fix warnings surfaced by full build"
```

If nothing needed changing, skip the commit.

---

## Task 8 — CHECKPOINT: full Tomb Raider interactive verification (blocks merge)

**Files:** none — this is a verification task only.

- [ ] **Step 1: Run the full checklist from the spec's Testing section against real Tomb Raider Underworld data**

- Open real DRM/CDRM files, walk the section tree.
- Open Raw-viewer tabs and Texture-viewer tabs; confirm DXT1/DXT5 render correctly; confirm zoom + alpha toggle work.
- Exercise pop-out/pop-in on at least one tab of each kind (file, raw, texture).
- Verify Save DRM round-trips (open → save to a new path → reopen the saved copy → same tree).
- Verify Raw and Texture Save-to-file / Load-from-file paths.
- Verify `-h`/unrecognized-option dialogs (Task 7).

- [ ] **Step 2: Get explicit user sign-off before Task 9 (the Deus Ex 3 mirror) starts**

Do not proceed to Task 9 until the user confirms Tomb Raider is good. This is the Global Constraints' merge-blocking gate for the Tomb Raider half of this stage.

---

## Task 9: Strip WinForms, scaffold the Avalonia app shell + core MVVM infrastructure (Deus Ex 3)

Mirrors Task 1 + Task 2, combined (Deus Ex 3 only needs structural verification per Global Constraints, so the lower review bar means one task here instead of two).

**Files:**
- Modify: `Gibbed.DeusEx3.DRMEdit/Gibbed.DeusEx3.DRMEdit.csproj` (full rewrite)
- Create: `Gibbed.DeusEx3.DRMEdit/app.manifest`
- Create: `Gibbed.DeusEx3.DRMEdit/Program.cs` (full rewrite)
- Create: `Gibbed.DeusEx3.DRMEdit/App.axaml`, `App.axaml.cs`
- Create: `Gibbed.DeusEx3.DRMEdit/ViewLocator.cs`
- Create: `Gibbed.DeusEx3.DRMEdit/ViewModels/ViewModelBase.cs`, `DocumentTabViewModel.cs`, `MainWindowViewModel.cs`
- Create: `Gibbed.DeusEx3.DRMEdit/Services/IFilePickerService.cs`, `AvaloniaFilePickerService.cs`, `IPopOutWindowService.cs`, `PopOutWindowService.cs`
- Create: `Gibbed.DeusEx3.DRMEdit/Views/MainWindow.axaml`/`.cs`, `PopOutWindow.axaml`/`.cs`
- Move: `Gibbed.DeusEx3.DRMEdit/SectionTypeImages/*.png` → `Gibbed.DeusEx3.DRMEdit/Assets/Icons/*.png`
- Delete: `Explorer.cs`, `Explorer.Designer.cs`, `Explorer.resx`, `FileViewer.cs`, `FileViewer.Designer.cs`, `FileViewer.resx`, `RawViewer.cs`, `RawViewer.Designer.cs`, `RawViewer.resx`, `TextureViewer.cs`, `TextureViewer.Designer.cs`, `TextureViewer.resx`, `ISectionViewer.cs`, `SectionTypeImages.Designer.cs`, `SectionTypeImages.resx`, `app.config`, `Properties/AssemblyInfo.cs`

**Interfaces:** identical shapes to Tasks 1-2, in the `Gibbed.DeusEx3.DRMEdit` namespace.

- [ ] **Step 1: Delete old files, move icons**

```bash
git rm Gibbed.DeusEx3.DRMEdit/Explorer.cs Gibbed.DeusEx3.DRMEdit/Explorer.Designer.cs Gibbed.DeusEx3.DRMEdit/Explorer.resx
git rm Gibbed.DeusEx3.DRMEdit/FileViewer.cs Gibbed.DeusEx3.DRMEdit/FileViewer.Designer.cs Gibbed.DeusEx3.DRMEdit/FileViewer.resx
git rm Gibbed.DeusEx3.DRMEdit/RawViewer.cs Gibbed.DeusEx3.DRMEdit/RawViewer.Designer.cs Gibbed.DeusEx3.DRMEdit/RawViewer.resx
git rm Gibbed.DeusEx3.DRMEdit/TextureViewer.cs Gibbed.DeusEx3.DRMEdit/TextureViewer.Designer.cs Gibbed.DeusEx3.DRMEdit/TextureViewer.resx
git rm Gibbed.DeusEx3.DRMEdit/ISectionViewer.cs
git rm Gibbed.DeusEx3.DRMEdit/SectionTypeImages.Designer.cs Gibbed.DeusEx3.DRMEdit/SectionTypeImages.resx
git rm Gibbed.DeusEx3.DRMEdit/app.config
git rm Gibbed.DeusEx3.DRMEdit/Properties/AssemblyInfo.cs

mkdir -p Gibbed.DeusEx3.DRMEdit/Assets/Icons
git mv "Gibbed.DeusEx3.DRMEdit/SectionTypeImages/__DRM.png" Gibbed.DeusEx3.DRMEdit/Assets/Icons/__DRM.png
git mv "Gibbed.DeusEx3.DRMEdit/SectionTypeImages/RenderResource.png" Gibbed.DeusEx3.DRMEdit/Assets/Icons/RenderResource.png
git mv "Gibbed.DeusEx3.DRMEdit/SectionTypeImages/Script.png" Gibbed.DeusEx3.DRMEdit/Assets/Icons/Script.png
git mv "Gibbed.DeusEx3.DRMEdit/SectionTypeImages/Wave.png" Gibbed.DeusEx3.DRMEdit/Assets/Icons/Wave.png
rmdir "Gibbed.DeusEx3.DRMEdit/SectionTypeImages" 2>/dev/null || true
```

- [ ] **Step 2: Rewrite the csproj**

Same shape as Task 1 Step 3, with these substitutions: `RootNamespace`/`AssemblyName` → `Gibbed.DeusEx3.DRMEdit`, `OutputPath` → `..\bin_dx3\` (both Debug and Release — check the original csproj for the Release path, since Tomb Raider's DRMEdit uses `bin_tr_release` but confirm Deus Ex 3's pattern from its existing csproj before assuming `bin_dx3_release` — if the original only defined a Debug `OutputPath` block, as `Test.csproj` did, keep it that way, don't add a Release block that wasn't there), `ProjectReference`s → `Gibbed.DeusEx3.FileFormats.csproj` instead of `Gibbed.TombRaider.FileFormats.csproj`. Same package set and versions.

- [ ] **Step 3: Create app.manifest**

Same as Task 1 Step 4, with `name="Gibbed.DeusEx3.DRMEdit.Desktop"`.

- [ ] **Step 4: Rewrite Program.cs**

Same shape as Task 1 Step 5, `namespace Gibbed.DeusEx3.DRMEdit`, same CLI parsing logic verbatim (it was already namespace-identical apart from the enclosing `namespace` line — confirm by diffing against the original `Gibbed.DeusEx3.DRMEdit/Program.cs` before deleting it in Step 1, since NDesk.Options usage might have Deus Ex 3-specific option entries beyond `-h|--help` that must be preserved).

- [ ] **Step 5: Create App.axaml / App.axaml.cs, ViewLocator.cs, ViewModelBase.cs**

Same as Task 1 Steps 6-8, `Gibbed.DeusEx3.DRMEdit` namespace throughout.

- [ ] **Step 6: Create DocumentTabViewModel, MainWindowViewModel, IFilePickerService/AvaloniaFilePickerService, IPopOutWindowService/PopOutWindowService, PopOutWindow, MainWindow**

Same as Task 2 Steps 1, 2, 3, 4, 5, 7, 8 — `Gibbed.DeusEx3.DRMEdit` namespace throughout, `OpenFile` still references `FileViewerViewModel` (created in Task 10 — for this task, stub it with the same temporary `DemoDocumentViewModel`/`DemoDocumentView` pattern from Task 2 Step 6, to be deleted in Task 10 exactly as Task 3 Step 1 did for Tomb Raider). Window title "Deus Ex: Human Revolution DRM Editor" is a reasonable placeholder — check the original `Explorer.Designer.cs`'s `this.Text` value for Deus Ex 3 before assuming this, and use whatever it actually says.

- [ ] **Step 7: Build and smoke-test**

Run: `dotnet build "Crystal Dynamics.sln" -c Debug`
Expected: 0 errors.

Run: `dotnet run --project Gibbed.DeusEx3.DRMEdit/Gibbed.DeusEx3.DRMEdit.csproj`
Expected: window opens, File→Open shows a picker, demo tab opens/pops-out/pops-in/closes correctly, Close All works. Close the app. (No real DX3 data needed for this task — that's Task 12.)

- [ ] **Step 8: Commit**

```bash
git add -A Gibbed.DeusEx3.DRMEdit
git commit -m "DRMEdit (DX3): strip WinForms, scaffold Avalonia shell + core MVVM infrastructure"
```

---

## Task 10: FileViewer (with type filter) + RawViewer (Deus Ex 3)

Mirrors Tasks 3 + 4, combined, with the Deus Ex 3-specific differences from Global Constraints: no Save DRM capability (permanently-disabled button, matching the original's `saveDRMButton.Enabled = false`), and a section-type filter `ComboBox` that Tomb Raider's `FileViewer` doesn't have.

**Files:**
- Create: `Gibbed.DeusEx3.DRMEdit/ViewModels/SectionNode.cs`, `FileViewerViewModel.cs`, `RawViewerViewModel.cs`
- Create: `Gibbed.DeusEx3.DRMEdit/Views/FileViewerView.axaml`/`.cs`, `RawViewerView.axaml`/`.cs`
- Modify: `Gibbed.DeusEx3.DRMEdit/ViewModels/MainWindowViewModel.cs` (`OpenFile` → real `FileViewerViewModel`)
- Delete: `Gibbed.DeusEx3.DRMEdit/ViewModels/DemoDocumentViewModel.cs`, `Gibbed.DeusEx3.DRMEdit/Views/DemoDocumentView.axaml`/`.cs`

**Interfaces:** same shapes as Tasks 3-4, `Gibbed.DeusEx3.FileFormats` types (`DRMFile`, `DRM.Section`, `DRM.SectionType`) in place of the Tomb Raider ones. `FileViewerViewModel` additionally exposes `ObservableCollection<string> TypeFilterOptions` and `string SelectedTypeFilter` (observable) — `"All"` plus every `DRM.SectionType` enum value name, matching the original `PopulateTypeFilter`.

- [ ] **Step 1: Delete the demo document**

```bash
git rm Gibbed.DeusEx3.DRMEdit/ViewModels/DemoDocumentViewModel.cs
git rm Gibbed.DeusEx3.DRMEdit/Views/DemoDocumentView.axaml Gibbed.DeusEx3.DRMEdit/Views/DemoDocumentView.axaml.cs
```

- [ ] **Step 2: Create SectionNode**

Same as Task 3 Step 2, `using DRM = Gibbed.DeusEx3.FileFormats.DRM;`.

- [ ] **Step 3: Create FileViewerViewModel — no Save DRM, with the type filter**

Create `Gibbed.DeusEx3.DRMEdit/ViewModels/FileViewerViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using DRM = Gibbed.DeusEx3.FileFormats.DRM;

namespace Gibbed.DeusEx3.DRMEdit.ViewModels
{
    public partial class FileViewerViewModel : DocumentTabViewModel
    {
        private readonly MainWindowViewModel _owner;
        private readonly FileFormats.DRMFile _fileData;

        public ObservableCollection<SectionNode> Sections { get; } = new();
        public ObservableCollection<string> TypeFilterOptions { get; } = new();

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        public partial SectionNode SelectedSection { get; set; }

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        public partial string SelectedTypeFilter { get; set; } = "All";

        public FileViewerViewModel(MainWindowViewModel owner, string path)
        {
            _owner = owner;
            Title = "DRM View: " + Path.GetFileName(path);

            using (var input = File.OpenRead(path))
            {
                var data = new FileFormats.DRMFile();
                data.Deserialize(input);
                _fileData = data;
            }

            TypeFilterOptions.Add("All");
            foreach (DRM.SectionType type in System.Enum.GetValues(typeof(DRM.SectionType)))
            {
                TypeFilterOptions.Add(type.ToString());
            }

            RebuildTree();
        }

        partial void OnSelectedTypeFilterChanged(string value) => RebuildTree();

        private void RebuildTree()
        {
            Sections.Clear();

            var showAll = SelectedTypeFilter == "All";
            var filterType = showAll
                ? default
                : (DRM.SectionType)System.Enum.Parse(typeof(DRM.SectionType), SelectedTypeFilter);

            foreach (var section in _fileData.Sections.OrderBy(s => s.Id))
            {
                if (showAll == false && section.Type != filterType)
                {
                    continue;
                }
                Sections.Add(new SectionNode(section));
            }
        }

        // No SaveDrmCommand: Gibbed.DeusEx3.FileFormats.DRMFile has no Serialize() method.
        // The original WinForms saveDRMButton exists but is permanently Enabled = false —
        // there is deliberately no equivalent command exposed here at all, rather than a
        // command that does nothing, since there's no toolbar button bound to it to explain
        // why (Deus Ex 3's FileViewerView keeps a disabled-looking button purely for visual
        // parity — see the View below).

        [RelayCommand]
        private void ViewSection() => OpenSection(SelectedSection, false);

        [RelayCommand]
        private void ViewSectionRaw() => OpenSection(SelectedSection, true);

        public void OpenSection(SectionNode node, bool forceRaw)
        {
            if (node == null)
            {
                return;
            }

            var section = node.Section;
            if (section.Data != null)
            {
                section.Data.Seek(0, System.IO.SeekOrigin.Begin);
            }

            DocumentTabViewModel viewer = forceRaw == true
                ? new RawViewerViewModel(section)
                : (section.Type == DRM.SectionType.RenderResource
                    ? new TextureViewerViewModel(section)
                    : new RawViewerViewModel(section));

            _owner.AddDocument(viewer);
        }
    }
}
```

- [ ] **Step 4: Create FileViewerView with the disabled Save DRM button and the type filter**

Create `Gibbed.DeusEx3.DRMEdit/Views/FileViewerView.axaml` — same tree/icon structure as Tomb Raider's (Task 3 Step 5), but the toolbar differs:

```xml
        <ToolBar DockPanel.Dock="Top">
            <Button Content="Save DRM" IsEnabled="False" ToolTip.Tip="Not supported for Deus Ex 3 DRM files" />
            <ComboBox ItemsSource="{Binding TypeFilterOptions}" SelectedItem="{Binding SelectedTypeFilter}" Width="140" />
            <Separator />
            <Button Content="View Section" Command="{Binding ViewSectionCommand}" />
            <Button Content="View Section Raw" Command="{Binding ViewSectionRawCommand}" />
        </ToolBar>
```

(Rest of the file — icon resources/converter, `TreeView`, code-behind — identical in shape to Task 3 Step 5, `Gibbed.DeusEx3.DRMEdit` namespace and asset URIs.)

- [ ] **Step 5: Create RawViewerViewModel + RawViewerView**

Identical to Task 4 Steps 1-2, `Gibbed.DeusEx3.DRMEdit` namespace and `DRM = Gibbed.DeusEx3.FileFormats.DRM`.

- [ ] **Step 6: Wire real file opening into MainWindowViewModel**

Same edit as Task 3 Step 6.

- [ ] **Step 7: Build**

Run: `dotnet build "Crystal Dynamics.sln" -c Debug`
Expected: 0 errors. (No manual data-driven test yet — that's Task 12, per Global Constraints' structural-only bar for Deus Ex 3. It's fine to spot-check with real DX3 data here if it's on hand, but not required to pass this task.)

- [ ] **Step 8: Commit**

```bash
git add -A Gibbed.DeusEx3.DRMEdit
git commit -m "DRMEdit (DX3): FileViewer (type filter, no Save DRM) + RawViewer"
```

---

## Task 11: TextureViewer — view-only (Deus Ex 3)

Mirrors Task 6, but per Global Constraints, Deus Ex 3's `TextureViewer` has **no** load/replace/save-back — view + zoom + alpha toggle + save-to-PNG only.

**Files:**
- Create: `Gibbed.DeusEx3.DRMEdit/ViewModels/TextureViewerViewModel.cs`
- Create: `Gibbed.DeusEx3.DRMEdit/Views/TextureViewerView.axaml`/`.cs`

**Interfaces:**
- Produces: `TextureViewerViewModel(DRM.Section section)` — `WriteableBitmap? Preview`, `bool IsZoomed`, `bool ShowAlpha`, `string InfoText`, `SaveToFileCommand` only (no `SaveCommand`, no `LoadFromFileCommand`).

- [ ] **Step 1: Create TextureViewerViewModel**

Same shape as Task 6 Step 1's `UpdatePreview`/constructor, with `Save`, `SaveToFileAsync`'s file-type list unchanged, but **remove** `SaveCommand` and `LoadFromFileAsync`/`ReplaceImage` entirely — Deus Ex 3's original `TextureViewer.cs` has no `Section` field stored and no such methods. `InfoText` format also differs slightly (no filesize suffix in the original DX3 version — `"{0} : {1}x{2}"` only, confirmed from the DX3 `TextureViewer.cs` read during planning):

```csharp
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using DRM = Gibbed.DeusEx3.FileFormats.DRM;
using Texture.BCnE.NET.Codec;

namespace Gibbed.DeusEx3.DRMEdit.ViewModels
{
    public partial class TextureViewerViewModel : DocumentTabViewModel
    {
        private readonly FileFormats.PCD9File _texture;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        public partial WriteableBitmap Preview { get; set; }

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        public partial bool IsZoomed { get; set; }

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        public partial bool ShowAlpha { get; set; } = true;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        public partial string InfoText { get; set; }

        public TextureViewerViewModel(DRM.Section section)
        {
            var texture = new FileFormats.PCD9File();
            texture.Deserialize(section.Data);
            _texture = texture;

            Title = "Texture: " + section.Id.ToString("X8");
            InfoText = string.Format("{0} : {1}x{2}", texture.Format, texture.Width, texture.Height);

            IsZoomed = texture.Width > 512 || texture.Height > 512;

            UpdatePreview();
        }

        partial void OnShowAlphaChanged(bool value) => UpdatePreview();

        private void UpdatePreview()
        {
            if (_texture.Mipmaps.Count == 0)
            {
                Preview = null;
                return;
            }

            var mip = _texture.Mipmaps[0];
            var width = (int)mip.Width;
            var height = (int)mip.Height;

            byte[] data = _texture.Format switch
            {
                FileFormats.PCD9.Format.A8R8G8B8 => mip.Data,
                FileFormats.PCD9.Format.DXT1 => TextureCodec.DecompressImage(mip.Data, width, height, TextureCodec.Flags.DXT1),
                FileFormats.PCD9.Format.DXT3 => TextureCodec.DecompressImage(mip.Data, width, height, TextureCodec.Flags.DXT3),
                FileFormats.PCD9.Format.DXT5 => TextureCodec.DecompressImage(mip.Data, width, height, TextureCodec.Flags.DXT5),
                _ => null,
            };

            if (data == null)
            {
                Preview = null;
                return;
            }

            var bitmap = new WriteableBitmap(
                new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);

            using (var buffer = bitmap.Lock())
            {
                unsafe
                {
                    var dst = (byte*)buffer.Address;
                    for (int i = 0; i < width * height; i++)
                    {
                        dst[i * 4 + 0] = data[i * 4 + 0];
                        dst[i * 4 + 1] = data[i * 4 + 1];
                        dst[i * 4 + 2] = data[i * 4 + 2];
                        dst[i * 4 + 3] = ShowAlpha == false ? (byte)0xFF : data[i * 4 + 3];
                    }
                }
            }

            Preview = bitmap;
        }

        [RelayCommand]
        private async Task SaveToFileAsync()
        {
            if (Preview == null)
            {
                return;
            }

            var fileTypes = new[]
            {
                new FilePickerFileType("PNG Files") { Patterns = new[] { "*.png" } },
                FilePickerFileTypes.All,
            };
            var savePath = await App.PickerService.SaveFileAsync(GetTopLevel?.Invoke(), "Save To File", fileTypes, null);
            if (savePath == null)
            {
                return;
            }

            using (var output = System.IO.File.Create(savePath))
            {
                Preview.Save(output);
            }
        }
    }
}
```

- [ ] **Step 2: Create TextureViewerView — same layout as Tomb Raider's minus the Save/Load buttons**

Same as Task 6 Step 2's `.axaml`/`.axaml.cs`, but the `ToolBar` only has:

```xml
        <ToolBar DockPanel.Dock="Top">
            <Button Content="Save To File" Command="{Binding SaveToFileCommand}" />
            <Separator />
            <ToggleButton Content="Zoom" IsChecked="{Binding IsZoomed}" />
            <ToggleButton Content="Show Alpha" IsChecked="{Binding ShowAlpha}" />
        </ToolBar>
```

- [ ] **Step 3: Build**

Run: `dotnet build "Crystal Dynamics.sln" -c Debug`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add -A Gibbed.DeusEx3.DRMEdit
git commit -m "DRMEdit (DX3): TextureViewer (view-only — no save/load/replace)"
```

---

## Task 12 — CHECKPOINT: Deus Ex 3 structural verification (blocks merge) + wrap-up

**Files:**
- Modify: `CLAUDE.md` (append the three carried-over gaps from the spec, matching the existing `PCD9File` TODO's style)

**Interfaces:** none new.

- [ ] **Step 1: Full solution build**

Run: `dotnet build "Crystal Dynamics.sln" -c Debug -t:Rebuild`
Expected: 0 errors, 0 warnings (fix any real warnings now — this is the last chance before merge).

- [ ] **Step 2: Launch and close cleanly (structural bar only, per Global Constraints)**

Run: `dotnet run --project Gibbed.DeusEx3.DRMEdit/Gibbed.DeusEx3.DRMEdit.csproj`
Expected: window opens without exception, title matches the original app's window text, File→Open shows a picker. Close it — no exception on shutdown. No requirement to open real Deus Ex 3 data or walk sections/textures for this task.

- [ ] **Step 3: Cross-platform publish smoke check, both projects**

Run:
```bash
dotnet publish Gibbed.TombRaider.DRMEdit/Gibbed.TombRaider.DRMEdit.csproj -r osx-arm64 --self-contained -c Debug
dotnet publish Gibbed.DeusEx3.DRMEdit/Gibbed.DeusEx3.DRMEdit.csproj -r osx-arm64 --self-contained -c Debug
```
Expected: both succeed. This is a build-level check only, per the spec — no macOS execution testing without Mac hardware.

- [ ] **Step 4: Document the three carried-over gaps in CLAUDE.md**

Add a new subsection under "Known Issues / Low-Priority TODOs" in `CLAUDE.md`, matching the existing `PCD9File` entry's style:

```markdown
### DRMEdit Avalonia migration — three gaps carried over unchanged (found 2026-08-14, Stage 2B)

While porting `Gibbed.TombRaider.DRMEdit`/`Gibbed.DeusEx3.DRMEdit` from WinForms to Avalonia
(`DRMEdit-ReplaceHexEditor` branch, see `docs/superpowers/specs/2026-08-14-drmedit-avalonia-migration-design.md`),
three pre-existing behavioral gaps were deliberately preserved rather than fixed, per explicit
instruction to document them for later review instead of silently reproducing them:

1. **No error handling on corrupt/malformed DRM load** — `FileViewerViewModel`'s constructor
   (like the original `FileViewer.LoadResource`) has no try/catch around `DRMFile.Deserialize`;
   a bad file crashes rather than showing an error.
2. **Exception-type change in the "unsupported image format" catch** — the original WinForms
   `catch (OutOfMemoryException)` around `Image.FromFile` relied on a GDI+ idiosyncrasy specific
   to System.Drawing. Avalonia's `Bitmap` loader doesn't replicate it, so
   `TextureViewerViewModel.LoadFromFileAsync` catches general `Exception` instead — same
   user-facing message, different underlying exception type.
3. **`TextureViewerViewModel.ReplaceImage`'s same-size-only / single-mipmap-only restrictions**
   (Tomb Raider only — Deus Ex 3's TextureViewer never had replace support) — already flagged
   in `drmCompressionHandling.md` §4 as a real gap (no texture-upscaling support); still
   unaddressed.
```

- [ ] **Step 5: Commit**

```bash
git add CLAUDE.md
git commit -m "DRMEdit: document three carried-over gaps from the Avalonia migration"
```

- [ ] **Step 6: Get explicit user sign-off, then hand off to superpowers:finishing-a-development-branch**

Both checkpoints (Task 8 for Tomb Raider, this task for Deus Ex 3) must be confirmed by the user before merging `DRMEdit-ReplaceHexEditor` back into `cross-platform-NET10`. Once confirmed, use `superpowers:finishing-a-development-branch` to decide how to integrate — do not merge unilaterally.

---

## Self-Review

**Spec coverage:** Decision 1 (flat tabs + pop-out/pop-in, hover/selected visibility, always-visible pop-in, no cascade close on file-tab-closes-viewer-tabs) → Tasks 2, 3. Decision 2 (duplicated per-project) → Tasks 9-12 mirror rather than share. Decision 3 (DX3 structural-only) → Global Constraints + Tasks 9-12 vs. Task 8. Decision 4 (MVVM via CommunityToolkit.Mvvm) → every ViewModel task. Decision 5 (Material Icons) → Task 2 Step 7, Task 2 Step 4. Architecture's bootstrap/window-shape/pop-out-mechanism → Tasks 1-2. Component mapping table → Tasks 3, 4, 6 (TR) and 10, 11 (DX3), each with the noted quirks (blank `hintLabel`, empty `tabPage2`, disabled `loadFromFileButton`/DX3 `saveDRMButton`) explicitly preserved. Data flow (shared mutable `Section` reference, `//TODO` comment kept) → Task 6 Step 1's `Save()`. Known gaps → Task 12 Step 4. Testing plan → Tasks 5, 8, 12.

**Placeholder scan:** no TBD/TODO-as-a-plan-gap found; the one `//TODO` in generated code is the *original author's* comment being deliberately preserved, not a plan placeholder. Task 6 Step 1 flagged its own redundant code and fixed it in Step 1a rather than leaving it as dead weight.

**Type consistency:** `DocumentTabViewModel`'s `GetTopLevel`/`RequestPopOut`/`RequestPopIn`/`RequestClose`/`PopOutCommand`/`PopInCommand`/`CloseCommand`/`Close()` (Task 2) are used identically by `MainWindowViewModel` (Task 2), every View's code-behind (Tasks 3, 4, 6, 10, 11), and `PopOutWindowService` (Task 2) — checked consistent throughout. `IFilePickerService.OpenFilesAsync`/`SaveFileAsync` signatures (Task 2) match every call site (`MainWindowViewModel.OpenAsync`, `FileViewerViewModel.SaveDrmAsync`, `RawViewerViewModel.SaveToFileAsync`, `TextureViewerViewModel.SaveToFileAsync`/`LoadFromFileAsync`) via the `App.PickerService` static accessor (Task 3 Step 4). `IPopOutWindowService.PopOut`/`Close` signatures match their `MainWindowViewModel` call sites. `SectionNode` (Task 3) is consumed identically by `FileViewerView`'s `TreeView.ItemTemplate` in both Tomb Raider and Deus Ex 3 variants.

Execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?
