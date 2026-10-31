# How to build it from scratch

If anyone wants to actually build it from their own machine, here's how to do it.

### Building from Source

Prerequisites: .NET SDK 6.0+, Python 3.8+

#### Package Release (apworld & zip bundle)
Run the automated packager from the repository root:
```powershell
python package_release.py
```
This automatically produces:
- `Release/word_play.apworld`
- `Release/WordPlayArchipelago_v1.0.0_with_BepInEx.zip`

*(Alternatively, run `python package_release.py --apworld` to compile only the `.apworld` package, or `python package_release.py --mod` to compile only the mod zip **Bepinex included**).*
