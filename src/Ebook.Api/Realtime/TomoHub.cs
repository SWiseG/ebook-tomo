using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ebook.Api.Realtime;

/// <summary>
/// Hub de tempo real do painel. Apenas servidor → cliente (push): o servidor
/// invoca "JobChanged"/"ProductChanged" nos clientes; não há métodos de cliente
/// para servidor. Exige JWT (o token chega via query string "access_token",
/// configurado no JwtBearerEvents do Program.cs).
/// </summary>
[Authorize]
public sealed class TomoHub : Hub;
