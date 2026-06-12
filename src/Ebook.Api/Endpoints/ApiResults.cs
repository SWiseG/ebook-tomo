using Ebook.Domain.Common;

namespace Ebook.Api.Endpoints;

public static class ApiResults
{
    /// <summary>Mapeia Result → HTTP: sucesso 200, erro como ProblemDetails com status pelo código.</summary>
    public static IResult ToHttp<T>(this Result<T> result) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(title: result.Error.Code, detail: result.Error.Message, statusCode: StatusFor(result.Error.Code));

    private static int StatusFor(string errorCode) => errorCode switch
    {
        "Auth.InvalidCredentials" => StatusCodes.Status401Unauthorized,
        _ when errorCode.EndsWith(".NotFound", StringComparison.Ordinal) => StatusCodes.Status404NotFound,
        "Ai.BudgetExceeded" or "Ai.WindowExhausted" => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status400BadRequest
    };
}
