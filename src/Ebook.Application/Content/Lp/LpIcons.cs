namespace Ebook.Application.Content.Lp;

/// <summary>
/// Sistema de ícones SVG inline da landing page (Fase 6 / docs/12). Símbolos nítidos e escaláveis,
/// coloridos via <c>currentColor</c>, sem requisições externas (bundle auto-contido). Usados nos
/// pontos de confiança (selos, garantia, bônus, pagamento) no lugar de glifos de fonte.
/// </summary>
internal static class LpIcons
{
    public const string Check =
        "<svg class=\"ic\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"3\" " +
        "stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M20 6 9 17l-5-5\"/></svg>";

    public const string Shield =
        "<svg class=\"ic\" viewBox=\"0 0 24 24\" fill=\"currentColor\" aria-hidden=\"true\">" +
        "<path d=\"M12 2 4 5v6c0 5 3.4 9.4 8 11 4.6-1.6 8-6 8-11V5l-8-3Z\"/></svg>";

    public const string Gift =
        "<svg class=\"ic\" viewBox=\"0 0 24 24\" fill=\"currentColor\" aria-hidden=\"true\">" +
        "<path d=\"M20 7h-2.3a3 3 0 0 0-5.7-1.2A3 3 0 0 0 6.3 7H4a1 1 0 0 0-1 1v2.5h8.2V7H13v3.5H21V8a1 1 0 0 0-1-1Z" +
        "M3 12v8a1 1 0 0 0 1 1h7.2v-9H3Zm9.8 9H20a1 1 0 0 0 1-1v-7h-8.2v8Z\"/></svg>";

    public const string Lock =
        "<svg class=\"ic\" viewBox=\"0 0 24 24\" fill=\"currentColor\" aria-hidden=\"true\">" +
        "<path d=\"M17 9V7A5 5 0 0 0 7 7v2a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-8a2 2 0 0 0-2-2ZM9 7a3 3 0 0 1 6 0v2H9V7Z\"/></svg>";
}
