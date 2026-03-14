using Loxifi.CurlImpersonate;

namespace FinaryExport.Infrastructure;

// HttpMessageHandler that delegates to CurlClient for TLS fingerprint impersonation.
// Enables using CurlImpersonate with HttpClientFactory and DelegatingHandler chains.
sealed class CurlMessageHandler(CurlClient curlClient) : HttpMessageHandler
{
	protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => curlClient.SendAsync(request, cancellationToken);

    // CurlClient lifecycle is managed externally (DI singleton); don't dispose it here
    protected override void Dispose(bool disposing) => base.Dispose(disposing);
}
