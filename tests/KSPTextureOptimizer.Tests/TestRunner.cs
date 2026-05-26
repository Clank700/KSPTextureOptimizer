using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using KSPTextureOptimizer;

internal static class TestRunner
{
    private static int failures;

    private static void Main()
    {
        string temp = Path.Combine(Path.GetTempPath(), "KSPTextureOptimizerTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            OptimizerEngine.AllowKspRunningForTests = true;
            TestDdsHeaderParsing(temp);
            TestPreviewDoesNotWrite(temp);
            TestPngOptimizeAndRestore(temp);
            Console.WriteLine(failures == 0 ? "All tests passed." : failures + " tests failed.");
            Environment.ExitCode = failures == 0 ? 0 : 1;
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    private static void TestDdsHeaderParsing(string temp)
    {
        string path = Path.Combine(temp, "sample.dds");
        WriteMinimalDds(path, 4096, 2048, 12, "DXT5");
        DdsInfo info = DdsHeader.Read(path);
        AssertEqual("dds width", 4096, info.Width);
        AssertEqual("dds height", 2048, info.Height);
        AssertEqual("dds mips", 12, info.MipCount);
        AssertEqual("dds format", "DXT5", info.Format);
    }

    private static void TestPreviewDoesNotWrite(string temp)
    {
        string root = Path.Combine(temp, "preview");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "large.dds");
        WriteMinimalDds(path, 4096, 4096, 13, "DXT1");
        DateTime beforeTime = File.GetLastWriteTimeUtc(path);
        string beforeHash = OptimizerEngine.Sha256File(path);

        var items = TextureScanner.Scan(root, null, new OptimizerOptions());
        TextureScanner.Preview(items, new OptimizerOptions { TargetMaxResolution = 2048 });

        AssertEqual("preview count", 1, items.Count);
        AssertEqual("preview status", "Ready", items[0].Status);
        AssertEqual("preview target width", 2048, items[0].TargetWidth);
        AssertEqual("preview unchanged hash", beforeHash, OptimizerEngine.Sha256File(path));
        AssertEqual("preview unchanged timestamp", beforeTime, File.GetLastWriteTimeUtc(path));
    }

    private static void TestPngOptimizeAndRestore(string temp)
    {
        string toolRoot = Path.Combine(temp, "tool");
        string gameRoot = Path.Combine(temp, "game");
        string modRoot = Path.Combine(gameRoot, "Mod");
        Directory.CreateDirectory(toolRoot);
        Directory.CreateDirectory(modRoot);
        string path = Path.Combine(modRoot, "big.png");
        using (Bitmap bmp = new Bitmap(1024, 1024, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(128, 20, 90, 160));
            bmp.Save(path, ImageFormat.Png);
        }
        string originalHash = OptimizerEngine.Sha256File(path);

        var options = new OptimizerOptions { TargetMaxResolution = 512, IncludePngTga = true };
        var items = TextureScanner.Scan(gameRoot, null, options);
        TextureScanner.Preview(items, options);
        Manifest manifest = new OptimizerEngine(toolRoot).Optimize(gameRoot, items, options, delegate { });

        AssertEqual("png manifest files", 1, manifest.files.Count);
        TextureItem optimized = TextureScanner.TryReadTexture(path, gameRoot);
        AssertEqual("png optimized width", 512, optimized.Width);
        AssertEqual("png optimized height", 512, optimized.Height);

        string manifestPath = Path.Combine(toolRoot, "runs", manifest.runId, "manifest.json");
        RestoreResult result = new OptimizerEngine(toolRoot).Restore(manifestPath);
        AssertEqual("restore count", 1, result.Restored.Count);
        AssertEqual("restore hash", originalHash, OptimizerEngine.Sha256File(path));
    }

    private static void WriteMinimalDds(string path, int width, int height, int mips, string fourCc)
    {
        using (FileStream fs = File.Create(path))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            bw.Write(new byte[] { (byte)'D', (byte)'D', (byte)'S', (byte)' ' });
            byte[] header = new byte[124];
            BitConverter.GetBytes((uint)124).CopyTo(header, 0);
            BitConverter.GetBytes((uint)height).CopyTo(header, 8);
            BitConverter.GetBytes((uint)width).CopyTo(header, 12);
            BitConverter.GetBytes((uint)mips).CopyTo(header, 24);
            BitConverter.GetBytes((uint)32).CopyTo(header, 72);
            byte[] cc = System.Text.Encoding.ASCII.GetBytes(fourCc);
            Array.Copy(cc, 0, header, 80, Math.Min(4, cc.Length));
            bw.Write(header);
            bw.Write(new byte[128]);
        }
    }

    private static void AssertEqual<T>(string name, T expected, T actual)
    {
        if (!object.Equals(expected, actual))
        {
            failures++;
            Console.WriteLine("FAIL " + name + ": expected " + expected + ", got " + actual);
        }
        else
        {
            Console.WriteLine("PASS " + name);
        }
    }
}
