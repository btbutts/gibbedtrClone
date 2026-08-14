# DRMEdit UI Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current functionally-complete-but-visually-flat DRMEdit UI (both `Gibbed.TombRaider.DRMEdit` and `Gibbed.DeusEx3.DRMEdit`) with a native Windows 11 Fluent/Mica chrome design in the style of Notepad — tabs integrated into the title bar, Mica/Acrylic backdrop, a shared toolbar row whose buttons change based on the active tab — while keeping the UI's structure and code identical across Windows and macOS except for the one region every cross-platform app varies: window-control-button style and placement.

**Architecture:** Two new pieces of shared infrastructure per project: a `ToolbarViewLocator` (mirrors the existing `ViewLocator`, resolves `XxxViewModel` → `XxxToolbar` instead of `XxxView`) so `MainWindow`'s toolbar region can show different buttons per active tab without any per-tab-type branching; and a platform-swapped pair of title-bar-button `UserControl`s (`WindowsTitleBarButtons` / `MacTitleBarButtons`), chosen once at window construction via `RuntimeInformation.IsOSPlatform`. Each of the three viewer types (`FileViewer`, `RawViewer`, `TextureViewer`) splits its existing embedded toolbar `StackPanel` out into a new paired `XxxToolbar` view, leaving the original `XxxView` as content-only.

**Tech Stack:** Same as the DRMEdit Avalonia migration (`docs/superpowers/plans/2026-08-14-drmedit-avalonia-migration.md`) — no new packages. Uses `Avalonia`'s `WindowTransparencyLevel.Mica`/`AcrylicBlur`/`Blur` fallback chain, `ExtendClientAreaToDecorationsHint`, and Fluent theme's `SystemControlBackgroundChromeMediumBrush`/`SystemControlBackgroundChromeMediumLowBrush`/`SystemControlPageBackgroundChromeLowBrush` resources (confirmed present in Avalonia.Themes.Fluent's `Accents/BaseResources.xaml` — these are the long-standing UWP-era "chrome elevation" tokens Fluent still ships).

**Spec:** This document (no separate spec — the design brief came directly from the user's description of the target, verified against real Avalonia 12.1.1 APIs via reflection on the installed assemblies and a real working sample, `FrankenApps/Avalonia-CustomTitleBarTemplate`).

## Global Constraints

- Both `Gibbed.TombRaider.DRMEdit` and `Gibbed.DeusEx3.DRMEdit` get this treatment, kept structurally identical (per the existing "duplicated per-project" precedent from the Avalonia migration plan) — only the platform-conditional title-bar-button pair differs, and that pair's *code* is identical between the two projects too, just namespaced separately.
- **No platform branching in the actual DRMEdit UI** — tabs, toolbar, content, Mica backdrop are 100% shared code/XAML across Windows/macOS/Linux. The *only* platform-conditional piece is which small title-bar-button `UserControl` gets instantiated, and its position (`DockPanel.Dock="Right"` on Windows, `"Left"` on macOS, matching each OS's native window-control convention).
- Do not hardcode colors. Use Fluent's own theme resources (`DynamicResource`) so the UI keeps following the user's OS accent/light-dark setting automatically, exactly as it does today.
- `WindowTransparencyLevel` is set as a fallback list (`Mica, AcrylicBlur, Blur, None`) — never assume Mica is available; `ActualTransparencyLevel` degrades automatically per-platform, no code branching needed for the backdrop itself.
- macOS rendering is **not verified in this pass** — the user has Mac hardware and will validate the macOS title-bar-button variant and Mica/Blur fallback later via VS Code Remote. Build the macOS variant correctly by documented convention (traffic-light colors/sizes/left-position matching real macOS apps), flag it clearly as unverified, same as other macOS gaps already tracked in `CLAUDE.md`.
- Existing functional behavior (every command, every dialog, every gap intentionally preserved in the migration plan) must not change — this is a visual/structural pass only. No new features, no behavior changes to any button's action.
- Checkpoint after Phase 1 (Tomb Raider only) before replicating to Deus Ex 3, matching the pattern the original migration plan used — the user needs to actually see this rendered before it's worth propagating to a second project.

---

## Phase 1: Tomb Raider — shared infrastructure + MainWindow chrome

### Task 1: ToolbarViewLocator + title-bar-button pair

**Files:**
- Create: `Gibbed.TombRaider.DRMEdit/ToolbarViewLocator.cs`
- Create: `Gibbed.TombRaider.DRMEdit/Views/TitleBar/WindowsTitleBarButtons.axaml`, `.axaml.cs`
- Create: `Gibbed.TombRaider.DRMEdit/Views/TitleBar/MacTitleBarButtons.axaml`, `.axaml.cs`

**Interfaces:**
- Produces: `ToolbarViewLocator : IDataTemplate` — `Match(object? data) => data is ViewModelBase`, `Build` replaces `"ViewModel"` with `"Toolbar"` in the full type name (same convention as `ViewLocator`, different suffix). Not registered globally in `Application.DataTemplates` — set as the explicit `ContentTemplate` on the one `ContentControl` that needs it (Task 2), so it never competes with the main `ViewLocator` for content-area resolution.
- Produces: `WindowsTitleBarButtons`/`MacTitleBarButtons : UserControl` — no bindings; each exposes three `Button`s (`Name="MinimizeButton"`, `"MaximizeButton"`, `"CloseButton"`) wired in code-behind to `((Window)TopLevel.GetTopLevel(this)!).WindowState = WindowState.Minimized` etc. and `.Close()`. Windows variant: square, right-aligned-in-its-own-container, hover backgrounds (`#22FFFFFF` neutral hover, red-on-close-hover), 46px wide matching the real Windows convention. macOS variant: 12px circular, red/yellow/green, left-aligned, icons only visible on container hover (matching real macOS behavior where the glyphs inside the dots are hidden until you mouse over the traffic-light cluster).

- [ ] **Step 1: Create ToolbarViewLocator**

```csharp
using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Gibbed.TombRaider.DRMEdit.ViewModels;

namespace Gibbed.TombRaider.DRMEdit
{
    public class ToolbarViewLocator : IDataTemplate
    {
        public Control? Build(object? param)
        {
            if (param == null)
            {
                return null;
            }

            var name = param.GetType().FullName!.Replace("ViewModel", "Toolbar", StringComparison.Ordinal);
            var type = Type.GetType(name);

            return type != null ? (Control)Activator.CreateInstance(type)! : null;
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}
```

Returning `null` (not a "Not Found" placeholder like `ViewLocator`) when no `XxxToolbar` type exists yet is deliberate — during this phase, most viewer types won't have a `Toolbar` counterpart until Task 4, and an empty toolbar region is the correct interim state, not an error message.

- [ ] **Step 2: Create WindowsTitleBarButtons**

Create `Gibbed.TombRaider.DRMEdit/Views/TitleBar/WindowsTitleBarButtons.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Gibbed.TombRaider.DRMEdit.Views.TitleBar.WindowsTitleBarButtons">
    <StackPanel Orientation="Horizontal" Height="32">
        <Button Name="MinimizeButton" Width="46" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" BorderThickness="0" CornerRadius="0" Background="Transparent" ToolTip.Tip="Minimize">
            <Button.Styles>
                <Style Selector="Button:pointerover /template/ ContentPresenter">
                    <Setter Property="Background" Value="#22FFFFFF" />
                </Style>
            </Button.Styles>
            <Path Data="M0,0 L10,0" Stroke="{DynamicResource SystemControlForegroundBaseHighBrush}" StrokeThickness="1" Width="10" Height="1" />
        </Button>
        <Button Name="MaximizeButton" Width="46" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" BorderThickness="0" CornerRadius="0" Background="Transparent" ToolTip.Tip="Maximize">
            <Button.Styles>
                <Style Selector="Button:pointerover /template/ ContentPresenter">
                    <Setter Property="Background" Value="#22FFFFFF" />
                </Style>
            </Button.Styles>
            <Rectangle Width="10" Height="10" Stroke="{DynamicResource SystemControlForegroundBaseHighBrush}" StrokeThickness="1" />
        </Button>
        <Button Name="CloseButton" Width="46" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" BorderThickness="0" CornerRadius="0" Background="Transparent" ToolTip.Tip="Close">
            <Button.Styles>
                <Style Selector="Button:pointerover /template/ ContentPresenter">
                    <Setter Property="Background" Value="#E81123" />
                </Style>
                <Style Selector="Button:pointerover Path">
                    <Setter Property="Stroke" Value="White" />
                </Style>
            </Button.Styles>
            <Path Data="M0,0 L10,10 M0,10 L10,0" Stroke="{DynamicResource SystemControlForegroundBaseHighBrush}" StrokeThickness="1" Width="10" Height="10" />
        </Button>
    </StackPanel>
</UserControl>
```

Create `Gibbed.TombRaider.DRMEdit/Views/TitleBar/WindowsTitleBarButtons.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Input;

namespace Gibbed.TombRaider.DRMEdit.Views.TitleBar
{
    public partial class WindowsTitleBarButtons : UserControl
    {
        public WindowsTitleBarButtons()
        {
            InitializeComponent();

            this.FindControl<Button>("MinimizeButton")!.Click += (_, _) => OwnerWindow()!.WindowState = WindowState.Minimized;
            this.FindControl<Button>("MaximizeButton")!.Click += (_, _) => OwnerWindow()!.WindowState =
                OwnerWindow()!.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            this.FindControl<Button>("CloseButton")!.Click += (_, _) => OwnerWindow()!.Close();
        }

        private Window? OwnerWindow() => TopLevel.GetTopLevel(this) as Window;
    }
}
```

- [ ] **Step 3: Create MacTitleBarButtons**

Create `Gibbed.TombRaider.DRMEdit/Views/TitleBar/MacTitleBarButtons.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Gibbed.TombRaider.DRMEdit.Views.TitleBar.MacTitleBarButtons">
    <StackPanel Orientation="Horizontal" Spacing="8" Margin="12,0,0,0" Height="32" VerticalAlignment="Center" Name="Root">
        <StackPanel.Styles>
            <Style Selector="StackPanel#Root:pointerover Path">
                <Setter Property="IsVisible" Value="True" />
            </Style>
            <Style Selector="StackPanel#Root:not(:pointerover) Path">
                <Setter Property="IsVisible" Value="False" />
            </Style>
        </StackPanel.Styles>
        <Button Name="CloseButton" Width="12" Height="12" CornerRadius="6" Padding="0" Background="#FF5F57" BorderThickness="0" ToolTip.Tip="Close">
            <Path Data="M0,0 L6,6 M0,6 L6,0" Stroke="#4D0000" StrokeThickness="1" Width="6" Height="6" />
        </Button>
        <Button Name="MinimizeButton" Width="12" Height="12" CornerRadius="6" Padding="0" Background="#FEBC2E" BorderThickness="0" ToolTip.Tip="Minimize">
            <Path Data="M0,3 L6,3" Stroke="#985712" StrokeThickness="1" Width="6" Height="1" />
        </Button>
        <Button Name="MaximizeButton" Width="12" Height="12" CornerRadius="6" Padding="0" Background="#28C840" BorderThickness="0" ToolTip.Tip="Maximize">
            <Path Data="M0,6 L4,6 L4,2 M6,0 L6,4 L2,4" Stroke="#0A630C" StrokeThickness="1" Width="6" Height="6" />
        </Button>
    </StackPanel>
</UserControl>
```

Create `Gibbed.TombRaider.DRMEdit/Views/TitleBar/MacTitleBarButtons.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace Gibbed.TombRaider.DRMEdit.Views.TitleBar
{
    public partial class MacTitleBarButtons : UserControl
    {
        public MacTitleBarButtons()
        {
            InitializeComponent();

            this.FindControl<Button>("CloseButton")!.Click += (_, _) => OwnerWindow()!.Close();
            this.FindControl<Button>("MinimizeButton")!.Click += (_, _) => OwnerWindow()!.WindowState = WindowState.Minimized;
            this.FindControl<Button>("MaximizeButton")!.Click += (_, _) => OwnerWindow()!.WindowState =
                OwnerWindow()!.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private Window? OwnerWindow() => TopLevel.GetTopLevel(this) as Window;
    }
}
```

**Not runtime-verified on macOS** — built to documented macOS conventions (12px circles, red/yellow/green, glyphs hidden until hover, 8px spacing) but flagged for real validation once the user's Mac is used for that pass.

- [ ] **Step 4: Build**

Run: `dotnet build "Gibbed.TombRaider.DRMEdit/Gibbed.TombRaider.DRMEdit.csproj" -c Debug`
Expected: 0 errors. (These new files aren't wired into `MainWindow` yet — Task 2 does that — so this just confirms they compile standalone.)

- [ ] **Step 5: Commit**

```bash
git add Gibbed.TombRaider.DRMEdit/ToolbarViewLocator.cs Gibbed.TombRaider.DRMEdit/Views/TitleBar
git commit -m "DRMEdit (TR): ToolbarViewLocator + platform title-bar-button pair"
```

---

### Task 2: MainWindow chrome rewrite — Mica, extended title bar, tab strip, toolbar row

**Files:**
- Modify: `Gibbed.TombRaider.DRMEdit/Views/MainWindow.axaml`, `MainWindow.axaml.cs`

**Interfaces:**
- Consumes: `ToolbarViewLocator` (Task 1), `WindowsTitleBarButtons`/`MacTitleBarButtons` (Task 1), existing `MainWindowViewModel.OpenDocuments`/`SelectedDocument`/`OpenCommand`/`CloseAllCommand` (unchanged).
- No ViewModel changes in this task — pure View-layer restructuring.

- [ ] **Step 1: Rewrite MainWindow.axaml**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Gibbed.TombRaider.DRMEdit.ViewModels"
        xmlns:materialIcons="clr-namespace:Material.Icons.Avalonia;assembly=Material.Icons.Avalonia"
        xmlns:local="using:Gibbed.TombRaider.DRMEdit"
        x:Class="Gibbed.TombRaider.DRMEdit.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Width="1100" Height="750"
        Background="Transparent"
        TransparencyLevelHint="Mica,AcrylicBlur,Blur,None"
        ExtendClientAreaToDecorationsHint="True"
        ExtendClientAreaTitleBarHeightHint="40"
        Title="Tomb Raider DRM Editor">

    <DockPanel>

        <!-- Title bar row: tab strip + app identity + window controls -->
        <Grid DockPanel.Dock="Top" Height="40" Background="{DynamicResource SystemControlBackgroundChromeMediumBrush}"
              Name="TitleBarRow">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>

            <ScrollViewer Grid.Column="0" HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Disabled"
                          Name="TabScrollViewer">
                <ListBox ItemsSource="{Binding OpenDocuments}"
                         SelectedItem="{Binding SelectedDocument}"
                         Background="Transparent"
                         BorderThickness="0"
                         Padding="4,0">
                    <ListBox.ItemsPanel>
                        <ItemsPanelTemplate>
                            <StackPanel Orientation="Horizontal" />
                        </ItemsPanelTemplate>
                    </ListBox.ItemsPanel>
                    <ListBox.Styles>
                        <Style Selector="ListBoxItem">
                            <Setter Property="Padding" Value="0" />
                            <Setter Property="Margin" Value="2,4,0,4" />
                            <Setter Property="CornerRadius" Value="6,6,0,0" />
                        </Style>
                        <Style Selector="ListBoxItem:selected /template/ ContentPresenter">
                            <Setter Property="Background" Value="{DynamicResource SystemControlPageBackgroundChromeLowBrush}" />
                        </Style>
                        <Style Selector="ListBoxItem:not(:selected) /template/ ContentPresenter">
                            <Setter Property="Background" Value="Transparent" />
                        </Style>
                    </ListBox.Styles>
                    <ListBox.ItemTemplate>
                        <DataTemplate DataType="vm:DocumentTabViewModel">
                            <Grid ColumnDefinitions="Auto,Auto,Auto" Height="32" Margin="8,0">
                                <TextBlock Grid.Column="0" Text="{Binding Title}" VerticalAlignment="Center"
                                           MaxWidth="180" TextTrimming="CharacterEllipsis" Margin="0,0,8,0" />
                                <Button Grid.Column="1" Command="{Binding PopOutCommand}" Classes="tabPopOut"
                                        Background="Transparent" BorderThickness="0" Padding="4"
                                        ToolTip.Tip="Pop out into its own window">
                                    <materialIcons:MaterialIcon Kind="Export" Width="14" Height="14" />
                                </Button>
                                <Button Grid.Column="2" Command="{Binding CloseCommand}"
                                        Background="Transparent" BorderThickness="0" Padding="4"
                                        ToolTip.Tip="Close">
                                    <materialIcons:MaterialIcon Kind="Close" Width="14" Height="14" />
                                </Button>
                            </Grid>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
            </ScrollViewer>

            <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Stretch" Name="WindowControlsHost" />
        </Grid>

        <!-- Toolbar row: static File/Windows/Open DRM + dynamic per-tab actions -->
        <Grid DockPanel.Dock="Top" Background="{DynamicResource SystemControlBackgroundChromeMediumLowBrush}">
            <StackPanel Orientation="Horizontal" Spacing="4" Margin="6,4">
                <Menu Background="Transparent">
                    <MenuItem Header="_File">
                        <MenuItem Header="_Open" Command="{Binding OpenCommand}" InputGesture="Ctrl+O" />
                        <Separator />
                        <MenuItem Header="E_xit" Click="OnExitClick" />
                    </MenuItem>
                    <MenuItem Header="_Windows">
                        <MenuItem Header="C_lose All" Command="{Binding CloseAllCommand}" />
                    </MenuItem>
                </Menu>
                <Separator />
                <Button Content="Open DRM" Command="{Binding OpenCommand}" />
                <Separator />
                <ContentControl Content="{Binding SelectedDocument}">
                    <ContentControl.ContentTemplate>
                        <local:ToolbarViewLocator />
                    </ContentControl.ContentTemplate>
                </ContentControl>
            </StackPanel>
        </Grid>

        <!-- Content -->
        <ContentControl Content="{Binding SelectedDocument}" Background="{DynamicResource SystemControlPageBackgroundChromeLowBrush}" />

    </DockPanel>

</Window>
```

- [ ] **Step 2: Rewrite MainWindow.axaml.cs — platform-conditional window controls, title-bar drag region**

```csharp
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Interactivity;
using Gibbed.TombRaider.DRMEdit.ViewModels;
using Gibbed.TombRaider.DRMEdit.Views.TitleBar;

namespace Gibbed.TombRaider.DRMEdit.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            var controls = this.FindControl<StackPanel>("WindowControlsHost")!;

            if (isMac)
            {
                controls.Children.Add(new MacTitleBarButtons());
                DockPanel.SetDock(controls, Dock.Left);
            }
            else
            {
                controls.Children.Add(new WindowsTitleBarButtons());
            }

            var titleBarRow = this.FindControl<Grid>("TitleBarRow")!;
            WindowDecorationProperties.SetElementRole(titleBarRow, Avalonia.Input.WindowDecorationsElementRole.TitleBar);
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

        private void OnExitClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
```

Marking the whole `TitleBarRow` grid with `WindowDecorationsElementRole.TitleBar` makes it draggable by default; the `ListBox` (tabs) and the window-control buttons still capture their own clicks normally since Avalonia only treats *unhandled* pointer presses in a `TitleBar`-marked region as a drag — this matches how the reference sample's `IsHitTestVisible="False"` background layer achieves the same effect, just via the newer 12.x API instead of the hit-test-visibility trick.

- [ ] **Step 3: Build and manually verify**

Run: `dotnet build "Gibbed.TombRaider.DRMEdit/Gibbed.TombRaider.DRMEdit.csproj" -c Debug`
Expected: 0 errors.

Run: `dotnet run --project Gibbed.TombRaider.DRMEdit/Gibbed.TombRaider.DRMEdit.csproj -- "<real .drm>"`

Manually confirm: Mica/blur backdrop visible behind the title bar, tabs render inline with window controls, dragging the title bar area (not over a tab/button) moves the window, minimize/maximize/close all work, opening a second file adds a second tab, toolbar row shows File/Windows/Open DRM (dynamic per-tab buttons will be empty until Task 4).

- [ ] **Step 4: Commit**

```bash
git add Gibbed.TombRaider.DRMEdit/Views/MainWindow.axaml Gibbed.TombRaider.DRMEdit/Views/MainWindow.axaml.cs
git commit -m "DRMEdit (TR): MainWindow chrome rewrite -- Mica, title-bar tabs, toolbar row"
```

---

### Task 3: PopOutWindow chrome — same Mica/toolbar treatment, no tab strip

**Files:**
- Modify: `Gibbed.TombRaider.DRMEdit/Views/PopOutWindow.axaml`, `PopOutWindow.axaml.cs`

- [ ] **Step 1: Rewrite PopOutWindow.axaml**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Gibbed.TombRaider.DRMEdit.ViewModels"
        xmlns:materialIcons="clr-namespace:Material.Icons.Avalonia;assembly=Material.Icons.Avalonia"
        xmlns:local="using:Gibbed.TombRaider.DRMEdit"
        x:Class="Gibbed.TombRaider.DRMEdit.Views.PopOutWindow"
        x:DataType="vm:DocumentTabViewModel"
        Width="700" Height="500"
        Background="Transparent"
        TransparencyLevelHint="Mica,AcrylicBlur,Blur,None"
        ExtendClientAreaToDecorationsHint="True"
        ExtendClientAreaTitleBarHeightHint="40"
        Title="{Binding Title}">

    <DockPanel>
        <Grid DockPanel.Dock="Top" Height="40" Background="{DynamicResource SystemControlBackgroundChromeMediumBrush}"
              Name="TitleBarRow">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <TextBlock Grid.Column="0" Text="{Binding Title}" VerticalAlignment="Center" Margin="12,0" />
            <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Stretch" Name="WindowControlsHost" />
        </Grid>

        <Grid DockPanel.Dock="Top" Background="{DynamicResource SystemControlBackgroundChromeMediumLowBrush}">
            <StackPanel Orientation="Horizontal" Spacing="4" Margin="6,4">
                <Button Command="{Binding PopInCommand}" ToolTip.Tip="Dock back into main window">
                    <materialIcons:MaterialIcon Kind="DockWindow" />
                </Button>
                <Separator />
                <ContentControl Content="{Binding}">
                    <ContentControl.ContentTemplate>
                        <local:ToolbarViewLocator />
                    </ContentControl.ContentTemplate>
                </ContentControl>
            </StackPanel>
        </Grid>

        <ContentControl Content="{Binding}" Background="{DynamicResource SystemControlPageBackgroundChromeLowBrush}" />
    </DockPanel>

</Window>
```

- [ ] **Step 2: Rewrite PopOutWindow.axaml.cs**

```csharp
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Gibbed.TombRaider.DRMEdit.Views.TitleBar;

namespace Gibbed.TombRaider.DRMEdit.Views
{
    public partial class PopOutWindow : Window
    {
        public PopOutWindow()
        {
            InitializeComponent();

            var isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            var controls = this.FindControl<StackPanel>("WindowControlsHost")!;

            if (isMac)
            {
                controls.Children.Add(new MacTitleBarButtons());
                DockPanel.SetDock(controls, Dock.Left);
            }
            else
            {
                controls.Children.Add(new WindowsTitleBarButtons());
            }

            var titleBarRow = this.FindControl<Grid>("TitleBarRow")!;
            WindowDecorationProperties.SetElementRole(titleBarRow, Avalonia.Input.WindowDecorationsElementRole.TitleBar);
        }
    }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build "Gibbed.TombRaider.DRMEdit/Gibbed.TombRaider.DRMEdit.csproj" -c Debug`
Expected: 0 errors. Manually confirm a popped-out tab shows the same Mica chrome, title text, working drag, and dock-window button.

- [ ] **Step 4: Commit**

```bash
git add Gibbed.TombRaider.DRMEdit/Views/PopOutWindow.axaml Gibbed.TombRaider.DRMEdit/Views/PopOutWindow.axaml.cs
git commit -m "DRMEdit (TR): PopOutWindow chrome -- same Mica/toolbar treatment, no tab strip"
```

---

### Task 4: Split each viewer's embedded toolbar into a paired Toolbar view

**Files:**
- Create: `Gibbed.TombRaider.DRMEdit/Views/FileViewerToolbar.axaml`, `.axaml.cs`
- Create: `Gibbed.TombRaider.DRMEdit/Views/RawViewerToolbar.axaml`, `.axaml.cs`
- Create: `Gibbed.TombRaider.DRMEdit/Views/TextureViewerToolbar.axaml`, `.axaml.cs`
- Modify: `Gibbed.TombRaider.DRMEdit/Views/FileViewerView.axaml`, `RawViewerView.axaml`, `TextureViewerView.axaml` (remove the embedded toolbar `StackPanel`, keep content only)

**Interfaces:** each `XxxToolbar` is a `UserControl` with `x:DataType="vm:XxxViewModel"` — the exact same `x:DataType` its paired `XxxView` already uses, bound to the exact same commands.

- [ ] **Step 1: Extract FileViewerToolbar**

Create `Gibbed.TombRaider.DRMEdit/Views/FileViewerToolbar.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Gibbed.TombRaider.DRMEdit.ViewModels"
             x:Class="Gibbed.TombRaider.DRMEdit.Views.FileViewerToolbar"
             x:DataType="vm:FileViewerViewModel">
    <StackPanel Orientation="Horizontal" Spacing="4">
        <Button Content="Save DRM" Command="{Binding SaveDrmCommand}" />
        <Separator />
        <Button Content="View Section" Command="{Binding ViewSectionCommand}" />
        <Button Content="View Section Raw" Command="{Binding ViewSectionRawCommand}" />
    </StackPanel>
</UserControl>
```

Create `Gibbed.TombRaider.DRMEdit/Views/FileViewerToolbar.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace Gibbed.TombRaider.DRMEdit.Views
{
    public partial class FileViewerToolbar : UserControl
    {
        public FileViewerToolbar()
        {
            InitializeComponent();
        }
    }
}
```

In `FileViewerView.axaml`, remove the `<StackPanel DockPanel.Dock="Top" ...>` toolbar block (the one with Save DRM/View Section/View Section Raw) entirely, leaving the `TreeView` as the `DockPanel`'s only remaining child (or the sole content, dropping the now-unnecessary `DockPanel` wrapper if it was only there to dock the toolbar).

- [ ] **Step 2: Extract RawViewerToolbar**

Create `Gibbed.TombRaider.DRMEdit/Views/RawViewerToolbar.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Gibbed.TombRaider.DRMEdit.ViewModels"
             x:Class="Gibbed.TombRaider.DRMEdit.Views.RawViewerToolbar"
             x:DataType="vm:RawViewerViewModel">
    <StackPanel Orientation="Horizontal" Spacing="4">
        <Button Content="Load From File" Command="{Binding LoadFromFileCommand}" />
        <Button Content="Save To File" Command="{Binding SaveToFileCommand}" />
    </StackPanel>
</UserControl>
```

Create `Gibbed.TombRaider.DRMEdit/Views/RawViewerToolbar.axaml.cs` (same trivial shape as Step 1's code-behind). Remove the equivalent `StackPanel` from `RawViewerView.axaml`, leaving the `Grid` with `HexEditor`/`GridSplitter`/`TabControl` as the top-level content.

- [ ] **Step 3: Extract TextureViewerToolbar**

Create `Gibbed.TombRaider.DRMEdit/Views/TextureViewerToolbar.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Gibbed.TombRaider.DRMEdit.ViewModels"
             x:Class="Gibbed.TombRaider.DRMEdit.Views.TextureViewerToolbar"
             x:DataType="vm:TextureViewerViewModel">
    <StackPanel Orientation="Horizontal" Spacing="4">
        <Button Content="Save" Command="{Binding SaveCommand}" />
        <Separator />
        <Button Content="Load From File" Command="{Binding LoadFromFileCommand}" />
        <Button Content="Save To File" Command="{Binding SaveToFileCommand}" />
        <Separator />
        <ToggleButton Content="Zoom" IsChecked="{Binding IsZoomed}" />
        <ToggleButton Content="Show Alpha" IsChecked="{Binding ShowAlpha}" />
    </StackPanel>
</UserControl>
```

Create `Gibbed.TombRaider.DRMEdit/Views/TextureViewerToolbar.axaml.cs` (same trivial shape). Remove the equivalent `StackPanel` from `TextureViewerView.axaml`; keep the `InfoText` `TextBlock` and the `ScrollViewer`/`Image` — `InfoText` stays in the content view (it's status text, not an action), not the toolbar.

- [ ] **Step 4: Build and manually verify against real data**

Run: `dotnet build "Gibbed.TombRaider.DRMEdit/Gibbed.TombRaider.DRMEdit.csproj" -c Debug`
Expected: 0 errors.

Run: `dotnet run --project Gibbed.TombRaider.DRMEdit/Gibbed.TombRaider.DRMEdit.csproj -- "<real .drm>"`

Manually confirm: opening a file tab shows Save DRM/View Section/View Section Raw in the shared toolbar row (not embedded in the content below); switching to a raw-view tab swaps the toolbar to Load From File/Save To File; switching to a texture-view tab swaps it again to Save/Load/Save To File/Zoom/Show Alpha; all buttons still do exactly what they did before.

- [ ] **Step 5: Commit**

```bash
git add -A Gibbed.TombRaider.DRMEdit
git commit -m "DRMEdit (TR): hoist per-viewer toolbars into the shared dynamic toolbar row"
```

---

## Phase 1 CHECKPOINT — user visual review before replicating to Deus Ex 3

Hand off to the user: build, run, visually review the new chrome (Mica backdrop, title-bar tabs, dynamic toolbar, drag/minimize/maximize/close behavior) against real Tomb Raider data. Do not start Phase 2 until confirmed — this is the same expensive-to-redo-if-wrong risk the original migration plan's Task 5 checkpoint was guarding against, just for visual design instead of functional architecture.

---

## Phase 2: Deus Ex 3 — replicate the confirmed design

Mirror every task above into `Gibbed.DeusEx3.DRMEdit`, `Gibbed.DeusEx3.DRMEdit` namespace throughout, with the same real differences already established in the Avalonia migration preserved (no `SaveDrmCommand` on `FileViewerToolbar` — instead a permanently-disabled "Save DRM" button plus the type-filter `ComboBox`; `TextureViewerToolbar` has only Save To File/Zoom/Show Alpha, no Save/Load From File). Window title stays `"DRM Editor"`.

- [ ] Task 5: ToolbarViewLocator + title-bar-button pair (DX3) — identical to Task 1, `Gibbed.DeusEx3.DRMEdit` namespace.
- [ ] Task 6: MainWindow chrome rewrite (DX3) — identical to Task 2, `Title="DRM Editor"` (not "Tomb Raider DRM Editor").
- [ ] Task 7: PopOutWindow chrome (DX3) — identical to Task 3.
- [ ] Task 8: Split FileViewer/RawViewer/TextureViewer toolbars (DX3) — same shape as Task 4, `FileViewerToolbar` keeps the disabled Save DRM button + `ComboBox` (moved from the old embedded toolbar, not dropped), `TextureViewerToolbar` has no Save/Load From File.

Combine Tasks 5-8 into one commit, matching the batching precedent from the original migration's Tasks 9-11.

- [ ] **Final build + smoke test**: `dotnet build "Crystal Dynamics.sln" -c Debug` (0 warnings/0 errors across the whole solution), launch both DRMEdit exes, confirm no crash.

## Follow-on (not this pass)

- Real macOS validation of `MacTitleBarButtons`, Mica-fallback rendering, and drag/traffic-light behavior — deferred until the user's Mac is used for that pass (VS Code Remote, per the user's stated plan).
- Segoe UI Variable / system-font matching was discussed but is **not** part of this plan — revisit only if the user asks; `Avalonia.Fonts.Inter` stays as-is unless flagged as a real mismatch after the visual checkpoint.
