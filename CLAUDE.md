# CLAUDE.md

> Circle Integration POC
>
> Angular + .NET 10 Proof of Concept demonstrating Circle Crypto Deposits, Payouts, Webhooks, Ledgering, Reconciliation, and Funding Account Management.
>
> This repository is optimized for Claude Code, Circle MCP, Circle Skills, and AI-assisted development.

---

# Mission

Build a working end-to-end Proof of Concept that demonstrates how IFS can integrate with Circle to:

1. Accept USDC deposits
2. Credit customer funding accounts
3. Process withdrawals
4. Debit customer funding accounts
5. Handle Circle webhooks
6. Maintain a financial ledger
7. Reconcile Circle transactions with internal records

The objective is learning, validation, and architectural proof.

This is NOT a production system.

Prefer simplicity, readability, and correctness over enterprise complexity.

---

# Business Context

IFS owns a Circle Mint Account.

Customers interact with IFS.

Customers do NOT interact directly with Circle APIs.

IFS remains the system of record.

Circle acts as settlement infrastructure.

---

# Supported Scenarios

## Scenario 1 — Customer Has Circle Account

Customer owns a Circle account.

Flow:

Customer
→ IFS Portal
→ Create Deposit Request
→ Circle Payment Intent
→ Customer Transfers USDC
→ Circle Settlement
→ Circle Webhook
→ IFS Credits Funding Account

---

## Scenario 2 — Customer Uses External Wallet

Examples:

- Binance
- Coinbase
- Kraken
- MetaMask
- Trust Wallet

Flow:

Customer
→ IFS Portal
→ Create Deposit Request
→ Circle Payment Intent
→ Customer Transfers USDC
→ Circle Settlement
→ Circle Webhook
→ IFS Credits Funding Account

---

## Future Scenario (Out of Scope)

Circle-to-Circle internal transfers.

Potential future flow:

Customer Circle Account
→ Circle Internal Transfer
→ IFS Circle Account

Do not implement during POC.

Document only.

---

# POC Goals

The application must demonstrate:

## Deposits

Create Payment Intent.

Display Deposit Address.

Track Status.

Process Settlement.

Credit Funding Account.

---

## Withdrawals

Create Recipient Address.

Create Payout.

Track Status.

Process Settlement.

Debit Funding Account.

---

## Webhooks

Receive Circle events.

Store events.

Prevent duplicate processing.

Update internal records.

---

## Ledger

Create ledger entries.

Calculate balances.

Maintain audit history.

---

## Reconciliation

Compare:

- Circle Deposits
- Circle Payouts
- Internal Transactions
- Internal Ledger

Detect mismatches.

---

# Technology Stack

## Frontend

Angular Latest Stable

Requirements:

- Standalone Components
- Signals
- Signal Stores
- Functional Guards
- Functional Interceptors
- Strict TypeScript
- Angular Control Flow

Use:

```html
@if
@for
@switch
```

Avoid:

- NgModules
- NgRx
- Subject-heavy state management
- Legacy Angular syntax

---

## Backend

.NET 10

Requirements:

- ASP.NET Core Web API
- Minimal APIs where appropriate
- Entity Framework Core
- SQL Server
- OpenAPI
- Swagger

Prefer:

- Feature-based organization
- Vertical Slice architecture

Avoid:

- Generic Repository Pattern
- Over-engineering
- Excessive abstractions

---

## Database

SQL Server

Purpose:

- Customers
- Funding Accounts
- Deposits
- Withdrawals
- Ledger Entries
- Webhook Events
- Reconciliation Records

---

# Architecture

Frontend
↓
.NET 10 API
↓
Circle Service Layer
↓
Circle APIs

---

# Core Principles

## Source Of Truth

Circle is source of truth for:

- Deposit Settlement
- Payout Settlement
- Blockchain Status

IFS is source of truth for:

- Customer Balance
- Funding Account Balance
- Ledger
- Reporting
- Transaction History

---

## Balance Management

Never update balances directly.

Balances must be derived from ledger entries.

Wrong:

```csharp
account.Balance += amount;
```

Correct:

```csharp
CreateLedgerEntry(...);
RecalculateBalance(...);
```

---

## Ledger Is Append Only

Never modify historical entries.

Never delete entries.

Corrections must be reversal entries.

---

## Idempotency

Mandatory for:

- Deposits
- Payouts
- Webhooks
- Reconciliation Jobs

Store:

- Circle Event Id
- Payment Intent Id
- Payout Id

Duplicate processing must be ignored.

---

# Domain Model

## Customer

```csharp
Customer
{
    Id
    Name
    Email
    CustomerType
}
```

CustomerType:

```text
Circle
ExternalWallet
```

---

## FundingAccount

```csharp
FundingAccount
{
    Id
    CustomerId
    Currency
    Balance
}
```

---

## Deposit

```csharp
Deposit
{
    Id
    CustomerId
    FundingAccountId
    CirclePaymentIntentId
    Amount
    Status
}
```

---

## Withdrawal

```csharp
Withdrawal
{
    Id
    CustomerId
    FundingAccountId
    CirclePayoutId
    Amount
    Status
}
```

---

## LedgerEntry

```csharp
LedgerEntry
{
    Id
    FundingAccountId
    EntryType
    Amount
    ReferenceId
}
```

EntryType:

```text
Credit
Debit
```

---

## WebhookEvent

```csharp
WebhookEvent
{
    Id
    EventId
    EventType
    Payload
    Status
}
```

---

# Circle APIs In Scope

## Connectivity

```http
GET /ping
```

```http
GET /v1/configuration
```

```http
GET /v1/stablecoins
```

```http
GET /v1/businessAccount/balances
```

---

## Deposits

```http
POST /v1/paymentIntents
```

```http
GET /v1/paymentIntents/{id}
```

```http
GET /v1/paymentIntents
```

---

## Payouts

```http
POST /v1/businessAccount/wallets/addresses/recipient
```

```http
GET /v1/businessAccount/wallets/addresses/recipient
```

```http
POST /v1/businessAccount/payouts
```

```http
GET /v1/businessAccount/payouts/{id}
```

```http
GET /v1/businessAccount/payouts
```

---

## Webhooks

```http
POST /v1/notifications/subscriptions
```

```http
GET /v1/notifications/subscriptions
```

```http
DELETE /v1/notifications/subscriptions/{id}
```

---

# Required Screens

## Dashboard

Display:

- Circle Balance
- Funding Accounts
- Deposits
- Withdrawals
- Recent Webhooks

---

## Customers

Display:

- Customer Details
- Customer Type
- Funding Accounts

---

## Funding Accounts

Display:

- Current Balance
- Ledger
- Deposits
- Withdrawals

---

## Deposits

Create Deposit.

Display:

- Amount
- Network
- Deposit Address
- Status

---

## Withdrawals

Create Withdrawal.

Display:

- Amount
- Destination Wallet
- Status

---

## Ledger

Display:

- Credits
- Debits
- Running Balance

---

## Webhooks

Display:

- Event Type
- Status
- Processing Result
- Payload

---

## Reconciliation

Display:

- Circle Transactions
- Internal Transactions
- Mismatches

---

# Backend Features

## Deposit Service

Responsible for:

- Create Payment Intent
- Poll Status
- Process Settlement

---

## Payout Service

Responsible for:

- Create Recipient
- Create Payout
- Track Status

---

## Ledger Service

Responsible for:

- Create Credits
- Create Debits
- Calculate Balances

---

## Webhook Service

Responsible for:

- Validate Event
- Prevent Duplicates
- Process Event

---

## Reconciliation Service

Responsible for:

- Compare Circle Records
- Compare Internal Records
- Generate Exceptions

---

# Logging

Every request should include:

```text
CorrelationId
CustomerId
FundingAccountId
```

Log:

- Circle Requests
- Circle Responses
- Webhooks
- Reconciliation Results

---

# Security

Never commit:

- Circle API Keys
- Secrets
- Connection Strings

Use:

```text
appsettings.Development.json
User Secrets
Environment Variables
```

---

# Circle MCP Usage

Prefer Circle MCP before implementing Circle API logic.

Use MCP for:

- API exploration
- Endpoint discovery
- Request examples
- Response examples
- Sandbox testing

Always verify API assumptions using Circle MCP.

---

# Circle Skills Usage

Use Circle Skills when:

- Generating integration code
- Creating Payment Intent requests
- Creating Payout requests
- Creating Webhook handlers
- Understanding Circle workflows

Prefer official Circle Skills guidance over assumptions.

---

# Success Criteria

POC is complete when:

✓ Deposit Flow Works

✓ Withdrawal Flow Works

✓ Funding Accounts Update

✓ Ledger Updates

✓ Webhooks Process Correctly

✓ Reconciliation Runs

✓ Angular UI Demonstrates All Flows

✓ .NET 10 API Integrates With Circle Sandbox

✓ Both Customer Scenarios Are Demonstrated

✓ Circle MCP and Skills Are Successfully Utilized

---

# Golden Rule

Never trust the client.

Never trust screenshots.

Never trust pending blockchain transactions.

Only trust:

- Circle Settlement Status
- Circle Webhooks
- Internal Ledger
