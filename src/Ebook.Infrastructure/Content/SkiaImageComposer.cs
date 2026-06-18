using System.Text;
using Ebook.Application.Content.Images;
using SkiaSharp;

namespace Ebook.Infrastructure.Content;

/// <summary>
/// Composição programática de imagens com SkiaSharp (E09-01): capa de e-book,
/// mockup 3D de marketing e cards sociais (feed 1080×1080, story 1080×1920).
/// Fundo por foto (com overlay) ou gradiente da paleta do nicho.
/// </summary>
public sealed class SkiaImageComposer : IImageComposer
{
    private const int CoverWidth = 1600;
    private const int CoverHeight = 2560;

    public byte[] RenderCover(CoverArt art, byte[]? backgroundPhoto = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(CoverWidth, CoverHeight));
        var canvas = surface.Canvas;
        FillBackground(canvas, CoverWidth, CoverHeight, art.Palette, backgroundPhoto);

        const float margin = 130f;
        using (var accent = new SKPaint { Color = SKColor.Parse(art.Palette.Accent), IsAntialias = true })
        {
            canvas.DrawRect(SKRect.Create(margin, 360, 230, 16), accent);
        }

        using var titleFace = Typeface(art.Palette.HeadingFont, SKFontStyleWeight.Bold);
        using var titlePaint = new SKPaint
        {
            Color = SKColor.Parse(art.Palette.OnDark), IsAntialias = true,
            TextSize = 124, Typeface = titleFace, TextAlign = SKTextAlign.Left
        };
        var y = DrawWrapped(canvas, art.Title, titlePaint, margin, 540, CoverWidth - 2 * margin, 150);

        if (!string.IsNullOrWhiteSpace(art.Subtitle))
        {
            using var subFace = Typeface(art.Palette.BodyFont, SKFontStyleWeight.Normal);
            using var subPaint = new SKPaint
            {
                Color = SKColor.Parse(art.Palette.Accent), IsAntialias = true,
                TextSize = 58, Typeface = subFace, TextAlign = SKTextAlign.Left
            };
            DrawWrapped(canvas, art.Subtitle, subPaint, margin, y + 50, CoverWidth - 2 * margin, 78);
        }

        if (!string.IsNullOrWhiteSpace(art.Brand))
        {
            using var brandFace = Typeface(art.Palette.BodyFont, SKFontStyleWeight.Normal);
            using var brandPaint = new SKPaint
            {
                Color = SKColor.Parse(art.Palette.OnDark).WithAlpha(200), IsAntialias = true,
                TextSize = 46, Typeface = brandFace, TextAlign = SKTextAlign.Left
            };
            canvas.DrawText(art.Brand, margin, CoverHeight - 150, brandPaint);
        }

        return Encode(surface);
    }

    public byte[] RenderSocial(SocialArt art, byte[]? backgroundPhoto = null)
    {
        var (w, h) = art.Template == ImageTemplate.Story ? (1080, 1920) : (1080, 1080);
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var canvas = surface.Canvas;
        FillBackground(canvas, w, h, art.Palette, backgroundPhoto);

        using var headFace = Typeface(art.Palette.HeadingFont, SKFontStyleWeight.Bold);
        using var headPaint = new SKPaint
        {
            Color = SKColor.Parse(art.Palette.OnDark), IsAntialias = true,
            TextSize = 84, Typeface = headFace, TextAlign = SKTextAlign.Center
        };
        var y = DrawWrapped(canvas, art.Headline, headPaint, w / 2f, h * 0.42f, w - 160, 104);

        using (var accent = new SKPaint { Color = SKColor.Parse(art.Palette.Accent), IsAntialias = true })
        {
            canvas.DrawRect(SKRect.Create(w / 2f - 90, y + 18, 180, 10), accent);
        }

        if (!string.IsNullOrWhiteSpace(art.Subtext))
        {
            using var subFace = Typeface(art.Palette.BodyFont, SKFontStyleWeight.Normal);
            using var subPaint = new SKPaint
            {
                Color = SKColor.Parse(art.Palette.OnDark).WithAlpha(220), IsAntialias = true,
                TextSize = 44, Typeface = subFace, TextAlign = SKTextAlign.Center
            };
            DrawWrapped(canvas, art.Subtext, subPaint, w / 2f, y + 90, w - 220, 60);
        }

        return Encode(surface);
    }

    public IReadOnlyList<byte[]> RenderCarousel(CarouselArt art, byte[]? backgroundPhoto = null)
    {
        const int w = 1080;
        const int h = 1080;
        var images = new List<byte[]>
        {
            RenderCarouselSlide(w, h, art.Palette, backgroundPhoto, cover: true, number: 0, text: art.Headline, brand: art.Brand),
        };

        var n = 1;
        foreach (var slide in art.Slides)
        {
            images.Add(RenderCarouselSlide(w, h, art.Palette, backgroundPhoto, cover: false, number: n, text: slide, brand: null));
            n++;
        }

        return images;
    }

    private static byte[] RenderCarouselSlide(
        int w, int h, NichePalette palette, byte[]? photo, bool cover, int number, string text, string? brand)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var canvas = surface.Canvas;
        FillBackground(canvas, w, h, palette, photo);

        if (!cover)
        {
            using var badgeFace = Typeface(palette.HeadingFont, SKFontStyleWeight.Bold);
            using var badgePaint = new SKPaint
            {
                Color = SKColor.Parse(palette.Accent), IsAntialias = true,
                TextSize = 72, Typeface = badgeFace, TextAlign = SKTextAlign.Left,
            };
            canvas.DrawText($"{number:00}", 90, 170, badgePaint);
        }

        using var headFace = Typeface(palette.HeadingFont, cover ? SKFontStyleWeight.Bold : SKFontStyleWeight.SemiBold);
        using var headPaint = new SKPaint
        {
            Color = SKColor.Parse(palette.OnDark), IsAntialias = true,
            TextSize = cover ? 90 : 62, Typeface = headFace, TextAlign = SKTextAlign.Center,
        };
        var y = DrawWrapped(canvas, text, headPaint, w / 2f, cover ? h * 0.40f : h * 0.34f, w - 160, cover ? 110 : 82);

        using (var accent = new SKPaint { Color = SKColor.Parse(palette.Accent), IsAntialias = true })
        {
            canvas.DrawRect(SKRect.Create(w / 2f - 90, y + 18, 180, 10), accent);
        }

        using var bodyFace = Typeface(palette.BodyFont, SKFontStyleWeight.Normal);
        if (cover)
        {
            using var hintPaint = new SKPaint
            {
                Color = SKColor.Parse(palette.OnDark).WithAlpha(210), IsAntialias = true,
                TextSize = 40, Typeface = bodyFace, TextAlign = SKTextAlign.Center,
            };
            canvas.DrawText("arraste →", w / 2f, h - 110, hintPaint);

            if (!string.IsNullOrWhiteSpace(brand))
            {
                using var brandPaint = new SKPaint
                {
                    Color = SKColor.Parse(palette.OnDark).WithAlpha(180), IsAntialias = true,
                    TextSize = 34, Typeface = bodyFace, TextAlign = SKTextAlign.Center,
                };
                canvas.DrawText(brand, w / 2f, 120, brandPaint);
            }
        }

        return Encode(surface);
    }

    public byte[] RenderMockup(byte[] coverPng, NichePalette palette)
    {
        const int w = 1600;
        const int h = 1200;
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var canvas = surface.Canvas;

        using (var bg = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, h),
                [SKColor.Parse("#F3F4F6"), SKColor.Parse("#D1D5DB")], null, SKShaderTileMode.Clamp)
        })
        {
            canvas.DrawRect(SKRect.Create(0, 0, w, h), bg);
        }

        using var cover = SKBitmap.Decode(coverPng);
        if (cover is null)
        {
            return Encode(surface);
        }

        const float targetH = 880f;
        var scale = targetH / cover.Height;
        var bookW = cover.Width * scale;
        var left = (w - bookW) / 2f;
        var top = (h - targetH) / 2f;
        var dest = SKRect.Create(left, top, bookW, targetH);

        using (var shadow = new SKPaint { Color = SKColors.Black.WithAlpha(70), IsAntialias = true, ImageFilter = SKImageFilter.CreateBlur(18, 18) })
        {
            canvas.DrawRect(SKRect.Create(left + 26, top + 26, bookW, targetH), shadow);
        }

        using (var spine = new SKPaint { Color = Darken(SKColor.Parse(palette.Background)), IsAntialias = true })
        {
            canvas.DrawRect(SKRect.Create(left - 22, top + 6, 26, targetH), spine);
        }

        canvas.DrawBitmap(cover, dest);
        return Encode(surface);
    }

    private static void FillBackground(SKCanvas canvas, int w, int h, NichePalette palette, byte[]? photo)
    {
        var baseColor = SKColor.Parse(palette.Background);

        if (photo is not null)
        {
            using var bmp = SKBitmap.Decode(photo);
            if (bmp is not null)
            {
                DrawCovering(canvas, bmp, w, h);
                using var overlay = new SKPaint { Color = baseColor.WithAlpha(205), IsAntialias = true };
                canvas.DrawRect(SKRect.Create(0, 0, w, h), overlay);
                return;
            }
        }

        using var gradient = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, h),
                [baseColor, Darken(baseColor)], null, SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(SKRect.Create(0, 0, w, h), gradient);
    }

    private static void DrawCovering(SKCanvas canvas, SKBitmap bmp, int w, int h)
    {
        var scale = Math.Max((float)w / bmp.Width, (float)h / bmp.Height);
        var dw = bmp.Width * scale;
        var dh = bmp.Height * scale;
        canvas.DrawBitmap(bmp, SKRect.Create((w - dw) / 2f, (h - dh) / 2f, dw, dh));
    }

    private static float DrawWrapped(SKCanvas canvas, string text, SKPaint paint, float x, float yTop, float maxWidth, float lineHeight)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        var y = yTop;

        void Flush()
        {
            if (line.Length == 0)
            {
                return;
            }

            canvas.DrawText(line.ToString(), x, y, paint);
            y += lineHeight;
            line.Clear();
        }

        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (line.Length > 0 && paint.MeasureText(candidate) > maxWidth)
            {
                Flush();
                line.Append(word);
            }
            else
            {
                line.Clear();
                line.Append(candidate);
            }
        }

        Flush();
        return y;
    }

    // fonte embarcada (FontRegistry) primeiro — no Linux headless FromFamilyName não acha as famílias
    // profissionais; o registry usa SKTypeface.FromFile. Fallback: família do sistema, depois Default.
    private static SKTypeface Typeface(string family, SKFontStyleWeight weight) =>
        FontRegistry.Resolve(family, weight >= SKFontStyleWeight.SemiBold)
        ?? SKTypeface.FromFamilyName(family, weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        ?? SKTypeface.Default;

    private static SKColor Darken(SKColor c) =>
        new((byte)(c.Red * 0.55), (byte)(c.Green * 0.55), (byte)(c.Blue * 0.55));

    private static byte[] Encode(SKSurface surface)
    {
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
