namespace FundManagement.Application.Reconciliation;

public interface IReconciliationService
{
    Task<ReconciliationResult> RunAsync();
}
