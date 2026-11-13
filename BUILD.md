# How to build it from scratch

If anyone wants to actually build it from their own machine, here's how to do it.

### Building from Source

Prerequisites: .NET SDK 6.0+, Python 3.8+

> **Note on Steam Game Location:**  
> The project resolves game assemblies by default from the standard Steam directory:  
> `C:\Program Files (x86)\Steam\steamapps\common\Word Play\Word Play_Data\Managed`  
> If Word Play is installed on another drive or library folder (e.g. `D:\SteamLibrary\...`), set the `WORDPLAY_DIR` environment variable before building:
> ```powershell
> $env:WORDPLAY_DIR = "D:\SteamLibrary\steamapps\common\Word Play"
> ```
> or specify `WORDPLAY_MANAGED_DIR` pointing directly to the `Managed` folder.

#### Package Release (apworld & zip bundle)
Run the automated packager from the repository root:
```powershell
python package_release.py
```
This automatically produces:
- `Release/word_play.apworld`
- `Release/WordPlayArchipelago_v1.0.0_with_BepInEx.zip`

*(Alternatively, run `python package_release.py --apworld` to compile only the `.apworld` package, or `python package_release.py --mod` to compile only the mod zip **Bepinex included**).*
