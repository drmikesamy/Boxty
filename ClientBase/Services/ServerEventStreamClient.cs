using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Boxty.ClientBase.Services
{
    public interface IServerEventStreamClient
    {
        Task StreamAsync<TEvent>(string relativeUrl, Func<TEvent, Task> onEvent, CancellationToken cancellationToken = default);
    }

    public class ServerEventStreamClient : IServerEventStreamClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ServerEventStreamClient(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task StreamAsync<TEvent>(string relativeUrl, Func<TEvent, Task> onEvent, CancellationToken cancellationToken = default)
        {
            var httpClient = _httpClientFactory.CreateClient("api");
            using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            var dataBuilder = new StringBuilder();

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    if (dataBuilder.Length == 0)
                    {
                        continue;
                    }

                    var payload = dataBuilder.ToString();
                    dataBuilder.Clear();

                    var @event = JsonSerializer.Deserialize<TEvent>(payload, _jsonOptions);
                    if (@event != null)
                    {
                        await onEvent(@event);
                    }

                    continue;
                }

                if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var data = line[5..].TrimStart();
                    if (dataBuilder.Length > 0)
                    {
                        dataBuilder.Append('\n');
                    }

                    dataBuilder.Append(data);
                }
            }
        }
    }
}
