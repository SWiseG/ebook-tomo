using Ebook.Domain.Common;

namespace Ebook.Domain.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_com_valor_expoe_value()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_lanca_ao_acessar_value()
    {
        var result = Result.Failure<int>(new Error("X.Err", "falhou"));

        Assert.True(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_exige_erro()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(Error.None));
    }
}
