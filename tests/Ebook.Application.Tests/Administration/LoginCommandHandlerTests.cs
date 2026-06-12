using Ebook.Application.Administration.Auth;
using Microsoft.Extensions.Options;

namespace Ebook.Application.Tests.Administration;

public class LoginCommandHandlerTests
{
    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string hash) => hash == "hash:" + password;
    }

    private sealed class FakeJwt : IJwtService
    {
        public (string Token, DateTime ExpiresAtUtc) IssueToken(string username) =>
            ("token-" + username, new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc));
    }

    private static LoginCommandHandler BuildHandler() => new(
        Options.Create(new AdminAuthOptions { Username = "rafael", PasswordHash = "hash:s3nh4" }),
        new FakeHasher(),
        new FakeJwt());

    [Fact]
    public async Task Credenciais_corretas_emitem_token()
    {
        var result = await BuildHandler().HandleAsync(new LoginCommand("rafael", "s3nh4"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("token-rafael", result.Value.Token);
    }

    [Theory]
    [InlineData("rafael", "errada")]
    [InlineData("outro", "s3nh4")]
    public async Task Credenciais_invalidas_falham_sem_distinguir_usuario_de_senha(string user, string password)
    {
        var result = await BuildHandler().HandleAsync(new LoginCommand(user, password), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Auth.InvalidCredentials", result.Error.Code);
    }
}
