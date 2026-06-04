using FundManagement.Application.Common;

namespace FundManagement.Infrastructure.Circle;

public class CircleClient : ICircleClient
{
    private readonly HttpClient _httpClient;

    public CircleClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/ping", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
