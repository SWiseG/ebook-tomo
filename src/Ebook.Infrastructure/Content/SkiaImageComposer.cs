using System.Text;
using Ebook.Application.Content.Images;
using SkiaSharp;

namespace Ebook.Infrastructure.Content;

/// <summary>
/// ComposiÃ§Ã£o programÃ¡tica de imagens com SkiaSharp (E09-01): capa de e-book,
/// mockup 3D de marketing e cards sociais (feed 1080Ã—1080, story 1080Ã—1920).
/// Fundo por foto (com overlay) ou gradiente da paleta do nicho.
/// </summary>
public sealed class SkiaImageComposer : IImageComposer
{
    // Capa 2:3 (1600Ã—2400) â€” proporÃ§Ã£o padrÃ£o de e-book Kiwify/Hotmart (docs/14 WP-7).
    private const int CoverWidth = 1600;
    private const int CoverHeight = 2400;

    public byte[] RenderCover(CoverArt art, byte[]? backgroundPhoto = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(CoverWidth, CoverHeight));
        var canvas = surface.Canvas;

        const float margin = 120f;
        const float contentW = CoverWidth - 2 * margin;
        var bg = SKColor.Parse(art.Palette.Background);
        var accent = SKColor.Parse(art.Palette.Accent);
        var onDark = SKColor.Parse(art.Palette.OnDark);

        // Fundo: ilustraÃ§Ã£o visÃ­vel com scrims de gradiente nas zonas de texto (nÃ£o mais o overlay
        // chapado que matava a imagem). Sem foto â†’ gradiente rico da paleta (docs/14 WP-6).
        FillCoverBackground(canvas, CoverWidth, CoverHeight, art.Palette, backgroundPhoto);

        // â”€â”€ Topo: eyebrow + rÃ©gua + tÃ­tulo display + subtÃ­tulo â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var hasSeal = !string.IsNullOrWhiteSpace(art.Seal);

        var top = 230f;
        if (!string.IsNullOrWhiteSpace(art.Eyebrow))
        {
            using var eyeFace = Typeface(art.Palette.HeadingFont, SKFontStyleWeight.Bold);
            using var eyePaint = new SKPaint
            {
                Color = accent, IsAntialias = true, TextSize = 40, Typeface = eyeFace, TextAlign = SKTextAlign.Left,
            };
            DrawTracked(canvas, art.Eyebrow.ToUpperInvariant(), eyePaint, margin, top, 7f);
            top += 28;
        }

        using (var rule = new SKPaint { Color = accent, IsAntialias = true })
        {
            canvas.DrawRect(SKRect.Create(margin, top, 200, 14), rule);
        }
        top += 70;

        using var titleFace = Typeface(art.Palette.Display, SKFontStyleWeight.Bold);
        using var titlePaint = new SKPaint
        {
            Color = onDark, IsAntialias = true, TextSize = 132, Typeface = titleFace, TextAlign = SKTextAlign.Left,
        };
        // com selo no canto superior direito, o tÃ­tulo quebra ANTES da coluna do selo (nÃ£o some sob ele)
        var titleMaxW = hasSeal ? CoverWidth - margin - 300 - 30 - margin : contentW;
        var y = DrawWrapped(canvas, art.Title, titlePaint, margin, top + 110, titleMaxW, 150);

        if (!string.IsNullOrWhiteSpace(art.Subtitle))
        {
            using var subFace = Typeface(art.Palette.BodyFont, SKFontStyleWeight.Normal);
            using var subPaint = new SKPaint
            {
                Color = accent, IsAntialias = true, TextSize = 56, Typeface = subFace, TextAlign = SKTextAlign.Left,
            };
            DrawWrapped(canvas, art.Subtitle, subPaint, margin, y + 56, contentW, 76);
        }

        // â”€â”€ Selo de confianÃ§a: emblema accent no canto superior direito (como os exemplos) â”€â”€â”€â”€â”€â”€â”€
        if (!string.IsNullOrWhiteSpace(art.Seal))
        {
            DrawSeal(canvas, CoverWidth - margin - 150, 360, 150, art.Palette, art.Seal!);
        }

        // â”€â”€ Base: caixas de benefÃ­cio empilhadas, ancoradas acima do rodapÃ© de autor â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var author = art.Author ?? art.Brand;
        var footerTop = CoverHeight - (string.IsNullOrWhiteSpace(author) ? 150f : 220f);

        if (art.Features is { Count: > 0 } features)
        {
            var boxes = features.Take(3).ToList();
            const float boxH = 150f;
            const float gap = 26f;
            var blockH = boxes.Count * boxH + (boxes.Count - 1) * gap;
            var by = footerTop - 60 - blockH;
            foreach (var feature in boxes)
            {
                DrawFeatureBox(canvas, margin, by, contentW, boxH, art.Palette, feature);
                by += boxH + gap;
            }
        }

        // â”€â”€ RodapÃ©: rÃ©gua fina + autor â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (!string.IsNullOrWhiteSpace(author))
        {
            using (var rule = new SKPaint { Color = accent.WithAlpha(160), IsAntialias = true })
            {
                canvas.DrawRect(SKRect.Create(margin, footerTop, 120, 6), rule);
            }

            using var authorFace = Typeface(art.Palette.HeadingFont, SKFontStyleWeight.Bold);
            using var authorPaint = new SKPaint
            {
                Color = onDark.WithAlpha(225), IsAntialias = true,
                TextSize = 44, Typeface = authorFace, TextAlign = SKTextAlign.Left,
            };
            canvas.DrawText(Shorten(author!, 48), margin, footerTop + 64, authorPaint);
        }

        return Encode(surface);
    }

    // Caixa de benefÃ­cio clara (alto contraste sobre a ilustraÃ§Ã£o) com Ã­cone accent + texto escuro.
    private static void DrawFeatureBox(SKCanvas canvas, float x, float y, float w, float h, NichePalette palette, CoverFeature feature)
    {
        var bg = SKColor.Parse(palette.Background);
        var accent = SKColor.Parse(palette.Accent);
        var rect = new SKRoundRect(SKRect.Create(x, y, w, h), 18);

        using (var panel = new SKPaint { Color = SKColors.White.WithAlpha(238), IsAntialias = true })
        {
            canvas.DrawRoundRect(rect, panel);
        }
        using (var stripe = new SKPaint { Color = accent, IsAntialias = true })
        {
            canvas.DrawRoundRect(new SKRoundRect(SKRect.Create(x, y, 12, h), 6), stripe);
        }

        // disco accent + "check" desenhado (sem dependÃªncia de SVG no Skia)
        var cx = x + 78;
        var cy = y + h / 2f;
        using (var disc = new SKPaint { Color = accent, IsAntialias = true })
        {
            canvas.DrawCircle(cx, cy, 38, disc);
        }
        using (var tick = new SKPaint
        {
            Color = bg, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 8,
            StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
        })
        {
            using var path = new SKPath();
            path.MoveTo(cx - 17, cy + 1);
            path.LineTo(cx - 4, cy + 15);
            path.LineTo(cx + 19, cy - 15);
            canvas.DrawPath(path, tick);
        }

        using var face = Typeface(palette.HeadingFont, SKFontStyleWeight.SemiBold);
        using var textPaint = new SKPaint
        {
            Color = bg, IsAntialias = true, TextSize = 40, Typeface = face, TextAlign = SKTextAlign.Left,
        };
        var textX = x + 140;
        var textW = w - 140 - 40;
        // centraliza verticalmente o texto (1 ou 2 linhas) na caixa
        var lines = WrapLines(feature.Text, textPaint, textW, max: 2);
        var startY = cy - (lines.Count - 1) * 25 + 14;
        foreach (var line in lines)
        {
            canvas.DrawText(line, textX, startY, textPaint);
            startY += 50;
        }
    }

    // Emblema circular: anel accent + texto curto em caixa-alta centrado (selo de confianÃ§a).
    private static void DrawSeal(SKCanvas canvas, float cx, float cy, float r, NichePalette palette, string text)
    {
        var accent = SKColor.Parse(palette.Accent);
        var onDark = SKColor.Parse(palette.OnDark);

        using (var fill = new SKPaint { Color = SKColor.Parse(palette.Background).WithAlpha(235), IsAntialias = true })
        {
            canvas.DrawCircle(cx, cy, r, fill);
        }
        using (var ring = new SKPaint { Color = accent, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 8 })
        {
            canvas.DrawCircle(cx, cy, r, ring);
            canvas.DrawCircle(cx, cy, r - 18, new SKPaint { Color = accent.WithAlpha(120), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 });
        }

        using var face = Typeface(palette.HeadingFont, SKFontStyleWeight.Bold);
        using var textPaint = new SKPaint
        {
            Color = onDark, IsAntialias = true, TextSize = 34, Typeface = face, TextAlign = SKTextAlign.Center,
        };
        var lines = WrapLines(text.ToUpperInvariant(), textPaint, r * 1.5f, max: 3);
        var startY = cy - (lines.Count - 1) * 22 + 10;
        foreach (var line in lines)
        {
            canvas.DrawText(line, cx, startY, textPaint);
            startY += 44;
        }
    }

    public byte[] FitCover(byte[] imageBytes)
    {
        using var bmp = SKBitmap.Decode(imageBytes);
        if (bmp is null)
        {
            return imageBytes;
        }

        using var surface = SKSurface.Create(new SKImageInfo(CoverWidth, CoverHeight));
        DrawCovering(surface.Canvas, bmp, CoverWidth, CoverHeight);
        return Encode(surface);
    }

    public byte[] RenderSocial(SocialArt art, byte[]? backgroundPhoto = null)
    {
        var (w, h) = art.Template == ImageTemplate.Story ? (1080, 1920) : (1080, 1080);
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var canvas = surface.Canvas;
        FillBackground(canvas, w, h, art.Palette, backgroundPhoto);

        using var headFace = Typeface(art.Palette.Display, SKFontStyleWeight.Bold);
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

    public byte[] RenderInfographic(InfographicArt art)
    {
        const int w = 1500;
        const int h = 480;
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var canvas = surface.Canvas;
        FillBackground(canvas, w, h, art.Palette, null); // gradiente do nicho

        var metrics = art.Metrics.Take(3).ToList();
        if (metrics.Count == 0)
        {
            return Encode(surface);
        }

        var accent = SKColor.Parse(art.Palette.Accent);
        var onDark = SKColor.Parse(art.Palette.OnDark);
        var cellW = (float)w / metrics.Count;

        using var numFace = Typeface(art.Palette.HeadingFont, SKFontStyleWeight.Bold);
        using var labelFace = Typeface(art.Palette.BodyFont, SKFontStyleWeight.Normal);

        for (var i = 0; i < metrics.Count; i++)
        {
            var cx = cellW * i + cellW / 2f;

            // divisor vertical entre as cÃ©lulas
            if (i > 0)
            {
                using var divider = new SKPaint
                {
                    Color = accent.WithAlpha(70), IsAntialias = true,
                    StrokeWidth = 2, Style = SKPaintStyle.Stroke,
                };
                canvas.DrawLine(cellW * i, h * 0.24f, cellW * i, h * 0.76f, divider);
            }

            // nÃºmero de impacto (accent), encolhido atÃ© caber na cÃ©lula (ex.: "30 dias" Ã© largo)
            using var numPaint = new SKPaint
            {
                Color = accent, IsAntialias = true,
                Typeface = numFace, TextAlign = SKTextAlign.Center, TextSize = 150,
            };
            while (numPaint.TextSize > 56 && numPaint.MeasureText(metrics[i].Number) > cellW - 70)
            {
                numPaint.TextSize -= 6;
            }

            canvas.DrawText(metrics[i].Number, cx, h * 0.44f, numPaint);

            // traÃ§o accent sob o nÃºmero
            using (var rule = new SKPaint { Color = accent.WithAlpha(170), IsAntialias = true })
            {
                canvas.DrawRect(SKRect.Create(cx - 38, h * 0.50f, 76, 6), rule);
            }

            // rÃ³tulo claro, quebrado em linhas dentro da cÃ©lula
            using var labelPaint = new SKPaint
            {
                Color = onDark.WithAlpha(230), IsAntialias = true,
                Typeface = labelFace, TextAlign = SKTextAlign.Center, TextSize = 42,
            };
            DrawWrapped(canvas, metrics[i].Label, labelPaint, cx, h * 0.64f, cellW - 80, 52);
        }

        return Encode(surface);
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

        using var headFace = Typeface(cover ? palette.Display : palette.HeadingFont, cover ? SKFontStyleWeight.Bold : SKFontStyleWeight.SemiBold);
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
            canvas.DrawText("arraste â†’", w / 2f, h - 110, hintPaint);

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

    // Fundo da CAPA (docs/14 WP-6): ilustraÃ§Ã£o visÃ­vel + scrims de gradiente sÃ³ nas zonas de texto
    // (topo p/ tÃ­tulo, base p/ features/autor). Substitui o overlay chapado que apagava a imagem.
    private static void FillCoverBackground(SKCanvas canvas, int w, int h, NichePalette palette, byte[]? photo)
    {
        var bg = SKColor.Parse(palette.Background);

        if (photo is not null)
        {
            using var bmp = SKBitmap.Decode(photo);
            if (bmp is not null)
            {
                DrawCovering(canvas, bmp, w, h);

                using (var tint = new SKPaint { Color = bg.WithAlpha(55), IsAntialias = true })
                {
                    canvas.DrawRect(SKRect.Create(0, 0, w, h), tint); // coesÃ£o sutil com a paleta
                }
                using (var topScrim = new SKPaint
                {
                    IsAntialias = true,
                    Shader = SKShader.CreateLinearGradient(
                        new SKPoint(0, 0), new SKPoint(0, h * 0.46f),
                        [bg.WithAlpha(238), bg.WithAlpha(0)], null, SKShaderTileMode.Clamp),
                })
                {
                    canvas.DrawRect(SKRect.Create(0, 0, w, h * 0.46f), topScrim);
                }
                using (var botScrim = new SKPaint
                {
                    IsAntialias = true,
                    Shader = SKShader.CreateLinearGradient(
                        new SKPoint(0, h * 0.48f), new SKPoint(0, h),
                        [bg.WithAlpha(0), bg.WithAlpha(242)], null, SKShaderTileMode.Clamp),
                })
                {
                    canvas.DrawRect(SKRect.Create(0, h * 0.48f, w, h * 0.52f), botScrim);
                }

                return;
            }
        }

        using var coverGradient = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(w, h),
                [bg, Darken(bg)], null, SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(SKRect.Create(0, 0, w, h), coverGradient);
    }

    // EspaÃ§amento de letra (tracking) no eyebrow via thin space â€” toque editorial premium.
    private static void DrawTracked(SKCanvas canvas, string text, SKPaint paint, float x, float y, float tracking)
    {
        foreach (var ch in text)
        {
            var s = ch.ToString();
            canvas.DrawText(s, x, y, paint);
            x += paint.MeasureText(s) + tracking;
        }
    }


    private static string Shorten(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)].TrimEnd() + "â€¦";

    // Quebra em atÃ© `max` linhas que cabem em maxWidth; estoura a Ãºltima com "â€¦" se exceder.
    private static List<string> WrapLines(string text, SKPaint paint, float maxWidth, int max)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var all = new List<string>();
        var line = new StringBuilder();
        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (line.Length > 0 && paint.MeasureText(candidate) > maxWidth)
            {
                all.Add(line.ToString());
                line.Clear();
                line.Append(word);
            }
            else
            {
                line.Clear();
                line.Append(candidate);
            }
        }

        if (line.Length > 0)
        {
            all.Add(line.ToString());
        }

        if (all.Count <= max)
        {
            return all;
        }

        var capped = all.Take(max).ToList();
        capped[max - 1] = capped[max - 1].TrimEnd() + "â€¦";
        return capped;
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

    // fonte embarcada (FontRegistry) primeiro â€” no Linux headless FromFamilyName nÃ£o acha as famÃ­lias
    // profissionais; o registry usa SKTypeface.FromFile. Fallback: famÃ­lia do sistema, depois Default.
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
