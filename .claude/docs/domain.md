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
// CirclePayoutId = payout UUID from POST /v1/payouts response
// Payout flow: create recipient (POST /v1/addressBook/recipients) → wait active → create payout
```

## LedgerEntry
```csharp
LedgerEntry { Id, FundingAccountId, EntryType, Amount, ReferenceId }
// EntryType: Credit | Debit
// Append-only. Never update or delete. Corrections = reversal entries.
```

## WebhookEvent
```csharp
WebhookEvent { Id, CircleEventId, EventType, Payload, Status }
// CircleEventId = resource UUID used as idempotency key:
//   payouts notification  → payout.id
//   transfers notification → transfer.id
//   unknown types         → "{notificationType}:{clientId}"
// Circle Mint has NO notificationId — idempotency is per resource, not per delivery
// Status: Received → Processed | Failed
```

## Source of Truth

| Concern | Owner |
|---------|-------|
| Deposit/Payout settlement | Circle |
| Blockchain status | Circle |
| Customer balance | FundManagement (LedgerEntries) |
| Funding account balance | FundManagement (LedgerEntries) |
| Ledger / Reporting | FundManagement |
