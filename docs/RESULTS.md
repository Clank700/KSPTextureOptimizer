# Example Results

These are example results from one heavily modded KSP 1.12.x install on an 8 GB VRAM GPU. They are included to explain why the tool exists and what kind of outcome is realistic.

They are not benchmarks. KSP scene, camera angle, loaded craft, mod list, graphics settings, driver behavior, and measurement overlay all affect the numbers.

## Parallax Pass

`Parallax_StockTextures` was reduced from about 4 GB to about 1 GB on disk.

Observed VRAM:

- Before: Parallax grass scenes around 7.6 GB.
- After: similar grass scenes around 6.8-6.9 GB.
- Space/asteroid scene also dropped by several hundred MB.

The disk reduction was huge, but VRAM did not drop by the full 3 GB because KSP was not keeping every Parallax source texture resident in every scene, and VRAM is also consumed by non-texture resources.

## Additional Mod Passes

After lowering more selected mod textures, observed VRAM was roughly:

- Asteroid scene: 5.6-5.8 GB.
- VAB: about 800 MB to 1 GB lower than before.
- Grass scene: about 6.2-6.35 GB.

That is the practical win: enough headroom to avoid running at the edge of an 8 GB card and possibly enough room for more mods.

## Why Disk Savings Are Not VRAM Savings

Disk size and VRAM usage are related, but not identical.

Reasons:

- Only currently loaded textures consume VRAM.
- DDS compression format matters.
- Mipmaps add size but improve distant rendering and reduce shimmer.
- KSP/Unity also uses VRAM for render targets, shadows, terrain, meshes, particles, UI, post-processing, scatter systems, and driver overhead.
- Some mods generate runtime textures or use asset bundles that this tool does not touch.

Use the tool as a controlled way to reduce texture pressure, not as a precise VRAM calculator.

