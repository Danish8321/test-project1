namespace FundManagement.Domain.Enums;

public enum DepositStatus
{
    Pending,
    PendingCustomerTransfer,
    PaymentDetected,
    Completed,
    Failed,
    Cancelled
}
