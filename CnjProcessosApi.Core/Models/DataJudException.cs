namespace CnjProcessosApi.Core.Models;

public class DataJudException(string message, int statusCode = 502) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
