using RomaERP.Application.Common.Interfaces;

namespace RomaERP.API.Services;

public class HttpUserLanguage : IUserLanguage
{
    private readonly IHttpContextAccessor _http;

    public HttpUserLanguage(IHttpContextAccessor http) => _http = http;

    public bool PrefersArabic
    {
        get
        {
            var header = _http.HttpContext?.Request.Headers.AcceptLanguage.ToString();
            return !string.IsNullOrWhiteSpace(header) && header.TrimStart().StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        }
    }
}
