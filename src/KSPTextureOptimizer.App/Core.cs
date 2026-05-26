using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace KSPTextureOptimizer
{
    public sealed class OptimizerOptions
    {
        public int TargetMaxResolution = 2048;
        public bool IncludeStockDlc;
        public bool IncludePngTga = true;
        public bool IncludeNonPowerOfTwo;
        public bool ForceMipmaps;
        public bool PreserveTimestamps = true;
    }

    public sealed class TextureItem
    {
        public bool Checked;
        public string FullPath;
        public string RelativePath;
        public string Extension;
        public string Format;
        public int Width;
        public int Height;
        public int MipCount;
        public long SizeBytes;
        public int TargetWidth;
        public int TargetHeight;
        public long EstimatedBytes;
        public string Status;
        public string Warning;
        public DateTime LastWriteTimeUtc;

        public bool IsOptimizable
        {
            get { return Status == "Ready"; }
        }

        public string SizeText
        {
            get { return FormatBytes(SizeBytes); }
        }

        public string EstimatedText
        {
            get { return EstimatedBytes > 0 ? FormatBytes(EstimatedBytes) : ""; }
        }

        public string SavingsText
        {
            get
            {
                if (EstimatedBytes <= 0 || EstimatedBytes >= SizeBytes) return "";
                double pct = 100.0 * (SizeBytes - EstimatedBytes) / Math.Max(1, SizeBytes);
                return pct.ToString("0") + "%";
            }
        }

        public static string FormatBytes(long bytes)
        {
            double mb = bytes / 1024.0 / 1024.0;
            if (mb >= 1024.0) return (mb / 1024.0).ToString("0.00") + " GB";
            return mb.ToString("0.0") + " MB";
        }
    }

    public sealed class Manifest
    {
        public string runId { get; set; }
        public string createdAt { get; set; }
        public string kspRoot { get; set; }
        public int targetMaxResolution { get; set; }
        public string converterVersion { get; set; }
        public List<ManifestEntry> files { get; set; }
    }

    public sealed class ManifestEntry
    {
        public string relativePath { get; set; }
        public string originalHash { get; set; }
        public string optimizedHash { get; set; }
        public string originalFormat { get; set; }
        public string targetFormat { get; set; }
        public string originalDimensions { get; set; }
        public string targetDimensions { get; set; }
        public int originalMipCount { get; set; }
        public int targetMipCount { get; set; }
        public string backupPath { get; set; }
        public string status { get; set; }
        public long originalSize { get; set; }
        public long optimizedSize { get; set; }
    }

    public static class TextureScanner
    {
        private static readonly HashSet<string> TextureExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".dds", ".png", ".tga", ".mbm", ".truecolor"
        };

        public static List<string> ListTopLevelFolders(string root)
        {
            var folders = new List<string>();
            if (!Directory.Exists(root)) return folders;
            foreach (string dir in Directory.GetDirectories(root))
            {
                folders.Add(Path.GetFileName(dir));
            }
            folders.Sort(StringComparer.OrdinalIgnoreCase);
            return folders;
        }

        public static List<TextureItem> Scan(string root, HashSet<string> selectedTopFolders, OptimizerOptions options)
        {
            var items = new List<TextureItem>();
            if (!Directory.Exists(root)) return items;
            foreach (string file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file);
                if (!TextureExtensions.Contains(ext)) continue;

                string rel = MakeRelativePath(root, file);
                string top = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                if (selectedTopFolders != null && selectedTopFolders.Count > 0 && !selectedTopFolders.Contains(top)) continue;
                if (!options.IncludeStockDlc && (top.Equals("Squad", StringComparison.OrdinalIgnoreCase) || top.Equals("SquadExpansion", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                TextureItem item = TryReadTexture(file, root);
                if (item != null) items.Add(item);
            }
            items.Sort(delegate(TextureItem a, TextureItem b) { return StringComparer.OrdinalIgnoreCase.Compare(a.RelativePath, b.RelativePath); });
            return items;
        }

        public static TextureItem TryReadTexture(string file, string root)
        {
            var item = new TextureItem();
            var info = new FileInfo(file);
            item.FullPath = file;
            item.RelativePath = MakeRelativePath(root, file);
            item.Extension = Path.GetExtension(file).ToLowerInvariant();
            item.SizeBytes = info.Length;
            item.LastWriteTimeUtc = info.LastWriteTimeUtc;

            try
            {
                if (item.Extension == ".dds")
                {
                    DdsInfo dds = DdsHeader.Read(file);
                    item.Width = dds.Width;
                    item.Height = dds.Height;
                    item.MipCount = dds.MipCount;
                    item.Format = dds.Format;
                    item.Status = "Scanned";
                    return item;
                }
                if (item.Extension == ".png")
                {
                    using (Image img = Image.FromFile(file))
                    {
                        item.Width = img.Width;
                        item.Height = img.Height;
                        item.MipCount = 1;
                        item.Format = "PNG";
                    }
                    item.Status = "Scanned";
                    return item;
                }
                if (item.Extension == ".tga")
                {
                    TgaInfo tga = TgaCodec.ReadInfo(file);
                    item.Width = tga.Width;
                    item.Height = tga.Height;
                    item.MipCount = 1;
                    item.Format = "TGA" + tga.BitsPerPixel;
                    item.Status = "Scanned";
                    return item;
                }

                item.Format = item.Extension.TrimStart('.').ToUpperInvariant();
                item.Status = "Skipped";
                item.Warning = "Unsupported texture/container type.";
                return item;
            }
            catch (Exception ex)
            {
                item.Status = "Skipped";
                item.Warning = "Read failed: " + ex.Message;
                return item;
            }
        }

        public static void Preview(List<TextureItem> items, OptimizerOptions options)
        {
            foreach (TextureItem item in items)
            {
                item.Checked = false;
                item.TargetWidth = item.Width;
                item.TargetHeight = item.Height;
                item.EstimatedBytes = 0;
                item.Warning = "";

                if (item.Width <= 0 || item.Height <= 0)
                {
                    MarkSkipped(item, "Unknown dimensions.");
                    continue;
                }
                if (item.Width <= options.TargetMaxResolution && item.Height <= options.TargetMaxResolution)
                {
                    MarkSkipped(item, "Already at or below target.");
                    continue;
                }
                if ((item.Extension == ".png" || item.Extension == ".tga") && !options.IncludePngTga)
                {
                    MarkSkipped(item, "PNG/TGA disabled.");
                    continue;
                }
                if (item.Extension == ".mbm" || item.Extension == ".truecolor")
                {
                    MarkSkipped(item, "Unsupported container.");
                    continue;
                }
                if (Math.Max(item.Width, item.Height) <= 512)
                {
                    MarkSkipped(item, "Tiny UI/icon-sized texture.");
                    continue;
                }
                bool powerOfTwo = IsPowerOfTwo(item.Width) && IsPowerOfTwo(item.Height);
                if (item.Extension == ".dds" && !powerOfTwo && !options.IncludeNonPowerOfTwo)
                {
                    MarkSkipped(item, "Non-power-of-two DDS.");
                    continue;
                }
                if (item.Extension == ".dds" && (item.Width % 4 != 0 || item.Height % 4 != 0))
                {
                    MarkSkipped(item, "DDS block compression requires dimensions divisible by 4.");
                    continue;
                }
                if (item.Extension == ".dds" && !IsKnownDdsFormat(item.Format))
                {
                    MarkSkipped(item, "Unsupported DDS format: " + item.Format);
                    continue;
                }

                ScaleToMax(item.Width, item.Height, options.TargetMaxResolution, out item.TargetWidth, out item.TargetHeight);
                if (item.Extension == ".dds")
                {
                    item.TargetWidth = RoundDownToMultiple(item.TargetWidth, 4);
                    item.TargetHeight = RoundDownToMultiple(item.TargetHeight, 4);
                }
                if (item.TargetWidth <= 0 || item.TargetHeight <= 0 || (item.TargetWidth == item.Width && item.TargetHeight == item.Height))
                {
                    MarkSkipped(item, "No safe downscale target.");
                    continue;
                }

                double ratio = (double)item.TargetWidth * item.TargetHeight / Math.Max(1.0, (double)item.Width * item.Height);
                item.EstimatedBytes = Math.Max(1, (long)(item.SizeBytes * ratio));
                item.Status = "Ready";
                item.Checked = true;
            }
        }

        private static void MarkSkipped(TextureItem item, string reason)
        {
            item.Status = "Skipped";
            item.Warning = reason;
        }

        public static bool IsKnownDdsFormat(string format)
        {
            if (format == null) return false;
            return format.Equals("DXT1", StringComparison.OrdinalIgnoreCase)
                || format.Equals("DXT5", StringComparison.OrdinalIgnoreCase)
                || TryTexconvFormat(format) != null;
        }

        public static string TryTexconvFormat(string format)
        {
            if (string.IsNullOrEmpty(format)) return null;
            if (format.Equals("DXT1", StringComparison.OrdinalIgnoreCase)) return "DXT1";
            if (format.Equals("DXT5", StringComparison.OrdinalIgnoreCase)) return "DXT5";
            if (!format.StartsWith("DX10_", StringComparison.OrdinalIgnoreCase)) return null;

            string id = format.Substring(5);
            if (id == "71") return "BC1_UNORM";
            if (id == "72") return "BC1_UNORM_SRGB";
            if (id == "74") return "BC2_UNORM";
            if (id == "75") return "BC2_UNORM_SRGB";
            if (id == "77") return "BC3_UNORM";
            if (id == "78") return "BC3_UNORM_SRGB";
            if (id == "80") return "BC4_UNORM";
            if (id == "81") return "BC4_SNORM";
            if (id == "83") return "BC5_UNORM";
            if (id == "84") return "BC5_SNORM";
            if (id == "95") return "BC6H_UF16";
            if (id == "96") return "BC6H_SF16";
            if (id == "98") return "BC7_UNORM";
            if (id == "99") return "BC7_UNORM_SRGB";
            return null;
        }

        public static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        public static void ScaleToMax(int width, int height, int max, out int targetWidth, out int targetHeight)
        {
            double factor = Math.Min((double)max / width, (double)max / height);
            targetWidth = Math.Max(1, (int)Math.Round(width * factor));
            targetHeight = Math.Max(1, (int)Math.Round(height * factor));
        }

        public static int RoundDownToMultiple(int value, int multiple)
        {
            return Math.Max(multiple, value - (value % multiple));
        }

        public static string MakeRelativePath(string root, string file)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullFile = Path.GetFullPath(file);
            if (fullFile.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return fullFile.Substring(fullRoot.Length);
            return Path.GetFileName(file);
        }
    }

    public sealed class DdsInfo
    {
        public int Width;
        public int Height;
        public int MipCount;
        public string Format;
        public bool HasDx10Header;
    }

    public static class DdsHeader
    {
        public static DdsInfo Read(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            using (BinaryReader br = new BinaryReader(fs))
            {
                string magic = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (magic != "DDS ") throw new InvalidDataException("Missing DDS magic.");
                byte[] header = br.ReadBytes(124);
                if (header.Length != 124) throw new InvalidDataException("Truncated DDS header.");

                uint height = BitConverter.ToUInt32(header, 8);
                uint width = BitConverter.ToUInt32(header, 12);
                uint mipCount = BitConverter.ToUInt32(header, 24);
                string fourCc = Encoding.ASCII.GetString(header, 80, 4).TrimEnd('\0', ' ');
                string format = fourCc.Length == 0 ? "UNKNOWN" : fourCc;
                bool dx10 = fourCc == "DX10";
                if (dx10)
                {
                    byte[] dx10Header = br.ReadBytes(20);
                    if (dx10Header.Length != 20) throw new InvalidDataException("Truncated DDS DX10 header.");
                    uint dxgiFormat = BitConverter.ToUInt32(dx10Header, 0);
                    format = "DX10_" + dxgiFormat.ToString();
                }

                return new DdsInfo
                {
                    Width = checked((int)width),
                    Height = checked((int)height),
                    MipCount = mipCount == 0 ? 1 : checked((int)mipCount),
                    Format = format,
                    HasDx10Header = dx10
                };
            }
        }
    }

    public sealed class TgaInfo
    {
        public int Width;
        public int Height;
        public int BitsPerPixel;
        public int ImageType;
    }

    public static class TgaCodec
    {
        public static TgaInfo ReadInfo(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            using (BinaryReader br = new BinaryReader(fs))
            {
                byte[] h = br.ReadBytes(18);
                if (h.Length != 18) throw new InvalidDataException("Truncated TGA header.");
                int type = h[2];
                if (type != 2 && type != 3) throw new InvalidDataException("Only uncompressed TGA is supported.");
                int width = h[12] | (h[13] << 8);
                int height = h[14] | (h[15] << 8);
                int bpp = h[16];
                if (width <= 0 || height <= 0) throw new InvalidDataException("Invalid TGA dimensions.");
                if (bpp != 24 && bpp != 32 && bpp != 8) throw new InvalidDataException("Only 8/24/32-bit TGA is supported.");
                return new TgaInfo { Width = width, Height = height, BitsPerPixel = bpp, ImageType = type };
            }
        }

        public static Bitmap Load(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            using (BinaryReader br = new BinaryReader(fs))
            {
                byte[] h = br.ReadBytes(18);
                int idLength = h[0];
                int type = h[2];
                int width = h[12] | (h[13] << 8);
                int height = h[14] | (h[15] << 8);
                int bpp = h[16];
                bool topOrigin = (h[17] & 0x20) != 0;
                if (idLength > 0) br.ReadBytes(idLength);
                if (type != 2 && type != 3) throw new InvalidDataException("Only uncompressed TGA is supported.");

                Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                for (int y = 0; y < height; y++)
                {
                    int destY = topOrigin ? y : height - 1 - y;
                    for (int x = 0; x < width; x++)
                    {
                        Color color;
                        if (bpp == 8)
                        {
                            byte v = br.ReadByte();
                            color = Color.FromArgb(255, v, v, v);
                        }
                        else
                        {
                            byte b = br.ReadByte();
                            byte g = br.ReadByte();
                            byte r = br.ReadByte();
                            byte a = bpp == 32 ? br.ReadByte() : (byte)255;
                            color = Color.FromArgb(a, r, g, b);
                        }
                        bitmap.SetPixel(x, destY, color);
                    }
                }
                return bitmap;
            }
        }

        public static void Save(Bitmap bitmap, string path)
        {
            using (FileStream fs = File.Create(path))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                byte[] h = new byte[18];
                h[2] = 2;
                h[12] = (byte)(bitmap.Width & 0xff);
                h[13] = (byte)((bitmap.Width >> 8) & 0xff);
                h[14] = (byte)(bitmap.Height & 0xff);
                h[15] = (byte)((bitmap.Height >> 8) & 0xff);
                h[16] = 32;
                h[17] = 0x28;
                bw.Write(h);
                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        Color c = bitmap.GetPixel(x, y);
                        bw.Write(c.B);
                        bw.Write(c.G);
                        bw.Write(c.R);
                        bw.Write(c.A);
                    }
                }
            }
        }
    }

    public sealed class OptimizerEngine
    {
        public static bool AllowKspRunningForTests;

        private readonly string appRoot;
        private readonly string converterPath;

        public OptimizerEngine(string appRoot)
        {
            this.appRoot = appRoot;
            converterPath = ResolveConverterPath(appRoot);
        }

        public string ConverterPath
        {
            get { return converterPath; }
        }

        public string ConverterVersion
        {
            get
            {
                if (!File.Exists(converterPath)) return "texconv.exe missing";
                try
                {
                    FileVersionInfo v = FileVersionInfo.GetVersionInfo(converterPath);
                    return string.IsNullOrEmpty(v.FileVersion) ? "texconv.exe present" : v.FileVersion;
                }
                catch
                {
                    return "texconv.exe present";
                }
            }
        }

        public static bool IsKspRunning()
        {
            return Process.GetProcessesByName("KSP_x64").Length > 0;
        }

        public Manifest Optimize(string root, List<TextureItem> items, OptimizerOptions options, Action<string> log)
        {
            if (!AllowKspRunningForTests && IsKspRunning()) throw new InvalidOperationException("KSP_x64.exe is running. Close KSP before optimizing textures.");

            var selected = new List<TextureItem>();
            foreach (TextureItem item in items)
            {
                if (item.Checked && item.IsOptimizable) selected.Add(item);
            }
            if (selected.Count == 0) throw new InvalidOperationException("No ready textures are selected.");

            bool needsTexconv = false;
            foreach (TextureItem item in selected)
            {
                if (item.Extension == ".dds") needsTexconv = true;
            }
            if (needsTexconv && !File.Exists(converterPath))
            {
                throw new FileNotFoundException("DDS optimization requires DirectXTex texconv.exe. Put it at " + Path.Combine(appRoot, "Tools", "texconv.exe") + " or install it with winget.", converterPath);
            }

            string runId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string stageRoot = Path.Combine(appRoot, "staging", runId);
            string backupRoot = Path.Combine(appRoot, "backups", runId);
            string runRoot = Path.Combine(appRoot, "runs", runId);
            Directory.CreateDirectory(stageRoot);
            Directory.CreateDirectory(backupRoot);
            Directory.CreateDirectory(runRoot);

            var manifest = new Manifest
            {
                runId = runId,
                createdAt = DateTime.UtcNow.ToString("o"),
                kspRoot = root,
                targetMaxResolution = options.TargetMaxResolution,
                converterVersion = ConverterVersion,
                files = new List<ManifestEntry>()
            };

            foreach (TextureItem item in selected)
            {
                log("Converting " + item.RelativePath);
                string stagePath = Path.Combine(stageRoot, item.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(stagePath));

                ConvertToStage(item, stagePath, options);
                TextureItem staged = TextureScanner.TryReadTexture(stagePath, stageRoot);
                if (staged == null || staged.Width != item.TargetWidth || staged.Height != item.TargetHeight)
                {
                    throw new InvalidOperationException("Staged output failed validation for " + item.RelativePath);
                }

                string backupPath = Path.Combine(backupRoot, item.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                string originalHash = Sha256File(item.FullPath);
                File.Copy(item.FullPath, backupPath, true);
                File.Copy(stagePath, item.FullPath, true);
                if (options.PreserveTimestamps)
                {
                    File.SetLastWriteTimeUtc(item.FullPath, item.LastWriteTimeUtc);
                }
                string optimizedHash = Sha256File(item.FullPath);

                manifest.files.Add(new ManifestEntry
                {
                    relativePath = item.RelativePath,
                    originalHash = originalHash,
                    optimizedHash = optimizedHash,
                    originalFormat = item.Format,
                    targetFormat = staged.Format,
                    originalDimensions = item.Width + "x" + item.Height,
                    targetDimensions = staged.Width + "x" + staged.Height,
                    originalMipCount = item.MipCount,
                    targetMipCount = staged.MipCount,
                    backupPath = backupPath,
                    status = "optimized",
                    originalSize = item.SizeBytes,
                    optimizedSize = new FileInfo(item.FullPath).Length
                });
            }

            string manifestPath = Path.Combine(runRoot, "manifest.json");
            SaveManifest(manifest, manifestPath);
            try { Directory.Delete(stageRoot, true); } catch { }
            log("Manifest written: " + manifestPath);
            return manifest;
        }

        private void ConvertToStage(TextureItem item, string stagePath, OptimizerOptions options)
        {
            if (item.Extension == ".dds")
            {
                ConvertDdsWithTexconv(item, stagePath, options);
                return;
            }
            if (item.Extension == ".png")
            {
                using (Image source = Image.FromFile(item.FullPath))
                using (Bitmap resized = ResizeBitmap(source, item.TargetWidth, item.TargetHeight))
                {
                    resized.Save(stagePath, ImageFormat.Png);
                }
                return;
            }
            if (item.Extension == ".tga")
            {
                using (Bitmap source = TgaCodec.Load(item.FullPath))
                using (Bitmap resized = ResizeBitmap(source, item.TargetWidth, item.TargetHeight))
                {
                    TgaCodec.Save(resized, stagePath);
                }
                return;
            }
            throw new InvalidOperationException("Unsupported conversion type: " + item.Extension);
        }

        private void ConvertDdsWithTexconv(TextureItem item, string stagePath, OptimizerOptions options)
        {
            string outputDir = Path.GetDirectoryName(stagePath);
            string format = TextureScanner.TryTexconvFormat(item.Format);
            if (format == null) throw new InvalidOperationException("Unsupported DDS format: " + item.Format);
            int mipArg = options.ForceMipmaps ? 0 : (item.MipCount > 1 ? 0 : 1);

            var args = new StringBuilder();
            args.Append("-y ");
            args.Append("-w ").Append(item.TargetWidth).Append(' ');
            args.Append("-h ").Append(item.TargetHeight).Append(' ');
            args.Append("-m ").Append(mipArg).Append(' ');
            args.Append("-f ").Append(format).Append(' ');
            args.Append("-o ").Append(Quote(outputDir)).Append(' ');
            args.Append(Quote(item.FullPath));

            var psi = new ProcessStartInfo(converterPath, args.ToString());
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using (Process p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    throw new InvalidOperationException("texconv failed for " + item.RelativePath + Environment.NewLine + stdout + Environment.NewLine + stderr);
                }
            }

            string produced = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(item.FullPath) + ".DDS");
            if (!File.Exists(produced))
                produced = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(item.FullPath) + ".dds");
            if (!File.Exists(produced))
                throw new FileNotFoundException("texconv did not produce expected DDS output for " + item.RelativePath);

            if (!produced.Equals(stagePath, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(stagePath)) File.Delete(stagePath);
                File.Move(produced, stagePath);
            }
        }

        public static Bitmap ResizeBitmap(Image source, int width, int height)
        {
            Bitmap dest = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            dest.SetResolution(source.HorizontalResolution, source.VerticalResolution);
            using (Graphics g = Graphics.FromImage(dest))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, width, height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
            }
            return dest;
        }

        private static string ResolveConverterPath(string appRoot)
        {
            string local = Path.Combine(appRoot, "Tools", "texconv.exe");
            if (File.Exists(local)) return local;

            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try
                {
                    string candidate = Path.Combine(dir.Trim(), "texconv.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch
                {
                    // Ignore malformed PATH entries.
                }
            }

            return local;
        }

        public RestoreResult Restore(string manifestPath)
        {
            if (!AllowKspRunningForTests && IsKspRunning()) throw new InvalidOperationException("KSP_x64.exe is running. Close KSP before restoring backups.");

            Manifest manifest = LoadManifest(manifestPath);
            var result = new RestoreResult();
            string root = manifest.kspRoot;
            foreach (ManifestEntry entry in manifest.files)
            {
                string target = Path.Combine(root, entry.relativePath);
                if (!File.Exists(entry.backupPath))
                {
                    result.Skipped.Add(entry.relativePath + " (backup missing)");
                    continue;
                }
                if (File.Exists(target))
                {
                    string currentHash = Sha256File(target);
                    if (!currentHash.Equals(entry.optimizedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Skipped.Add(entry.relativePath + " (current file changed after optimization)");
                        continue;
                    }
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(entry.backupPath, target, true);
                string restoredHash = Sha256File(target);
                if (restoredHash.Equals(entry.originalHash, StringComparison.OrdinalIgnoreCase))
                    result.Restored.Add(entry.relativePath);
                else
                    result.Skipped.Add(entry.relativePath + " (restored hash mismatch)");
            }
            return result;
        }

        public static void SaveManifest(Manifest manifest, string path)
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            string json = js.Serialize(manifest);
            File.WriteAllText(path, PrettyJson(json), Encoding.UTF8);
        }

        public static Manifest LoadManifest(string path)
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            return js.Deserialize<Manifest>(File.ReadAllText(path, Encoding.UTF8));
        }

        public static string Sha256File(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string PrettyJson(string json)
        {
            var sb = new StringBuilder();
            int indent = 0;
            bool quoted = false;
            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];
                if (ch == '"' && (i == 0 || json[i - 1] != '\\')) quoted = !quoted;
                if (!quoted && (ch == '{' || ch == '['))
                {
                    sb.Append(ch).AppendLine();
                    indent++;
                    sb.Append(new string(' ', indent * 2));
                }
                else if (!quoted && (ch == '}' || ch == ']'))
                {
                    sb.AppendLine();
                    indent--;
                    sb.Append(new string(' ', indent * 2)).Append(ch);
                }
                else if (!quoted && ch == ',')
                {
                    sb.Append(ch).AppendLine();
                    sb.Append(new string(' ', indent * 2));
                }
                else if (!quoted && ch == ':')
                {
                    sb.Append(": ");
                }
                else
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }
    }

    public sealed class RestoreResult
    {
        public readonly List<string> Restored = new List<string>();
        public readonly List<string> Skipped = new List<string>();
    }
}
