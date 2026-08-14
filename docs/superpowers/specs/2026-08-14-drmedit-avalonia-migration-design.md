# DRMEdit Avalonia Migration — Design Spec

**Branch:** `DRMEdit-ReplaceHexEditor` (off `cross-platform-NET10`)
**Stage:** Stage 2B of `multi-platform-retarget.md`
**Date:** 2026-08-14

## Context

`multi-platform-retarget.md` is retargeting the whole Gibbed Tomb Raider toolchain to
.NET 10 for cross-platform (Windows + macOS) support. `Gibbed.TombRaider.DRMEdit` and
`Gibbed.DeusEx3.DRMEdit` are WinForms apps, and WinForms has no macOS implementation at
all — unlike other Windows-only spots in this codebase (a stray `Registry.GetValue` call,
a native P/Invoke), this isn't something that can be guarded behind a platform check. The
whole UI shell has to be replaced.

This is explicitly **not** a deprecation of DRMEdit's GUI in favor of a future Python
wrapper. DRMEdit keeps functioning as a full, standalone, cross-platform application on
both the Tomb Raider and Deus Ex 3 project trees indefinitely (Tomb Raider stays priority
if the two ever trade off). A future CLI/headless invocation surface for the Python
wrapper is separate follow-on work (Stage 3 item 9) built on top of what this stage
produces, not a replacement for it.

The concrete blocker this stage removes: `Be.Windows.Forms.HexBox` (WinForms-hosted) and
the WinForms shell around it (`Explorer`, `FileViewer`, `RawViewer`, `TextureViewer`).
Investigated during Stage 2 and confirmed here again: no available `Be.Windows.Forms.HexBox`
package (original, `.Net5`, or `.Net8` — all checked against real source) is cross-platform
anyway, so there was never a version of "just swap the hex control" that solved the actual
problem.

**Out of scope for this stage:** `Gibbed.Squish`'s native `squish_32.dll`/`squish_64.dll`
P/Invoke texture decode stays exactly as-is (Windows-only for now — that's Stage 3 item 8).
Extracting a headless/CLI invocation surface for the future Python wrapper is Stage 3 item
9, not here.

## Decisions made during brainstorming (with rationale)

1. **MDI replacement: flat tabbed document interface with pop-out/pop-in.** WinForms MDI
   (`Explorer` as an `IsMdiContainer`, `FileViewer`/`RawViewer`/`TextureViewer` as MDI
   children, Cascade/Tile/Arrange Icons menu) has no Avalonia equivalent. Every opened
   item — each DRM file *and* each opened section viewer — gets its own top-level tab in
   one `TabControl` on one `MainWindow`, exactly mirroring today's flat MDI-child list
   (not nested under the file that spawned it, which would be a real functional
   narrowing). To avoid losing today's "view several things side by side" capability
   (the one thing tabs alone can't do that floating MDI children could), every tab gets a
   pop-out button that detaches it into its own real OS window; a pop-in button sends it
   back. Cascade/Tile Vertical/Tile Horizontal/Arrange Icons menu items are dropped — they
   have no coherent meaning without floating child windows to arrange.
2. **Code sharing: kept duplicated per-project.** `Gibbed.TombRaider.DRMEdit` and
   `Gibbed.DeusEx3.DRMEdit` are structurally identical today but share no UI library —
   that precedent is kept rather than introducing a new abstraction over their divergent
   `FileFormats` types (`DRM.Section`/`PCD9File` vs. their DeusEx3 equivalents) in the same
   stage as the framework swap.
3. **DX3 verification depth: structural-only for this stage.** Tomb Raider gets full
   interactive validation against real data before merge; Deus Ex 3 only needs to build
   clean and launch/close without crashing. Real DX3 functional-parity validation is
   deferred to whenever DX3 is actually prioritized.
4. **UI code style: proper MVVM**, using `CommunityToolkit.Mvvm` (not `ReactiveUI` — no
   reactive-programming precedent in this codebase, and CommunityToolkit's source-generator
   approach has a much shallower learning curve). Chosen deliberately over the lower-risk
   "code-behind, same as today" option after the trade-off was explained.
5. **Icons**: `Material.Icons.Avalonia` (confirmed on NuGet, 3.0.2). Pop-out uses
   `MaterialIconKind.Export`, pop-in uses `MaterialIconKind.DockWindow` — both confirmed to
   exist in the underlying Pictogrammers MDI icon set the package generates its enum from.

## Architecture

**Bootstrap**: `Program.cs` keeps its NDesk.Options CLI parsing (help text, unrecognized-
option detection) byte-for-byte identical to today and to the other 5 tools' convention —
it runs before Avalonia starts. `[STAThread]` stays. `Application.Run(new Explorer(extras))`
is replaced with `AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args)`.
The parsed `extras` (startup file paths) feed into `MainWindowViewModel` the same way they
fed `Explorer`'s constructor loop today.

**MVVM toolkit**: `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`).

**Window/tab shape**: one `MainWindow` with one `TabControl` bound to
`MainWindowViewModel.OpenDocuments` (a collection of `DocumentTabViewModel`-derived
instances: `FileViewerViewModel`, `RawViewerViewModel`, `TextureViewerViewModel`). Avalonia
`DataTemplate`s per ViewModel type resolve which View renders — standard Avalonia MVVM,
not new machinery. `SectionType` → viewer selection logic is unchanged from today's
`FileViewer.GetViewer` switch.

**Pop-out/pop-in mechanism**: state lives entirely on the ViewModel, never the View, so
detaching a tab is just moving its ViewModel between two collections — no manual Control
reparenting.
- `PopOutCommand` removes the ViewModel from `OpenDocuments` and adds it to
  `PoppedOutDocuments`. A window-management piece in `App.axaml.cs` reacts to that
  collection change and opens a real `Window` whose `Content` binds to that ViewModel —
  the same `DataTemplate` resolution renders the same kind of view, just hosted in a
  `Window` instead of a `TabItem`, with no `MainWindow` chrome (menu/toolbar) duplicated.
- `PopInCommand` reverses it: window closes, ViewModel moves back into `OpenDocuments`
  (appended at the end — original tab position isn't tracked/restored).
- Closing a popped-out window via its OS titlebar (✕) closes the document entirely (same
  as closing a tab) — it does **not** silently re-dock. Only the dock-window button
  re-docks. This matches ordinary window-closing conventions.
- Pop-out button (`mdi:export`) is visible only on hover or on the currently active/
  selected tab — not on every tab all the time, and never on the `TabControl` itself (only
  individual tabs can pop out). Pop-in button (`mdi:dock-window`) is always visible on a
  popped-out window, no hover-gating.
- Closing a file's tab does **not** cascade-close section-viewer tabs opened from it —
  today's `RawViewer`/`TextureViewer` MDI children have no parent-child lifetime tie to the
  `FileViewer` that spawned them, and that independence is preserved, not "fixed."

**Project structure**: in-place rewrite of the same two project folders/names
(`Gibbed.TombRaider.DRMEdit`, `Gibbed.DeusEx3.DRMEdit`), same output exe names. New NuGet
dependencies for both: `Avalonia`, `Avalonia.Desktop`, `AvaloniaHex`, `Material.Icons.Avalonia`,
`MessageBox.Avalonia`, `CommunityToolkit.Mvvm`.

## Component mapping

| Today (WinForms) | Becomes (Avalonia) |
|---|---|
| `Explorer` (MDI container, File/Windows menus, toolbar) | `MainWindow` + `MainWindowViewModel`. Windows menu's Cascade/Tile/Arrange Icons dropped (see Decision 1); Close All kept (closes all tabs + all popped-out windows). |
| `FileViewer` (MDI child, section tree) | Tab content, `FileViewerViewModel`. `entryTreeView` → Avalonia `TreeView`. Per-type icons (`__DRM`/`RenderResource`/`Script`/`Wave`, real embedded bitmaps from `SectionTypeImages.resx`) re-embedded as Avalonia asset images, not redrawn from a generic icon set. `hintLabel` is permanently blank in the current code (set to `""` in the constructor, never touched again) — preserved as-is, not "fixed" into something useful. |
| `RawViewer` (MDI child, `HexBox`) | Tab content, `RawViewerViewModel`. `SplitContainer` (top: hex view, bottom: info tabs) → Avalonia `GridSplitter` in the same top/bottom arrangement. `hexBox` → `AvaloniaHex.HexEditor`, `Document="{Binding HexDocument}"` where `HexDocument` is `new MemoryBinaryDocument(Data, isReadOnly: true)` — direct match for today's `ReadOnly = true` + `DynamicByteProvider`. Bottom `TabControl`'s second tab (`tabPage2`) is empty/unused in the current code (no controls, default "tabPage2" label) — preserved as an empty tab rather than silently dropped. `loadFromFileButton` is permanently `Enabled = false` today and never enabled anywhere — ported as permanently disabled, same reasoning. |
| `TextureViewer` (MDI child, `PictureBox`) | Tab content, `TextureViewerViewModel`. `previewPictureBox` → Avalonia `Image` bound to a `WriteableBitmap` built the same way as today's `MakeBitmapFromTrueColor`/etc. (`Marshal.Copy` into a locked bitmap), fed by the same unchanged `Squish.Native.DecompressImage`/`CompressImage` calls. Zoom toggle: `PictureBoxSizeMode.Zoom`→`Image.Stretch="Uniform"`, `.Normal`→`Stretch="None"`; `previewPanel.AutoScroll` → `ScrollViewer`. |
| `OpenFileDialog`/`SaveFileDialog` | `IStorageProvider` via a new `IFilePickerService` abstraction (doesn't exist today — needed because ViewModels can't reach into a `Window` directly under proper MVVM), one implementation backed by the active window's `StorageProvider`. |
| `MessageBox.Show` | `MessageBox.Avalonia` package (12.0.0) — Avalonia has no built-in equivalent. |
| `Application.SetHighDpiMode`/`SetColorMode(SystemColorMode.System)` | Not ported — Avalonia windows are per-monitor-DPI-aware automatically; `Application.RequestedThemeVariant` via Avalonia's `FluentTheme` gives the OS-follows-theme behavior. |

## Data flow

Startup file paths and File→Open both go through `IFilePickerService` → `FileViewerViewModel`
deserializes the `DRMFile` (unchanged `Gibbed.TombRaider.FileFormats` code) → appended to
`OpenDocuments` as a tab. Selecting/double-clicking a tree node picks `RawViewerViewModel`
or `TextureViewerViewModel` by section type (same switch as today's `GetViewer`) and appends
it as its own new top-level tab.

**Shared mutable state, preserved exactly**: `TextureViewerViewModel`/`RawViewerViewModel`
hold a reference to the *same* `DRM.Section` object the owning `FileViewerViewModel`'s
`DRMFile` already holds — not a copy. `TextureViewer`'s save path writes straight into
`section.Data` in memory (original author's `//TODO Find a better way to do this!!` comment
stays, since it's still true), and nothing persists to disk until "Save DRM" runs on the
owning file. This is not something being changed, just carried over.

## Known gaps carried over unchanged (tracked for later review, not fixed here)

Per explicit instruction: these are documented so they can be reviewed and potentially
fixed later, not silently reproduced without a record.

1. **No error handling on corrupt/malformed DRM load** — `FileViewer.LoadResource` has no
   try/catch around `DRMFile.Deserialize` today; a bad file crashes rather than showing an
   error. Ported as-is (no new handling added).
2. **Exception-type change in the "unsupported image format" catch** — today's
   `catch (OutOfMemoryException)` around `Image.FromFile` relies on a GDI+ idiosyncrasy
   (corrupt/unsupported images throw `OutOfMemoryException` under System.Drawing).
   Avalonia's `Bitmap` loader won't replicate that specific quirk, so this becomes a
   general `catch (Exception)` — same user-facing message, different underlying exception
   type driving it. Not a functional change, but worth a note since the original code
   depended on a very specific (and widely-mocked) legacy .NET behavior.
3. **`TextureViewer.ReplaceImage`'s same-size-only / single-mipmap-only restrictions** —
   already flagged in `drmCompressionHandling.md` §4 as a real gap (no texture-upscaling
   support), carried over unchanged in this stage since fixing it is out of scope here.

(The unrelated, already-tracked `PCD9File`'s `Unknown16 & 0x8000` → `NotSupportedException`
limitation, logged in `CLAUDE.md`, is unaffected by this rewrite — same `FileFormats` code
either way.)

## Testing / verification plan

- **Tomb Raider (`Gibbed.TombRaider.DRMEdit`) — blocks merge**: interactive validation
  against real Tomb Raider Underworld data (DRM/CDRM files already used earlier in this
  project) — open files, walk the section tree, open Raw-viewer and Texture-viewer tabs
  (DXT1/DXT5 render correctly, zoom + alpha toggle work), exercise pop-out/pop-in on at
  least one tab of each kind, verify Save DRM round-trips, verify Raw/Texture Save-to-file
  and Load-from-file paths.
- **Deus Ex 3 (`Gibbed.DeusEx3.DRMEdit`) — blocks merge, structurally only**: builds with
  0 warnings/errors, launches and closes cleanly. No requirement to open real DX3 data or
  walk sections/textures this stage.
- **Cross-platform smoke check**: `dotnet publish -r osx-arm64 --self-contained` succeeds
  for both projects on a plain (non-`-windows`) `net10.0` TFM. Not real macOS execution
  testing (no Mac hardware yet).
- No new automated/unit tests — none exist for DRMEdit's UI today; manual interactive
  verification is the same bar every prior DRMEdit change in this project has used.

## Follow-on work (not this stage)

- Stage 3 item 8: `Gibbed.Squish` native P/Invoke → `BCnEncoder.NET` (makes texture
  decode/encode actually cross-platform, not just the shell around it).
- Stage 3 item 9: headless/CLI invocation surface for the future Python GUI wrapper,
  built on top of what this stage produces — not a replacement for the GUI.
- The three "known gaps carried over unchanged" above, if/when prioritized.
