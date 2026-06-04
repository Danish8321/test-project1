using System.Data;

namespace FundManagement.Application.Common;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
