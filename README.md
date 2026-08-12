# Lada

**Version 0.1.5** — Lada is finally out of the box. A box unboxing itself!

Lada is a personal desktop organizer for Windows 11: semi-transparent containers on the desktop that hold shortcuts, behaving like lightweight floating windows instead of normal applications.

Built as a personal project in C# and WPF, with a single third-party dependency (LibreHardwareMonitorLib, for the CPU/GPU widgets) beyond what ships with .NET.

The name comes from the Swedish "låda", a box or drawer, without the diacritic. A lada is exactly that: a box that holds things.

## What it does

Each "lada" is a resizable, draggable window that sits above the desktop but below whatever app you're actually using.
It never steals focus, never shows up in Alt-Tab or the taskbar.

- Drag files, folders, or shortcuts from the desktop into a lada. It keeps a reference to the original path, nothing gets copied or moved. Shortcuts (.lnk) show the target's own icon, not the small arrow overlay Explorer draws on them.
- Image files (jpg, png, gif, bmp, and whatever other formats the codecs installed on your machine support) show a real downscaled thumbnail of their own content instead of a generic file icon, both in a lada's own grid and inside an open Drawer. Falls back to the generic icon for anything that fails to decode (corrupt file, unsupported format).
- Reorder items by dragging one onto another. The dragged icon dims while you're holding it, the drop target highlights, so you can tell the drag actually started and where it'll land.
- Select several items at once: Ctrl+click to add/remove one at a time, Shift+click to select a range, or click-drag over empty space to draw a selection rectangle. A multi-selection reorders, drags, or gets removed as a group.
- Remove the current selection with the Delete key, or right-click it for "Remove from this lada" (shows the count when more than one item is selected). Always just a reference removed, the file itself is untouched.
- **A Drawer is a live view into a folder, right inside a lada.** Right-click a folder already in a lada and choose "Show contents" to turn it into one: capped in height with its own scrollbar, staying in sync with the real folder (add/remove/rename a file on disk and it updates within about a second), and collapsing back to a plain icon if the folder becomes unreachable (e.g. a USB drive unplugged). Dropping a file into an open drawer actually moves it into that folder on disk; dragging a file out of a drawer adds a normal reference elsewhere, same as dragging from the desktop.
- Right-click empty space in a lada for more: sort its contents by type (folders, documents, images, videos, audio, executables) once or automatically on every new drop, jump straight to a preset size or fit the lada to its current content, create a new lada, add a widget, or delete this one entirely (asks for confirmation first; the files inside stay on disk, only the lada and its references are removed).
- **List view**: same right-click menu, "View" switches a tab between the default icon grid and a list, one row per item. In list view, "Columns" adds Type, Size, and Date modified columns independently (none shown by default) — Type matches what Windows Explorer's own Details view shows for the same file. Per tab, so different tabs in the same lada can each have their own view and columns. Sorting stays "Sort by type" or manual drag-reordering either way; there's no click-a-column-header-to-sort.
- **World clock widget**: "New widget → Clock" from that same menu opens a searchable list of all Windows timezones; the clock ticks live (updates every second), labeled with the timezone's own name. Right-click a clock for "Change timezone" to repoint it (updates the label too), or to remove it.
- **Disk space widget**: "New widget → Disk space" lists the ready drives on the machine; picking one adds a widget showing free space and a small usage bar, refreshed every 30 seconds. Right-click one for "Change drive" to repoint it, or to remove it. Shows "Unavailable" instead of crashing if the drive goes away (a USB drive unplugged, for instance).
- **Timer widget**: "New widget → Timer" prompts for a duration (hours/minutes/seconds) and adds a countdown widget with a draining progress bar. Click it once to start or pause; right-click for "Reset" (back to the full duration, paused) or "Change duration". Keeps counting down correctly across a restart (based on wall-clock time, not a simple in-memory tick), and plays a short double-beep alongside a tray notification once when it reaches zero, repeating every second until you click the widget again (which also restarts it) or otherwise touch it (Reset, Change duration, removing it).
- **Battery widget**: "New widget → Battery" shows charge percentage and a usage bar, refreshed every 30 seconds, with "(charging)" appended while plugged in and charging. Shows "Unavailable" on a desktop with no battery.
- **Memory widget**: "New widget → Memory" shows used/total RAM and a usage bar, refreshed every 2 seconds.
- **CPU widget**: "New widget → CPU" shows overall usage and temperature, refreshed every 2 seconds. Right-click it for "Detailed view" to add a one-minute usage sparkline and the current clock speed (averaged across cores); off by default, one flag per widget, same idea as the list view's optional "Columns".
- **GPU widget**: "New widget → GPU" lists every GPU LibreHardwareMonitor detects (integrated and dedicated); picking one shows its usage and temperature, refreshed every 2 seconds. Right-click one for "Change GPU" to repoint it, or "Detailed view" for the same sparkline + clock speed as the CPU widget. CPU and GPU widgets share a single background poll regardless of how many are on screen, rather than each one polling on its own.
- **Network widget**: "New widget → Network" lists every network adapter LibreHardwareMonitor detects; picking one shows download and upload speed, refreshed every 2 seconds. Right-click one for "Change adapter" to repoint it, or "Detailed view" for a download-speed sparkline (auto-scaled to whatever's happening, since network throughput has no natural 0-100 ceiling like a usage percentage does).
- **Auto-organize**: in "Auto-organize" (right-click empty space), check one or more categories (Folders, Documents, Images, Videos, Audio, Executables, Other) for a tab to absorb matching desktop files automatically and continuously, hiding their original icon (never a real move or rename on disk). If several tabs target the same category, the most recently active one wins. Removing an absorbed item from a lada gives its icon back to the desktop, at its original spot.
- Split a lada into tabs: a small '+' at the right of the title bar (past a thin separator line) adds one. Click a tab to switch, double-click or right-click "Rename" to rename it, right-click "Delete this tab" to remove it (asks first if it still holds items; the last remaining tab can't be deleted). Drag an item onto a tab header, or right-click it for "Move to", to move it into another tab. Sorting (manual and auto) is per tab. A lada that never gets a second tab looks exactly as it always has, just the '+' shows. Tabs live in the title bar itself rather than a row of their own, so they stay reachable even when a lada is folded.
- **To-do list or memo mode**: right-click empty space in a lada (or any of its tabs) for "Convert to to-do list" / "...to memo" to turn the active tab into a checklist or a freeform notes surface instead of an icon grid — "Back to icons" switches it back. Works on a brand-new lada right away, no need to add a second tab first. Converting is blocked (with a message) if the tab still holds content for its current mode, so nothing is ever silently hidden or lost. A to-do list supports checking off tasks (strikes through the text), inline editing, deleting, drag-reordering, and adding new ones by typing and pressing Enter; a memo is a single freeform multi-line note that autosaves as you type. Neither one grows the lada itself — both scroll internally within whatever size you've set.
- Double-click the title bar (not on a tab) to fold a lada down to just its title, or unfold it back.
- Double-click an empty spot on the desktop to hide or show every lada at once. Double-clicking an actual desktop icon still works normally. `Ctrl+Alt+D` does the same thing from anywhere, no need to see the desktop first.
- `Ctrl+Alt+O` toggles Overlay: every lada jumps on top of whatever's currently open (even a maximized app), un-hiding them first if they were hidden. Press it again to send them back below active windows as usual.
- **Opacity on hover** (off by default, toggle from the tray menu): every lada fades down to 40% opacity when the mouse isn't over it, and back to 100% (with a short animated transition) the moment it is. Never fades away while you're actively using it — its own right-click menu open, the icon/color picker open, dragging, resizing, or renaming its title all keep it fully opaque regardless of where the mouse ends up. Overlay mode (`Ctrl+Alt+O`) always forces every lada to full opacity while it's active.
- **Magnetism** (off by default, toggle from the tray menu): while dragging a lada, its edges snap flush to a nearby lada or to a screen/monitor edge the moment it comes within about 15px, live during the drag. Toggling it off mid-drag has no effect until the next drag.
- **3D perspective** (off by default, toggle from the tray menu): a lada sitting at the center of its screen renders flat; the further it sits from center, the more it leans in real 3D (rendered on an actual perspective-projected plane, not a flat 2D approximation), as if mounted on a HUD panel facing that center point — horizontal distance tilts it left/right (yaw), vertical distance tilts it up/down (pitch), live while dragging, up to 15° at the screen edge. A lada dead center on its screen is always perfectly flat, and the tilt automatically pulls itself back in as needed so no corner ever renders past the lada's own edges. Works the same in all three themes.
- **HUD glow** (off by default, toggle from the tray menu): replaces a lada's normal thin border with a thicker rim in its own picked color, all the way around it. Independent of 3D perspective; either can be used alone or together, in any theme.
- Click the small icon next to a lada's title to pick a shape and color for it, from a built-in set of 41 icons and 8 colors (the 8 colors differ by theme, see below). Below the 8 presets, three sliders (hue/saturation/lightness) plus a hex code box (#RRGGBB) give full freedom to pick any color at all, applied live as you drag or type — useful for a color that doesn't happen to be one of the current theme's 8 presets, or after switching a lada to a theme whose palette doesn't include its current color. The whole 41-icon collection in the popup previews the color live too, right alongside the title bar, instead of only updating once the popup is reopened. The button next to the hex box saves the current color permanently to your own collection, which then appears below the theme presets (shared across every lada, and hidden until you've saved at least one color).
- Drag the chevron in the bottom-right corner to resize freely, or right-click it (or right-click empty space in the lada) for preset sizes (3×1, 3×3, 5×1, 5×3, 10×1, 10×3, measured in icon columns × rows) or "Fit to content" to shrink or grow the lada to exactly wrap whatever's inside right now, trimming any empty space on the right and bottom without changing how items are currently arranged into rows. Width fitting only applies in icon grid mode — list rows and the to-do/memo surfaces already span the full width by design. A lada also grows its own height automatically whenever content added to it (an item wrapping onto a new row, a widget taller than a normal icon) would otherwise get cut off at the bottom; it never shrinks back down on its own that way, only "Fit to content" or a manual resize does that.
- Three themes, picked from the tray menu and applied instantly to every open lada: **Midnight** (the default: dark, translucent, rounded corners), **Modernism** (Bauhaus/De Stijl inspired: solid white background, flat 2px black border, square corners, no drop shadow, solid primary-color palette), and **Anderson** (Matrix terminal inspired: solid black background, square corners, no drop shadow, monospace title text). In Modernism, the color picked for a lada paints its title bar and resize chevron instead of tinting the icon glyph; the title text and icon glyph automatically switch between black and white (based on that color's own brightness) so they stay readable against any accent, light or dark. In Anderson, the color picked for a lada (green by default) recolors its border, title text, and icon glyph together, so each lada can be its own terminal color (green, amber, cyan, ...); the icon/color picker popup itself always stays the default green regardless.
- Every right-click menu (an item, empty space, a tab, a widget, a drawer folder) is themed to match the active lada theme, instead of a generic Windows menu look — background, border, corner radius, separator lines, and font all follow whichever of the three themes is active, and the selection highlight and the accent-colored dot on checkable items (like "Grid"/"List" under "View") follow that specific lada's own picked color rather than a fixed theme color, in every theme. The tray icon's own menu (including its Theme/Language submenus) is themed the same way but isn't tied to any one lada, so it stays on the theme's own fixed accent. The tray menu (WinForms, rendered separately from the WPF ones) matches as closely as that toolkit allows — same colors, same rounded/square corners, same border thickness — and updates live when the theme changes.
- A tray icon gives you a menu to create a new lada, bring back any hidden ones, switch themes, switch language, read a short "About" (its own themed window, matching whichever of the three themes is active, with a note that Lada is free and open source and clickable links to this GitHub repo and the author's LinkedIn), or quit.
- "Delete this lada" asks for confirmation first in that same themed style (matching whichever of the three themes is active) rather than a generic Windows dialog — the files inside stay untouched on disk either way, only the lada itself and its own organization are removed.
- **Arrange ladas** (tray menu): a one-shot action that arranges every currently visible lada into a clean left-to-right, top-to-bottom layout on the primary screen, based on each lada's current position. Hidden ladas (desktop double-click toggle) are left untouched.
- French and English, picked from that same tray menu and applied instantly everywhere (menus, dialogs, tooltips) without restarting. Defaults to whichever one matches Windows' own display language the first time Lada runs; once picked explicitly it's remembered from then on. The menu labels quoted elsewhere in this README (e.g. "Remove from this lada") are the English ones.
- If a lada ends up off-screen (a monitor got unplugged, resolution changed), it's pulled back onto the primary screen automatically, both at startup and live.

Position, size, title, icon, color, fold state, sort setting, and contents are all saved automatically to `%APPDATA%\Lada\layout.json` and restored on the next launch.

## Requirements

- Windows 11
- .NET 8 SDK to build it (the app itself only needs the .NET 8 runtime once built)

## Running it

```
git clone <repo>
cd Lada
dotnet run
```

`dotnet run` builds and launches the app. Layout data lives in `%APPDATA%\Lada\`, and logs from anything that fails silently go to `%APPDATA%\Lada\logs\lada.log`. Note that running Lada this way (or under an IDE debugger) puts it inside a Windows Job Object that also owns whatever it launches from a lada; see Known Limitations below.

## Installing it permanently

```
scripts/publish.ps1
```

Publishes a Release, framework-dependent, single-file build, deploys it to `%LocalAppData%\Lada\Lada.exe`, registers it to start with Windows (a `HKCU\...\Run` registry entry, no admin rights needed), and launches the new build. Re-run the same script any time there's an update to push out, it closes the running instance, republishes, and redeploys in one go. To stop it from starting with Windows, delete the `Lada` value under `HKCU:\Software\Microsoft\Windows\CurrentVersion\Run`.

## Tests

```
dotnet test Lada.Tests/Lada.Tests.csproj
```

Covers the JSON persistence layer, the icon library, the file-type categorizer, off-screen detection logic, window-snap/arrange geometry, and the timer countdown calculation. The native Win32 behavior (window styles, the desktop hook, drag and drop, resizing) and the themed menu/tray rendering aren't unit-testable in any useful way, so those get checked by hand when they change.

## How it's built

No MVVM framework. `System.Windows.Forms.NotifyIcon` is the one WinForms piece pulled in, just for the tray icon. Native window behavior (keeping a lada out of Alt-Tab, pinned below active windows, the desktop double-click hook, off-screen recovery) goes through direct P/Invoke calls in `Native/`, including the Battery and Memory widgets. The one third-party dependency in the project is [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor): CPU/GPU load and temperature have no native Win32 equivalent worth hand-rolling, unlike Battery/Memory.

```
Lada/
├── App.xaml(.cs)          entry point, wires everything together on startup
├── Windows/LadaWindow.*   the lada window, split into partial classes by concern
│                          (native behavior, icon picker, fold, sort, drag & drop, selection,
│                          drawers, tabs, theme, localization, clock/disk/timer/battery/memory/cpu/gpu widgets, monitor recovery)
├── Windows/TimeZonePickerWindow.*  searchable timezone picker, used to create/reconfigure a clock widget
├── Windows/TimerDurationPickerWindow.*  hours/minutes/seconds picker, used to create/reconfigure a timer widget
├── Models/                 what gets saved to JSON
├── Services/               persistence, tray icon (incl. its themed WinForms menu renderer), desktop-toggle hook, global hotkeys, theme switching, language switching, sorting, monitor layout, shared CPU/GPU sensor polling
├── Styles/                 Theme.xaml (Midnight), ThemeModernism.xaml, ThemeAnderson.xaml -- one swapped in live by
│                          ThemeManager at a time -- plus MenuStyles.xaml (shared context/tray-menu styling, merged
│                          into all three, since ThemeManager never merges the theme files together)
├── Native/                 P/Invoke: window styles, z-order, mouse hook, global hotkeys, screen rect
├── Resources/               icon and color library for the picker, Strings.cs (every UI string, French/English),
│                          ThemeSurfaceColors.cs (theme colors/geometry for the tray's WinForms menu)
├── scripts/publish.ps1     publish + deploy + register-for-startup, re-run to push an update
└── Lada.Tests/              the parts of this that are actually testable
```

## Why C#/WPF

This is a Windows-only tool through and through, so the practical choice was whichever language sits closest to Win32. Keeping a lada out of Alt-Tab, pinning it below active windows without stealing focus, hooking the desktop double-click, and pulling icons straight from the shell all go through direct P/Invoke in `Native/`. C# reaches that API cleanly without leaving a managed language.

WPF earned its place for the same reason the project pulls in no third-party UI dependencies. Translucency, rounded corners, animated theme switching, and XAML-driven styling all ship with .NET itself, which is how three swappable themes and instant French/English switching happened without needing an extra framework on top.

.NET 8 rounds it out with a modern runtime, fast startup, and a single-file publish (`scripts/publish.ps1`) that installs without needing an admin prompt. None of it was picked for portability. Cross-platform was never a goal, see Known Limitations below.

The project itself is open source and free, no license fees, no paid tier.
Following the name and the principles behind furnitures: clone it, build it, change it... make your own Lada.

## Known limitations

- The CPU widget's temperature and clock speed read the CPU's MSR registers through LibreHardwareMonitor's kernel driver, which needs administrator rights to load. Lada runs without elevation by design (see Why C#/WPF above), so those two fields show "Unavailable" in practice; the usage percentage doesn't need the driver and always works. The GPU widget's temperature and clock go through the vendor API instead, so they're unaffected.
- The background is a flat semi-transparent fill, not real Mica/Acrylic blur. This was actually attempted: `DwmSetWindowAttribute` plus `WindowChrome` compiled and ran, but produced a static gray fill instead of a live blur, traced back to WPF's own rendering pipeline rather than anything specific to this window. See git history (commit around "Revert real Acrylic backdrop attempt") for the investigation if it's ever worth retrying.
- The icon picker popup has no scroll bar or height cap. With 39 icons plus the color row it's tall (~450px); if a lada sits low on the screen the popup can run off the bottom with no way to reach the icons past the edge.
- Auto-organization only watches the personal Desktop folder (`%USERPROFILE%\Desktop`), not the public Desktop folder shared between accounts.
- Resize presets compute a target size from a fixed per-icon width and height. Since item labels can wrap to one or two lines depending on file name length, the fit is close but not pixel-perfect in every case.
- Drawers don't nest: a subfolder shown inside a drawer opens in Windows Explorer on double-click rather than becoming a drawer itself. Moving a folder into a drawer that's on a different disk drive isn't supported either (Windows can't rename a folder across drives), so it fails with a message rather than silently copying.
- In list view, a clock or disk-space widget's row doesn't offer "Change timezone"/"Change drive" in its right-click menu (only "Remove from this lada") — switch back to grid view to change either.
- Multi-selection applies to a lada's own items, not to the files listed inside an open drawer (those stay single-click/double-click only).
- Windows-only. WPF itself doesn't run on Linux or macOS, and nearly every native behavior (always-below-active-windows without stealing focus, the desktop double-click hook, Shell icon extraction, the tray icon) goes through direct Win32 P/Invoke with no cross-platform equivalent. Porting isn't a portability tweak, it'd be a rewrite.
- The `Ctrl+Alt+O`/`Ctrl+Alt+D` global hotkeys can silently lose to another running app that already claimed the same combination: Windows itself doesn't reserve either one, but nothing stops a third-party tool from grabbing them first (this happened in practice with the previous Overlay shortcut, `Ctrl+Alt+L`, which is a common "lock screen" convention on some systems and tools). A tray balloon on startup names which shortcut failed and why when that happens.
- Running via `dotnet run` (or an IDE debugger) puts Lada inside a Windows Job Object that also owns whatever it launches from a lada; closing Lada in that case can kill the app you just opened (confirmed happening in practice). The installed build (`scripts/publish.ps1`, or double-clicking a built `Lada.exe` directly) doesn't have this problem.
- No item's label is independently renamable (only a lada's own title and its tab titles are). For the world clock widget specifically this means the label always matches the timezone's own name; picking a shorter or more personal label (e.g. "Mom's place" instead of the full timezone name) isn't possible yet. The timer widget's label is likewise fixed ("Timer").
- In list view, a timer widget's row (like clock/disk widgets) doesn't offer its Start/Pause/Reset/Change duration menu, only "Remove from this lada" — switch back to grid view to control it.

## Roadmap

- [ ] **Broader sorting rules**: today it's manual "sort by type" plus an optional auto-sort toggle. A real rules engine (by extension, by date, by custom tags) would go further.
- [ ] **Multi-monitor DPI rendering quality**: a lada currently gets pulled back on-screen if a monitor disappears, but visual sharpness when dragging between differently-scaled monitors isn't specifically handled.
- [ ] More themes beyond Midnight and Modernism, if a third distinct visual direction comes up worth building.
- [ ] Revisit real Mica/Acrylic if WPF ever ships first-class system backdrop support (see Known Limitations above).
- [ ] **Other operating systems**: Linux/macOS support, eventually. This means rewriting the native layer, not a portability tweak (see Known Limitations above).
- [ ] A small mascot for Lada.
- [ ] **Standalone widget-only ladas**: open question — should a widget like the timer be creatable as its own dedicated lada (closer to an iOS home-screen widget) instead of always living as an item inside a regular, mixed-content lada? Leaning against it for now: mixing a widget alongside files in one lada is part of what makes a lada useful (e.g. a recipe file next to a cooking timer), and "several timers at once" is already possible today by adding multiple timer widgets — a dedicated single-purpose lada type wouldn't add anything a normal lada can't already do, just less flexibly.
- [ ] **Mail widget**: a mail-only lada showing the user's inbox (Gmail via Google's API, OAuth). Under design — see `docs/superpowers/specs/` once a spec exists.
