using Ebook.Application.Common.Messaging;
using Ebook.Domain.Common;
using Microsoft.Extensions.Options;

namespace Ebook.Application.Administration.Auth;

public sealed class AdminAuthOptions
{
    public const string SectionName = "AdminAuth";

    public string Username { get; set; } = string.Empty;

    /// <summary>Hash PBKDF2 gerado por IPasswordHasher (nunca a senha em claro).</summary>
    public string PasswordHash { get; set; } = string.Empty;
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IJwtService
{
    (string Token, DateTime ExpiresAtUtc) IssueToken(string username);
}

public sealed record LoginCommand(string Username, string Password) : ICommand<LoginResult>;

public sealed record LoginResult(string Token, DateTime ExpiresAtUtc);

public sealed class LoginCommandHandler(
    IOptions<AdminAuthOptions> options,
    IPasswordHasher passwordHasher,
    IJwtService jwtService) : ICommandHandler<LoginCommand, LoginResult>
{
    private static readonly Error InvalidCredentials =
        new("Auth.InvalidCredentials", "Usuário ou senha inválidos.");

    public Task<Result<LoginResult>> HandleAsync(LoginCommand command, CancellationToken ct)
    {
        var admin = options.Value;
        var usernameMatches = string.Equals(command.Username, admin.Username, StringComparison.Ordinal);

        if (!usernameMatches || !passwordHasher.Verify(command.Password, admin.PasswordHash))
        {
            return Task.FromResult(Result.Failure<LoginResult>(InvalidCredentials));
        }

        var (token, expiresAt) = jwtService.IssueToken(admin.Username);
        return Task.FromResult(Result.Success(new LoginResult(token, expiresAt)));
    }
}
