using CnjProcessosApi.Core.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CnjProcessosApi.Middleware;

public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            ArgumentException => 400,
            DataJudException e => e.StatusCode,
            _ => 500
        };
        if (status == 500) logger.LogError(exception, "Erro interno na API");
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = status == 400 ? "Consulta inválida" : "Não foi possível concluir a consulta",
            Detail = status == 500 ? "Ocorreu um erro interno. Tente novamente mais tarde." : exception.Message
        }, cancellationToken);
        return true;
    }
}
