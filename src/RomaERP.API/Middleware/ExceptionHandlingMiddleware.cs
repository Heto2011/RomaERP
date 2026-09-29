using System.Text.Json;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Infrastructure.EInvoicing.Zatca;

namespace RomaERP.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, message) = ex switch
            {
                ValidationAppException => (StatusCodes.Status400BadRequest, ex.Message),
                NotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "غير مصرح."),
                // A failure reaching ZATCA's own servers (network, or ZATCA returning an error) — not a bug
                // in RomaERP, so its message is safe (and useful) to show the admin directly, unlike a 500.
                ZatcaApiException => (StatusCodes.Status502BadGateway, ex.Message),
                _ => (StatusCodes.Status500InternalServerError, "حدث خطأ غير متوقع.")
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
                _logger.LogError(ex, "Unhandled exception");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
        }
    }
}
