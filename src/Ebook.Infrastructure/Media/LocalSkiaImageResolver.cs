using Ebook.Application.Content.Images;
using Ebook.Application.Media;
using SkiaSharp;

namespace Ebook.Infrastructure.Media;

/// <summary>
/// E14-06 — Piso garantido da cadeia de mídia: gera um gradiente editorial com as cores do nicho
/// usando SkiaSharp local. Nunca falha, nunca retorna null — se todos os provedores externos
/// esgotaram ou falharam, este entrega sempre uma imagem válida.
/// </summary>
public sealed class LocalSkiaImageResolver : IMediaResolver
{
    public MediaProvider Provider => MediaProvider.LocalSkia;
    public bool Enabled => true;
    public int DailyLimit => 0; // sem limite — nunca esgota

    public Task<byte[]?> TryGenerateAsync(MediaBrief brief, CancellationToken ct)
    {
        var palette = NicheStyleCatalog.Palette(brief.NicheSlug);
        var bytes = RenderGradient(brief.Width, brief.Height, palette);
        return Task.FromResult<byte[]?>(bytes);
    }

    private static byte[] RenderGradient(int width, int height, NichePalette palette)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;

        var primary = SKColor.Parse(palette.Background);
        var accent = SKColor.Parse(palette.Accent);

        // gradiente diagonal suave (primário → accent muito claro → primário escuro)
        using var bg = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(width, height),
                [primary, Blend(primary, accent, 0.15f), Darken(primary)],
                [0f, 0.5f, 1f],
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(SKRect.Create(0, 0, width, height), bg);

        // linha decorativa accent no 1/3 inferior
        using var line = new SKPaint
        {
            Color = accent.WithAlpha(60),
            IsAntialias = true,
            StrokeWidth = height * 0.006f,
            Style = SKPaintStyle.Stroke,
        };
        var y = height * 0.68f;
        canvas.DrawLine(width * 0.08f, y, width * 0.92f, y, line);

        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private static SKColor Darken(SKColor c) =>
        new((byte)(c.Red * 0.65f), (byte)(c.Green * 0.65f), (byte)(c.Blue * 0.65f), c.Alpha);

    private static SKColor Blend(SKColor a, SKColor b, float t) =>
        new(
            (byte)(a.Red + (b.Red - a.Red) * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue + (b.Blue - a.Blue) * t),
            255);
}
