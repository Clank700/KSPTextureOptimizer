# KSP Universal Texture Optimizer

A small Windows GUI tool for reducing Kerbal Space Program texture memory pressure by downscaling selected mod textures offline.

It was built for heavily modded KSP 1.12.x installs where VRAM is the limiting factor. KSP and Unity can keep a lot of textures resident, and visual mods such as Parallax, EVE/BoulderCo, Scatterer, ReStock, and part packs can push an 8 GB GPU right to the edge. This tool does not add dynamic streaming to KSP. It makes the textures smaller before KSP loads them.

## What It Does

- Scans `GameData` or selected mod folders.
- Shows texture path, format, dimensions, mip count, size, target size, savings, and warnings.
- Lets you choose a max texture size such as `4096`, `2048`, `1024`, or a custom value.
- Requires a Preview pass before Optimize.
- Rebuilds the preview every time you click Preview, so folder selections and settings are current.
- Optimizes selected textures in place.
- Creates a timestamped backup before replacing anything.
- Writes a manifest for audit and one-click restore.
- Refuses to optimize or restore while `KSP_x64.exe` is running.

## Simple Use

1. Close KSP.
2. Start `KSPTextureOptimizer.exe`.
3. Browse to your `GameData` folder.
4. Select the mod folders you want to optimize.
5. Pick a target max resolution.
6. Click `Preview`.
7. Review the savings and warnings.
8. Click `Optimize`.
9. Test KSP.

If something looks bad, click `Restore Backup` and choose the matching `runs\<run-id>\manifest.json`.

## Recommended Targets

- `2048`: conservative first pass for large part packs and many environment textures.
- `1024`: stronger pass for part packs, suits, IVAs, cloud textures, and secondary details.
- `512`: aggressive; useful only for things you do not inspect closely.

The default preset leaves `2048` and smaller textures alone. That means some folders can look large on disk but still show little or no estimated savings at a `2048` target.

## Real Example Results

On one heavily modded KSP install with an 8 GB GPU:

- `Parallax_StockTextures` went from about 4 GB to about 1 GB on disk.
- VRAM in Parallax grass scenes dropped from roughly 7.6 GB to roughly 6.8-6.9 GB.
- After additional mod texture passes, the VAB dropped by roughly 800 MB to 1 GB.
- An asteroid scene dropped to roughly 5.6-5.8 GB.
- A grass scene settled around roughly 6.2-6.35 GB.

Disk savings are not equal to VRAM savings. KSP only loads some textures in a given scene, and VRAM is also used by render targets, shadows, meshes, scatter systems, terrain, post-processing, UI, and driver overhead.

More detail is in [docs/RESULTS.md](docs/RESULTS.md).

## Supported Inputs

- DDS: reads common DXT/BC headers, dimensions, and mip counts. DDS resizing is handled by DirectXTex `texconv.exe`.
- PNG: resized in place while preserving alpha.
- TGA: simple uncompressed 8/24/32-bit TGA support.
- MBM, `.truecolor`, asset bundles, `.mu`, and unknown containers: skipped and reported.

The tool keeps file paths and extensions the same so existing mod configs and texture references keep working.

## Safety Model

- Conversion happens in `staging\<run-id>` first.
- Staged outputs are validated before originals are touched.
- Originals are copied to `backups\<run-id>\...`.
- `runs\<run-id>\manifest.json` records source hash, optimized hash, original size, optimized size, dimensions, format, mip counts, and backup path.
- Restore checks the current optimized file hash before overwriting. If the file changed after optimization, restore skips it and reports that.

This tool does not edit configs, `.mu` models, asset bundles, Kopernicus configs, Parallax configs, or mod metadata.

## DirectXTex / texconv

DDS conversion uses Microsoft DirectXTex `texconv.exe`.

DirectXTex is an open-source Microsoft project under the MIT License. This repository does not need to commit `texconv.exe`; users can either install it with winget or place the executable in the local `Tools` folder.

Install option:

```powershell
winget install Microsoft.DirectXTex.Texconv
```

Local tool option:

```text
KSPTextureOptimizer\Tools\texconv.exe
```

The app first checks `Tools\texconv.exe` beside the running EXE, then searches `PATH`.

See [NOTICE.md](NOTICE.md) for dependency notes.

## Build

This project intentionally builds with the .NET Framework compiler that ships with Windows. No modern .NET SDK is required.

```powershell
.\build.ps1
```

Outputs:

```text
KSPTextureOptimizer.exe
bin\KSPTextureOptimizer.Tests.exe
```

Run tests:

```powershell
.\bin\KSPTextureOptimizer.Tests.exe
```

## What This Is Not

- Not a dynamic texture streaming mod.
- Not a KSP plugin.
- Not a config optimizer.
- Not a magic fix for all VRAM use.
- Not a replacement for testing your own mod list.

It is an offline, reversible texture downscaler with a safety-first workflow.

## License

KSP Universal Texture Optimizer is released under the MIT License. See [LICENSE](LICENSE).

Kerbal Space Program, mod names, and mod assets belong to their respective owners. This repository should not include KSP files, mod textures, backups, run manifests from real installs, or Microsoft binaries.
