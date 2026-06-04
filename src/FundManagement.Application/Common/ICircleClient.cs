namespace FundManagement.Application.Common;

public interface ICircleClient
{
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
}
