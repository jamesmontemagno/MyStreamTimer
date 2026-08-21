// Composes the macOS AppIcon (purple gradient + glyph layer, per AppIcon.icon/icon.json) into a 1024px master
// and emits every Windows MSIX asset (tiles, Square44x44 target sizes incl. unplated, splash, store logo, Art.png, .ico).
// Usage: dotnet run -- <glyph.png> <outAssetsDir> <outArtDir>
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Path = System.IO.Path;

var glyphPath = args[0];
var assets = args[1];
var art = args[2];
Directory.CreateDirectory(assets);
Directory.CreateDirectory(art);

// icon.json: linear gradient top (0.5,0) -> (0.5,0.7), display-p3 colours approximated to sRGB
var top = new Rgba32(0xB6, 0xA3, 0xFF);     // 0.712,0.640,1.0
var bottom = new Rgba32(0xAE, 0x3E, 0xF7);  // 0.683,0.243,0.968
const int S = 1024;

using var glyph = Image.Load<Rgba32>(glyphPath);

Image<Rgba32> Compose(int size, bool roundedSquircle, bool transparentBackground)
{
    var img = new Image<Rgba32>(size, size, Color.Transparent);
    if (!transparentBackground)
    {
        img.Mutate(ctx =>
        {
            var brush = new LinearGradientBrush(new PointF(size * 0.5f, 0), new PointF(size * 0.5f, size * 0.7f),
                GradientRepetitionMode.None, new ColorStop(0, top), new ColorStop(1, bottom));
            if (roundedSquircle)
            {
                var r = size * 0.2237f; // Apple-ish continuous corner approximation
                var path = new RectangularPolygon(0, 0, size, size);
                ctx.Fill(brush, new EllipsePolygon(size / 2f, size / 2f, size / 2f)); // placeholder to init
                ctx.Clear(Color.Transparent);
                ctx.Fill(brush, BuildRoundedRect(size, r));
            }
            else
            {
                ctx.Fill(brush);
            }
        });
    }

    // glyph: scale 1.25 of 1024 canvas, translated (8, 32.5) points on a 1024pt canvas
    var scale = 1.25f * size / S;
    var gw = (int)Math.Round(glyph.Width * scale);
    var gh = (int)Math.Round(glyph.Height * scale);
    using var g = glyph.Clone(c => c.Resize(gw, gh, KnownResamplers.Lanczos3));
    var tx = (int)Math.Round(8f * size / S + (size - gw) / 2f);
    var ty = (int)Math.Round(32.5f * size / S + (size - gh) / 2f);
    img.Mutate(c => c.DrawImage(g, new Point(tx, ty), 1f));
    return img;
}

static IPath BuildRoundedRect(float size, float radius)
{
    var pb = new PathBuilder();
    float s = size, r = radius;
    pb.AddLine(r, 0, s - r, 0);
    pb.AddArc(new PointF(s - r, r), r, r, 0, -90, 90);
    pb.AddLine(s, r, s, s - r);
    pb.AddArc(new PointF(s - r, s - r), r, r, 0, 0, 90);
    pb.AddLine(s - r, s, r, s);
    pb.AddArc(new PointF(r, s - r), r, r, 0, 90, 90);
    pb.AddLine(0, s - r, 0, r);
    pb.AddArc(new PointF(r, r), r, r, 0, 180, 90);
    pb.CloseFigure();
    return pb.Build();
}

var enc = new PngEncoder { ColorType = PngColorType.RgbWithAlpha };

// Master + Art
using (var master = Compose(S, roundedSquircle: true, transparentBackground: false))
{
    master.Save(Path.Combine(art, "NewArt1024.png"), enc);
    master.Clone(c => c.Resize(300, 300)).Save(Path.Combine(art, "NewArt300.png"), enc);
    master.Clone(c => c.Resize(512, 512)).Save(Path.Combine(assets, "Art.png"), enc); // protocol logo
}

// Square tiles: full-bleed gradient, glyph with padding
void SquareTile(string name, int baseSize, double glyphFraction, params int[] scales)
{
    foreach (var sc in scales)
    {
        var px = (int)Math.Round(baseSize * sc / 100.0);
        using var img = new Image<Rgba32>(px, px);
        img.Mutate(ctx => ctx.Fill(new LinearGradientBrush(new PointF(px * 0.5f, 0), new PointF(px * 0.5f, px * 0.7f),
            GradientRepetitionMode.None, new ColorStop(0, top), new ColorStop(1, bottom))));
        var gs = (int)Math.Round(px * glyphFraction);
        using var g = glyph.Clone(c => c.Resize(gs, gs, KnownResamplers.Lanczos3));
        img.Mutate(c => c.DrawImage(g, new Point((px - gs) / 2, (px - gs) / 2), 1f));
        img.Save(Path.Combine(assets, $"{name}.scale-{sc}.png"), enc);
    }
}

SquareTile("Square150x150Logo", 150, 0.80, 100, 125, 150, 200, 400);
SquareTile("SmallTile", 71, 0.80, 100, 125, 150, 200, 400);
SquareTile("LargeTile", 310, 0.70, 100, 125, 150, 200, 400);
SquareTile("StoreLogo", 50, 0.85, 100, 125, 150, 200, 400);
SquareTile("Square44x44Logo", 44, 0.90, 100, 125, 150, 200, 400);

// Wide tile 310x150 & splash 620x300: gradient, glyph centered
void WideTile(string name, int w, int h, double glyphFraction, params int[] scales)
{
    foreach (var sc in scales)
    {
        var pw = (int)Math.Round(w * sc / 100.0); var ph = (int)Math.Round(h * sc / 100.0);
        using var img = new Image<Rgba32>(pw, ph);
        img.Mutate(ctx => ctx.Fill(new LinearGradientBrush(new PointF(pw * 0.5f, 0), new PointF(pw * 0.5f, ph * 0.7f),
            GradientRepetitionMode.None, new ColorStop(0, top), new ColorStop(1, bottom))));
        var gs = (int)Math.Round(ph * glyphFraction);
        using var g = glyph.Clone(c => c.Resize(gs, gs, KnownResamplers.Lanczos3));
        img.Mutate(c => c.DrawImage(g, new Point((pw - gs) / 2, (ph - gs) / 2), 1f));
        img.Save(Path.Combine(assets, $"{name}.scale-{sc}.png"), enc);
    }
}
WideTile("Wide310x150Logo", 310, 150, 0.80, 100, 125, 150, 200, 400);
WideTile("SplashScreen", 620, 300, 0.70, 100, 125, 150, 200, 400);
SquareTile("LockScreenLogo", 24, 0.90, 200);

// Square44x44 target sizes (taskbar/start list). Plated = gradient square; unplated = transparent with composed icon.
foreach (var ts in new[] { 16, 24, 32, 48, 256 })
{
    using (var plated = Compose(ts, roundedSquircle: true, transparentBackground: false))
        plated.Save(Path.Combine(assets, $"Square44x44Logo.targetsize-{ts}.png"), enc);
    using (var unplated = Compose(ts, roundedSquircle: true, transparentBackground: false))
    {
        unplated.Save(Path.Combine(assets, $"Square44x44Logo.altform-unplated_targetsize-{ts}.png"), enc);
        unplated.Save(Path.Combine(assets, $"Square44x44Logo.altform-lightunplated_targetsize-{ts}.png"), enc);
    }
}

// .ico (16..256)
var icoSizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
var frames = new List<byte[]>();
foreach (var sz in icoSizes)
{
    using var f = Compose(sz, roundedSquircle: true, transparentBackground: false);
    using var ms = new MemoryStream();
    f.Save(ms, enc);
    frames.Add(ms.ToArray());
}
using (var ico = new BinaryWriter(File.Create(Path.Combine(assets, "AppIcon.ico"))))
{
    ico.Write((short)0); ico.Write((short)1); ico.Write((short)icoSizes.Length);
    var offset = 6 + 16 * icoSizes.Length;
    for (var i = 0; i < icoSizes.Length; i++)
    {
        var sz = icoSizes[i];
        ico.Write((byte)(sz == 256 ? 0 : sz)); ico.Write((byte)(sz == 256 ? 0 : sz));
        ico.Write((byte)0); ico.Write((byte)0); ico.Write((short)1); ico.Write((short)32);
        ico.Write(frames[i].Length); ico.Write(offset);
        offset += frames[i].Length;
    }
    foreach (var fr in frames) ico.Write(fr);
}
File.Copy(Path.Combine(assets, "AppIcon.ico"), Path.Combine(art, "icon.ico"), true);
Console.WriteLine($"Generated {Directory.GetFiles(assets).Length} assets into {assets}");

