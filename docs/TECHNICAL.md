# Technical Notes

## Workflow

1. Scan selected folders for texture-like files.
2. Parse metadata without writing anything.
3. Preview target dimensions and estimated output size.
4. Convert each selected file into a staging folder.
5. Validate staged output.
6. Hash and back up the original file.
7. Replace the original with the staged output.
8. Write a manifest that can restore the original bytes later.

## DDS Handling

DDS files are parsed directly for width, height, mip count, and format. Common DXT/BC formats are supported. Resizing and recompression are delegated to DirectXTex `texconv.exe`.

By default, the tool preserves the compression family and mipmap policy:

- DXT1 stays DXT1-like.
- DXT5 stays DXT5-like.
- Existing mipmapped textures keep mipmaps.
- Non-mipmapped textures stay non-mipmapped unless `Force mipmaps` is enabled.

## PNG and TGA Handling

PNG resizing uses `System.Drawing` and preserves alpha.

TGA support is intentionally narrow: simple uncompressed 8/24/32-bit images. Unsupported TGA variants are skipped.

## Conservative Skips

The tool skips cases that are more likely to break visuals or references:

- Already at or below target size.
- Tiny UI/icon textures.
- Unsupported DDS formats.
- Non-power-of-two DDS unless explicitly allowed.
- Texture containers that are not directly editable.
- Files that fail validation.

## Restore Behavior

Restore uses the manifest's `optimizedHash` to detect whether the current file still matches the tool's output. If another tool or mod update changed the file after optimization, restore skips it instead of overwriting blindly.

