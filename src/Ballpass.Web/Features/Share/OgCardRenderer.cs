using Ballpass.Web.Data;
using SkiaSharp;

namespace Ballpass.Web.Features.Share;

/// <summary>
/// Draws the 1200x630 dark share card with SkiaSharp. Pure function of <see cref="OgCardModel"/>; no I/O besides loading
/// the two TTFs once (Assets/fonts, copied to the output directory; Skia cannot read the woff2 in wwwroot).
/// Colours mirror wwwroot/css/bp-tokens.css (dark theme).
/// </summary>
public sealed class OgCardRenderer : IOgCardRenderer
{
    public const int Width = 1200;
    public const int Height = 630;
    /// <summary>Solid watermark band height (7% of the card; the requirement is at least 5%).</summary>
    public const int BandHeight = 44;

    public static readonly SKColor BandColor = new(0xFF, 0x6A, 0x2B);   // --bp-accent
    private static readonly SKColor Bg = new(0x08, 0x0B, 0x10);          // --bp-bg
    private static readonly SKColor Text = new(0xF3, 0xF6, 0xFA);        // --bp-text
    private static readonly SKColor Muted = new(0xA3, 0xB0, 0xC2);       // --bp-muted
    private static readonly SKColor SrcSelf = new(0xA7, 0xB4, 0xC6);
    private static readonly SKColor SrcCoach = new(0x60, 0xA5, 0xFA);
    private static readonly SKColor SrcLeague = new(0xB7, 0x94, 0xF6);

    private const float Left = 64;
    private const float ContentWidth = Width - 2 * Left;

    private static readonly Lazy<Fonts> LoadedFonts = new(Fonts.Load);

    public byte[] Render(OgCardModel model)
    {
        var fonts = LoadedFonts.Value;
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var canvas = surface.Canvas;
        canvas.Clear(Bg);

        DrawGlow(canvas);
        DrawBand(canvas, fonts, 0, model.WatermarkText);
        DrawBand(canvas, fonts, Height - BandHeight, model.WatermarkText);
        DrawWordmark(canvas, fonts);
        DrawIdentity(canvas, fonts, model);
        DrawStats(canvas, fonts, model);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void DrawGlow(SKCanvas canvas)
    {
        // Subtle accent glow, top right. Kept faint so the text contrast is unaffected.
        using var shader = SKShader.CreateRadialGradient(new SKPoint(1080, 120), 520,
            [BandColor.WithAlpha(0x2E), BandColor.WithAlpha(0)], null, SKShaderTileMode.Clamp);
        using var paint = new SKPaint { Shader = shader };
        canvas.DrawRect(0, BandHeight, Width, Height - 2 * BandHeight, paint);
    }

    private static void DrawBand(SKCanvas canvas, Fonts fonts, float top, string text)
    {
        using var fill = new SKPaint { Color = BandColor };
        canvas.DrawRect(0, top, Width, BandHeight, fill);
        using var font = new SKFont(fonts.TextSemi, 20) { Subpixel = true };
        using var ink = new SKPaint { Color = Bg, IsAntialias = true };
        canvas.DrawText(text, Width / 2f, top + BandHeight / 2f + 7, SKTextAlign.Center, font, ink);
    }

    private static void DrawWordmark(SKCanvas canvas, Fonts fonts)
    {
        using var font = new SKFont(fonts.Display, 40) { Subpixel = true };
        using var orange = new SKPaint { Color = BandColor, IsAntialias = true };
        using var white = new SKPaint { Color = Text, IsAntialias = true };
        canvas.DrawText("B", Left, 106, SKTextAlign.Left, font, orange);
        canvas.DrawText("allpass", Left + font.MeasureText("B"), 106, SKTextAlign.Left, font, white);
    }

    private static void DrawIdentity(SKCanvas canvas, Fonts fonts, OgCardModel model)
    {
        using var white = new SKPaint { Color = Text, IsAntialias = true };
        using var muted = new SKPaint { Color = Muted, IsAntialias = true };

        // Name: shrink to fit the content width, ellipsize only if even the minimum size overflows.
        var size = 104f;
        using var nameFont = new SKFont(fonts.Display, size) { Subpixel = true };
        while (nameFont.MeasureText(model.Name) > ContentWidth && nameFont.Size > 52) nameFont.Size -= 2;
        canvas.DrawText(Fit(model.Name, nameFont, ContentWidth), Left, 226, SKTextAlign.Left, nameFont, white);

        using var sub = new SKFont(fonts.Text, 30) { Subpixel = true };
        canvas.DrawText(Fit(model.Subtitle, sub, ContentWidth), Left, 274, SKTextAlign.Left, sub, muted);
        using var club = new SKFont(fonts.TextSemi, 28) { Subpixel = true };
        canvas.DrawText(Fit(model.ClubLine, club, ContentWidth), Left, 316, SKTextAlign.Left, club, white);
    }

    private static void DrawStats(SKCanvas canvas, Fonts fonts, OgCardModel model)
    {
        using var white = new SKPaint { Color = Text, IsAntialias = true };
        using var muted = new SKPaint { Color = Muted, IsAntialias = true };
        using var accent = new SKPaint { Color = BandColor, IsAntialias = true };

        if (model.Stats.Count == 0)
        {
            using var none = new SKFont(fonts.Text, 30) { Subpixel = true };
            canvas.DrawText(model.SeasonLine, Left, 440, SKTextAlign.Left, none, muted);
            return;
        }

        using var value = new SKFont(fonts.Display, 96) { Subpixel = true };
        using var label = new SKFont(fonts.TextSemi, 26) { Subpixel = true };
        var colWidth = ContentWidth / 3;
        for (var i = 0; i < Math.Min(3, model.Stats.Count); i++)
        {
            var x = Left + i * colWidth;
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(x, 348, x + 48, 353), 2.5f), accent);
            canvas.DrawText(model.Stats[i].Value, x, 440, SKTextAlign.Left, value, white);
            canvas.DrawText(model.Stats[i].Label, x, 476, SKTextAlign.Left, label, muted);
        }

        // Source glyph + season/source label: the card never shows a stat without saying where it came from.
        var color = model.Source switch { StatSource.CoachConfirmed => SrcCoach, StatSource.LeagueVerifiedMock => SrcLeague, _ => SrcSelf };
        DrawSourceGlyph(canvas, model.Source, Left, 512, color);
        using var src = new SKFont(fonts.Text, 24) { Subpixel = true };
        using var srcPaint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawText(Fit(model.SeasonLine, src, ContentWidth - 40), Left + 40, 532, SKTextAlign.Left, src, srcPaint);
    }

    /// <summary>24x24 glyph at (x, y): circle (self-reported), circle + check (coach), shield + check (league mock).</summary>
    private static void DrawSourceGlyph(SKCanvas canvas, StatSource? source, float x, float y, SKColor color)
    {
        using var stroke = new SKPaint
        {
            Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.4f,
            StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
        };
        switch (source)
        {
            case StatSource.LeagueVerifiedMock:
                // SkiaSharp 4: SKPath mutators are obsolete; build with SKPathBuilder and detach an immutable path.
                var shield = new SKPathBuilder();
                shield.MoveTo(x + 12, y + 1); shield.LineTo(x + 21, y + 4.5f); shield.LineTo(x + 21, y + 12);
                shield.CubicTo(x + 21, y + 17, x + 17, y + 21, x + 12, y + 23);
                shield.CubicTo(x + 7, y + 21, x + 3, y + 17, x + 3, y + 12);
                shield.LineTo(x + 3, y + 4.5f); shield.Close();
                using (var path = shield.Detach())
                    canvas.DrawPath(path, stroke);
                DrawCheck(canvas, stroke, x, y);
                break;
            case StatSource.CoachConfirmed:
                canvas.DrawCircle(x + 12, y + 12, 10.5f, stroke);
                DrawCheck(canvas, stroke, x, y);
                break;
            default:
                canvas.DrawCircle(x + 12, y + 12, 8.5f, stroke);
                break;
        }
    }

    private static void DrawCheck(SKCanvas canvas, SKPaint stroke, float x, float y)
    {
        var builder = new SKPathBuilder();
        builder.MoveTo(x + 7.5f, y + 12.5f); builder.LineTo(x + 10.7f, y + 15.7f); builder.LineTo(x + 16.5f, y + 9);
        using var check = builder.Detach();
        canvas.DrawPath(check, stroke);
    }

    private static string Fit(string text, SKFont font, float maxWidth)
    {
        if (font.MeasureText(text) <= maxWidth) return text;
        var t = text;
        while (t.Length > 1 && font.MeasureText(t + "…") > maxWidth) t = t[..^1];
        return t.TrimEnd() + "…";
    }

    private sealed record Fonts(SKTypeface Display, SKTypeface Text, SKTypeface TextSemi)
    {
        public static Fonts Load()
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Assets", "fonts");
            var roboto = SKTypeface.FromFile(Path.Combine(dir, "RobotoCondensed-VF.ttf"))
                ?? throw new FileNotFoundException("RobotoCondensed-VF.ttf missing from Assets/fonts");
            var inter = SKTypeface.FromFile(Path.Combine(dir, "InterVariable.ttf"))
                ?? throw new FileNotFoundException("InterVariable.ttf missing from Assets/fonts");
            return new Fonts(
                Weight(roboto, 700),
                Weight(inter, 400),
                Weight(inter, 600));
        }

        // The TTFs are variable fonts: the plain load gives weight 400, so pick the wght axis explicitly.
        private static SKTypeface Weight(SKTypeface variable, float wght) =>
            variable.Clone([new SKFontVariationPositionCoordinate { Axis = new SKFourByteTag('w', 'g', 'h', 't'), Value = wght }]);
    }
}
