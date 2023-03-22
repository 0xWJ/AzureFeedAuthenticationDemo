using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.WebApi;


namespace AzureFeedAuthenticationDemo
{
    internal class Program
    {
        internal const string VssAuthorizationEndpoint = "X-VSS-AuthorizationEndpoint";

        internal static bool IsValidAzureFeed(Uri uri)
        {
            // TODO: Add more valid hosts here
            return uri.Host.Equals("pkgs.dev.azure.com", StringComparison.OrdinalIgnoreCase);
        }

        internal static async Task<HttpResponseHeaders> GetResponseHeadersAsync(Uri uri, CancellationToken cancellationToken)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri))
                {
                    using (var response = await httpClient.SendAsync(request, cancellationToken))
                        return response.Headers;
                }
            }
        }

        internal static async Task<Uri> GetAuthorizationEndpointAsync(Uri uri, CancellationToken cancellationToken)
        {
            var headers = await GetResponseHeadersAsync(uri, cancellationToken);

            try
            {
                foreach (var endpoint in headers.GetValues(VssAuthorizationEndpoint))
                {
                    if (Uri.TryCreate(endpoint, UriKind.Absolute, out var parsedEndpoint))
                    {
                        return parsedEndpoint;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return null;
        }

        internal static async Task<string> LoadFeedIndexAsync(Uri azureFeedUri, CancellationToken cancellationToken)
        {
            if (!IsValidAzureFeed(azureFeedUri))
            {
                throw new ArgumentException("Invalid Feed Url");
            }
            Uri baseUri = await GetAuthorizationEndpointAsync(azureFeedUri, cancellationToken) ?? throw new ArgumentException($"No authorization endpoint found for {azureFeedUri}");
            //Prompt user for credential
            using (VssConnection connection = new VssConnection(baseUri, new VssClientCredentials()))
            {
                await connection.ConnectAsync(cancellationToken);
                using (HttpClient client = new HttpClient(connection.InnerHandler, disposeHandler: false))
                {
                    var resp = await client.GetAsync(azureFeedUri, cancellationToken);
                    resp.EnsureSuccessStatusCode();
                    return await resp.Content.ReadAsStringAsync();
                }
            }
        }

        static void Main(string[] args)
        {
            string azureFeedUrl = args[0];
            Uri azureFeedUri = new Uri(azureFeedUrl);
            var indexJson = LoadFeedIndexAsync(azureFeedUri, CancellationToken.None).Result;
            Console.WriteLine(indexJson);
        }
    }
}