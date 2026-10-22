using System.Net;

namespace CaptureLab.Alfresco;

/// <summary>
/// Reprise automatique avec attente exponentielle (1 s, 2 s, 4 s…) sur les erreurs transitoires :
/// serveur qui démarre (502/503), surcharge (429), délai dépassé, coupure réseau.
/// Les erreurs "métier" (400, 401, 403, 404, 409) ne sont PAS rejouées : rejouer ne changerait rien.
/// </summary>
public sealed class RetryHandler : DelegatingHandler
{
    private readonly int _maxRetries;

    public RetryHandler(int maxRetries, HttpMessageHandler inner) : base(inner) => _maxRetries = maxRetries;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var response = await base.SendAsync(request, ct);
                if (!IsTransient(response.StatusCode) || attempt >= _maxRetries)
                    return response;
                response.Dispose();
            }
            catch (HttpRequestException) when (attempt < _maxRetries) { }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < _maxRetries) { } // timeout

            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
        }
    }

    private static bool IsTransient(HttpStatusCode code) =>
        code is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
             or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
}
