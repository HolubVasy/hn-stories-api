namespace HackerNewsApi.Application.Exceptions;

public sealed class InvalidRequestParameterException
    : HackerNewsApiException
{
    public InvalidRequestParameterException(
        string paramName, string detail)
        : base(
            $"Parameter '{paramName}' is invalid. {detail}",
            statusCode: 400)
    { }
}
