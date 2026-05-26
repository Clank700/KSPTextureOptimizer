# Contributing

This project is intentionally simple and conservative. The main rule is: never risk a user's only KSP install for a clever optimization.

Good contributions:

- Better DDS format handling.
- Clearer warnings.
- More tests for edge-case texture headers.
- Safer restore behavior.
- Better estimates that distinguish disk size from likely VRAM impact.
- Packaging improvements that do not bundle copyrighted game or mod assets.

Avoid:

- Editing mod configs automatically.
- Touching `.mu`, asset bundles, save files, or KSP settings.
- Removing backups or manifests automatically.
- Treating all disk savings as VRAM savings.

Before opening a PR:

```powershell
.\build.ps1
.\bin\KSPTextureOptimizer.Tests.exe
```

