# Notices

## DirectXTex / texconv

DDS resizing and recompression are performed with `texconv.exe` from Microsoft DirectXTex.

- Project: https://github.com/microsoft/DirectXTex
- License: MIT License
- Copyright: Microsoft Corporation

This repository is set up so the source tree does not need to include `texconv.exe`. Users can install it with:

```powershell
winget install Microsoft.DirectXTex.Texconv
```

or place `texconv.exe` at:

```text
KSPTextureOptimizer\Tools\texconv.exe
```

If a release package chooses to bundle `texconv.exe`, it should include the DirectXTex MIT license text and retain the Microsoft copyright notice.

## Kerbal Space Program and Mods

Kerbal Space Program, Squad/Intercept/Take-Two assets, and third-party mod assets are not part of this project. Do not commit game files, mod textures, backup folders, run manifests from real installs, or optimized texture outputs.

This tool preserves texture paths and extensions, but it does not grant permission to redistribute optimized copies of someone else's textures.

