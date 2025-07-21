using System.Net.Http.Headers;
using Frontend.Client.Services;

namespace Frontend.Client.Services
{
    public class JwtTokenMessageHandler : DelegatingHandler
    {
        private readonly Uri _allowedBaseAddress;
        private readonly JwtAuthenticationStateProvider _authStateProvider;

        public JwtTokenMessageHandler(Uri allowedBaseAddress, JwtAuthenticationStateProvider authStateProvider)
        {
            _allowedBaseAddress = allowedBaseAddress;
            _authStateProvider = authStateProvider;
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return SendAsync(request, cancellationToken).Result;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri;
            var isSelfApiAccess = uri != null && _allowedBaseAddress.IsBaseOf(uri);

            if (isSelfApiAccess)
            {
                var token = _authStateProvider.Token ?? string.Empty;
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
