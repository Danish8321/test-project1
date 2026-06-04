using System.Data;
using Dapper;
using FundManagement.Domain.Enums;

namespace FundManagement.Infrastructure.Data;

public static class DapperConfig
{
    public static void Configure()
    {
        SqlMapper.AddTypeHandler(new EnumTypeHandler<CustomerType>());
        SqlMapper.AddTypeHandler(new EnumTypeHandler<DepositStatus>());
        SqlMapper.AddTypeHandler(new EnumTypeHandler<WithdrawalStatus>());
        SqlMapper.AddTypeHandler(new EnumTypeHandler<EntryType>());
        SqlMapper.AddTypeHandler(new EnumTypeHandler<WebhookStatus>());
    }
}

file class EnumTypeHandler<T> : SqlMapper.TypeHandler<T> where T : struct, Enum
{
    public override T Parse(object value) => Enum.Parse<T>((string)value);
    public override void SetValue(IDbDataParameter parameter, T value) => parameter.Value = value.ToString();
}
