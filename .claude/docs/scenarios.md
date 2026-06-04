# Business Scenarios

FundManagement owns Circle Mint Account. Customers interact with FundManagement only — never directly with Circle.

## Scenario 1 — Customer Has Circle Account
```
Customer → FundManagement Portal → Create Deposit Request
→ Circle Payment Intent → Customer Transfers USDC
→ Circle Settlement → Circle Webhook → FundManagement Credits Funding Account
```

## Scenario 2 — Customer Uses External Wallet
(Binance, Coinbase, Kraken, MetaMask, Trust Wallet)
```
Customer → FundManagement Portal → Create Deposit Request
→ Circle Payment Intent → Customer Transfers USDC
→ Circle Settlement → Circle Webhook → FundManagement Credits Funding Account
```

## Out of Scope (Document Only)
Circle-to-Circle internal transfers. Skip during POC.

## Required Screens

| Screen | Key Actions |
|--------|-------------|
| Dashboard | Circle balance, funding accounts, recent deposits/withdrawals/webhooks |
| Customers | Customer details, type, funding accounts |
| Funding Accounts | Balance, ledger, deposits, withdrawals |
| Deposits | Create deposit, show amount/network/address/status |
| Withdrawals | Create withdrawal, show amount/destination/status |
| Ledger | Credits, debits, running balance |
| Webhooks | Event type, status, processing result, payload |
| Reconciliation | Circle vs internal transactions, mismatches |