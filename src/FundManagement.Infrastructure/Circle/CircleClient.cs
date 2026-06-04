using FundManagement.Application.Common;
using Microsoft.Extensions.Logging;

namespace FundManagement.Infrastructure.Circle;

public class CircleClient : ICircleClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CircleClient> _logger;

    public CircleClient(HttpClient httpClient, ILogger<CircleClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/ping", cancellationToken);
            _logger.LogInformation("Circle ping responded with {StatusCode}", response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Circle ping failed — HTTP error");
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Circle ping cancelled");
            return false;
        }
    }
}
