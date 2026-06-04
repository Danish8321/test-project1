# Domain Model

## Customer
```csharp
Customer { Id, Name, Email, CustomerType }
// CustomerType: Circle | ExternalWallet
```

## FundingAccount
```csharp
FundingAccount { Id, CustomerId, Currency, Balance }
// Balance is always derived from LedgerEntries — never set directly
```

## Deposit
```csharp
Deposit { Id, CustomerId, FundingAccountId, CirclePaymentIntentId, Amount, Status }
```

## Withdrawal
```csharp
Withdrawal { Id, CustomerId, FundingAccountId, CirclePayoutId, Amount, Status }
```

## LedgerEntry
```csharp
LedgerEntry { Id, FundingAccountId, EntryType, Amount, ReferenceId }
// EntryType: Credit | Debit
// Append-only. Never update or delete. Corrections = reversal entries.
```

## WebhookEvent
```csharp
WebhookEvent { Id, EventId, EventType, Payload, Status }
// EventId is Circle's id — used for idempotency check before processing
```

## Source of Truth

| Concern | Owner |
|---------|-------|
| Deposit/Payout settlement | Circle |
| Blockchain status | Circle |
| Customer balance | FundManagement (LedgerEntries) |
| Funding account balance | FundManagement (LedgerEntries) |
| Ledger / Reporting | FundManagement |
