# CLAUDE.md - Project Context for AI Assistants

## Project Overview

**Project Name:** UnityLocalizationToolkit  
**Description:** A WinUI 3 desktop application for localizing/translating Unity games by modifying game asset files, including text extraction, translation import, and font replacement.  
**Programming Language:** C# (.NET 10)  
**UI Framework:** WinUI 3 (Windows App SDK 1.8)  
**Application Mode:** Unpackaged (WindowsPackageType=None)  
**UI Language:** Chinese (Simplified)

## Current Status

### Frontend UI - COMPLETED
The frontend UI has been fully implemented with all pages and navigation working.

### Backend Functionality - IN PROGRESS
Core backend services have been implemented with ongoing improvements:
- ✅ Game project loading and backend detection (Mono/IL2CPP)
- ✅ Text scanning from Assembly-CSharp.dll (working well)
- ⚠️ Text scanning from MonoBehaviour assets (limited - requires type trees)
- ✅ Text scanning from TextAsset assets (working)
- ✅ Traditional font scanning (working - 28 fonts detected in test game)
- ⚠️ TMP font scanning (detection logic improved, pending validation)
- ✅ Excel export/import functionality
- ✅ Asset modification and traditional font replacement

### Recent Fixes (December 2024)
1. **Migrated from local DLLs to NuGet packages** for AssetsTools.NET
2. **Fixed logging** - Changed from `Console.WriteLine` to `System.Diagnostics.Trace.WriteLine` for WinUI 3 compatibility
3. **Fixed asset file discovery** - Properly excludes `.resource` and `.resS` files which are not Unity asset files
4. **Fixed null reference exceptions** - Added proper null checks for `AssetsFileInstance` when loading bundle entries
5. **Improved TMP font detection** - Added 15+ characteristic fields and script reference checking

## Project Structure

```
UnityLocalizationToolkit/
├── UnityLocalizationToolkit.slnx          # Solution file
├── CLAUDE.md                               # This file
├── README.md                               # Project readme
├── LICENSE                                 # License file
└── UnityLocalizationToolkit/               # Main project folder
    ├── App.xaml / App.xaml.cs              # Application entry, theme initialization
    ├── MainWindow.xaml / MainWindow.xaml.cs # Main window with NavigationView
    ├── UnityLocalizationToolkit.csproj     # Project file (Unpackaged mode)
    ├── app.manifest                         # Application manifest
    ├── classdata.tpk                        # Unity class type database
    ├── Pages/
    │   ├── TextTranslationPage.xaml/.cs    # Game text modification UI
    │   ├── FontReplacementPage.xaml/.cs    # Font replacement UI
    │   └── SettingsPage.xaml/.cs           # Settings and theme selection
    ├── Services/
    │   ├── ThemeService.cs                 # Theme switching service
    │   ├── GameProjectService.cs           # Game project loading and detection
    │   ├── TextScannerService.cs           # Text extraction service
    │   ├── FontScannerService.cs           # Font detection service
    │   ├── ExcelService.cs                 # Excel export/import service
    │   └── AssetModifierService.cs         # Asset modification service
    ├── Models/
    │   ├── GameProject.cs                  # Game project data model
    │   ├── TextEntry.cs                    # Text entry data model
    │   ├── FontAsset.cs                    # Font asset data model
    │   └── LanguageFilter.cs               # Language filtering utilities
    └── Assets/                             # (Legacy MSIX assets, not used)
```

## Key Features

### 1. Text Translation Page (修改游戏文本)
- Game directory selection with folder picker
- Automatic backend type detection (Mono/IL2CPP)
- Source language selection (Japanese, English, Korean, Chinese, etc.)
- Text scanning from:
  - Assembly-CSharp.dll (Mono backend) - string literals
  - MonoBehaviour assets - serialized string fields (requires type trees)
  - TextAsset assets - embedded text files
- Text list with type filtering and search
- Export to Excel (.xlsx) with categorized worksheets
- Import translations from Excel
- Apply changes to game files with automatic backup
- Display of Unity engine reserved variables (protected from translation)

### 2. Font Replacement Page (修改游戏字体)
- Game directory selection
- Font resource scanning (Traditional and TMP fonts)
- Font type filtering (All/Traditional/TMP)
- Font list with selection and details
- TMP font identification by characteristic fields and script reference
- Replacement font file picker (.ttf/.otf)
- Traditional font replacement (working)
- TMP font replacement (requires external tools for atlas generation)

### 3. Settings Page (设置)
- Theme selection:
  - System (跟随系统) - follows Windows theme
  - Light (浅色模式)
  - Dark (深色模式)
- Application information and version
- Feature list and technology stack display

## Technical Details

### NuGet Package Dependencies
```xml
<PackageReference Include="AssetsTools.NET" Version="3.0.3" />
<PackageReference Include="AssetsTools.NET.Cpp2IL" Version="3.0.2" />
<PackageReference Include="AssetsTools.NET.MonoCecil" Version="1.0.1" />
<PackageReference Include="AssetsTools.NET.Texture" Version="3.0.2" />
<PackageReference Include="ClosedXML" Version="0.104.2" />
<PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.26100.7175" />
<PackageReference Include="Microsoft.WindowsAppSDK" Version="1.8.251106002" />
<PackageReference Include="Mono.Cecil" Version="0.11.6" />
<PackageReference Include="Samboy063.LibCpp2IL" Version="2022.1.0-pre-release.19" />
```

### Target Framework
- net10.0-windows10.0.26100.0
- Minimum Windows Version: 10.0.17763.0

## Services Architecture

### GameProjectService
- Singleton pattern for global access
- Detects Unity game backend type (Mono/IL2CPP)
- Finds game data directory (*_Data)
- Locates asset files and bundle files (`.assets`, `.unity3d`)
- Excludes `.resource` and `.resS` files (raw resource streams)
- Identifies Assembly-CSharp.dll or GameAssembly.dll

### TextScannerService
- Scans Assembly-CSharp.dll using Mono.Cecil for string literals
- Scans MonoBehaviour assets for string fields (requires type tree info)
- Scans TextAsset assets for embedded text
- Language-aware filtering (Japanese, Korean, Chinese, etc.)
- Engine keyword protection (Unity reserved words)
- Uses `System.Diagnostics.Trace.WriteLine` for logging

### FontScannerService
- Identifies traditional Font assets (TypeId = Font)
- Detects TMP fonts using multiple methods:
  1. Script reference checking (TMP_FontAsset, FontAsset scripts)
  2. Characteristic field detection (15+ fields):
     - m_FaceInfo, m_GlyphTable, m_CharacterTable
     - m_AtlasPopulationMode, materialHashCode
     - m_AtlasTextures, m_AtlasTextureIndex
     - m_GlyphLookupDictionary, m_CharacterLookupDictionary
     - m_FontFeatureTable, m_AtlasWidth, m_AtlasHeight
     - m_AtlasPadding, m_FreeGlyphRects, m_UsedGlyphRects
  3. Backup detection via field name pattern matching
- Extracts associated Material and Texture2D references

### ExcelService
- Uses ClosedXML for Excel operations
- Exports text entries grouped by source type
- Imports translations matching by entry ID
- Separate worksheet for skipped entries

### AssetModifierService
- Modifies Assembly-CSharp.dll string literals (Mono)
- Modifies MonoBehaviour and TextAsset assets
- Creates automatic backups before modification
- Supports traditional font data replacement

## Language Filtering

### Protected Keywords
Unity engine keywords that should not be translated:
- Core classes: UnityEngine, GameObject, Transform, MonoBehaviour
- Lifecycle methods: Awake, Start, Update, OnDestroy
- Common types: Vector3, Quaternion, Color, Texture2D
- UI classes: Canvas, Image, Text, Button

### Source Language Detection
Filters text based on character sets:
- Japanese: Hiragana, Katakana, Kanji
- Korean: Hangul syllables
- Chinese: CJK Unified Ideographs
- English: ASCII letters

## Build Commands

```powershell
# Build the project
cd D:\Document\Git\UnityLocalizationToolkit
dotnet build UnityLocalizationToolkit.slnx -c Debug

# Run the application
dotnet run --project UnityLocalizationToolkit/UnityLocalizationToolkit.csproj
```

## Development Notes

### Code Style
- File-scoped namespaces
- XML documentation comments in Chinese for user-facing elements
- Async/await pattern for file operations
- Singleton pattern for services

### Logging
For WinUI 3 apps, use `System.Diagnostics.Trace.WriteLine()` for debugging output visible in Visual Studio Output window (Debug tab). `Console.WriteLine()` does not work in WinUI 3 apps.

### UI Conventions
- All user-facing text in Chinese
- WinUI 3 Fluent Design controls
- Mica backdrop for modern appearance
- Expander controls for collapsible sections
- InfoBar for status messages
- AccentButtonStyle for primary actions

### File Picker Initialization
For Unpackaged WinUI 3 apps, file pickers require window handle initialization:
```csharp
var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
```

## Known Issues & Limitations

### Current Issues Under Investigation

1. **TMP Font Detection**: TMP fonts may not be detected if the game's asset files lack embedded type trees. The detection has been improved with multiple fallback methods, but games built without type trees may require MonoBehaviour template deserialization.

2. **MonoBehaviour Text Extraction**: Many MonoBehaviour string fields are not being extracted. This is because:
   - Custom MonoBehaviour types require type tree information for proper deserialization
   - Games built with "Strip Type Trees" enabled will have all custom fields return `IsDummy = true`
   - Solution: Use AssetsTools.NET.MonoCecil to generate MonoBehaviour templates from Assembly-CSharp.dll

### General Limitations

1. **TMP Font Replacement**: Requires external tools to generate SDF atlas textures. The current implementation only supports traditional font replacement.

2. **IL2CPP Text Modification**: Reading IL2CPP metadata is supported, but modifying GameAssembly.dll requires additional tooling not yet implemented.

3. **Bundle File Modification**: Currently reads bundle files for scanning but saves modifications to standalone asset files only.

## Future Enhancements

1. **MonoBehaviour Template Generation**: Use AssetsTools.NET.MonoCecil to generate type templates from game assemblies for proper MonoBehaviour deserialization
2. Integrate SDF atlas generation for TMP font replacement
3. Add IL2CPP assembly modification support
4. Implement bundle file modification
5. Add translation memory / glossary features
6. Support additional export formats (CSV, JSON)
7. Add batch processing for multiple games

## Debugging Tips

### Viewing Debug Output
1. Run the application with Visual Studio debugger attached
2. Open Output window (View → Output)
3. Select "Debug" in the "Show output from" dropdown
4. Look for `[TextScanner]`, `[FontScanner]`, `[GameProject]` prefixes

### Common Log Messages
- `[FontScanner] Sample MonoBehaviour structure` - Shows field structure for debugging TMP detection
- `[FontScanner] Found X Font assets, Y MonoBehaviour assets` - Per-file scan results
- `[TextScanner] Processed X TextAssets, Y MonoBehaviours` - Per-file scan results
- `[GameProject] Found X .assets files` - Asset file discovery results
