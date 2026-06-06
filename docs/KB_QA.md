# Circle USDC Integration — QA & Testing Guide

| Field | Value |
|---|---|
| Version | 2.0 |
| Date | 2026-06-06 |
| Audience | QA Engineers · Test Leads · Release Managers |
| Related Files | [Index](KB_INDEX.md) · [Management](KB_MANAGEMENT.md) · [Technical](KB_TECHNICAL.md) · [Operations](KB_OPERATIONS.md) |

---

## Table of Contents

1. [Test Environment Setup](#1-test-environment-setup)
2. [Testnet USDC Faucet](#2-testnet-usdc-faucet)
3. [Webhook Simulation Setup](#3-webhook-simulation-setup)
4. [Test Data Conventions](#4-test-data-conventions)
5. [Test Scenario Matrix — Group A: Transient Intent Deposits](#5-group-a--transient-intent-deposits)
6. [Test Scenario Matrix — Group B: Continuous Intent Deposits](#6-group-b--continuous-intent-deposits)
7. [Test Scenario Matrix — Group C: Scenario 1 (Circle Mint Client)](#7-group-c--scenario-1-circle-mint-client-deposits)
8. [Test Scenario Matrix — Group D: Scenario 2 (External Wallet / Exchange)](#8-group-d--scenario-2-external-walletexchange-deposits)
9. [Test Scenario Matrix — Group E: Withdrawals & Payouts](#9-group-e--withdrawals--payouts)
10. [Test Scenario Matrix — Group F: Mastercard Settlement](#10-group-f--mastercard-settlement)
11. [Test Scenario Matrix — Group G: Security & Idempotency](#11-group-g--security--idempotency)
12. [Test Scenario Matrix — Group H: Reconciliation](#12-group-h--reconciliation)
13. [Test Scenario Matrix — Group I: Performance](#13-group-i--performance)
14. [Simulation Payloads — Ready to Use](#14-simulation-payloads--ready-to-use)
15. [Acceptance Criteria for Go-Live](#15-acceptance-criteria-for-go-live)

---

## 1. Test Environment Setup

### Prerequisites

| Item | Value |
|---|---|
| Circle sandbox account | https://console.circle.com |
| Sandbox API key | `SAND_API_KEY_XXXX` |
| Local API running | `dotnet run --project api/src/FundManagement.Api` (port 5000) |
| Public webhook endpoint | ngrok (local) or staging server (pipeline) |
| DB | PostgreSQL running locally (`docker-compose up db`) |
| Test framework | xUnit + Playwright (E2E) |

### Start Local Environment

```bash
# Start database
docker run -d --name ifs-pg \
  -e POSTGRES_PASSWORD=localdev \
  -e POSTGRES_DB=ifs_poc \
  -p 5432:5432 postgres:16

# Start API (runs DbUp migrations on startup)
cd api && dotnet run --project src/FundManagement.Api

# Start ngrok for webhooks
ngrok http 5000
# → Copy forwarding URL, e.g. https://abc123.ngrok.io

# Register ngrok URL as Circle webhook endpoint
curl -X POST https://api-sandbox.circle.com/v1/notifications/subscriptions \
  -H "Authorization: Bearer SAND_API_KEY_XXXX" \
  -H "Content-Type: application/json" \
  -d '{"endpoint": "https://abc123.ngrok.io/api/webhooks/circle"}'

# Verify connectivity
curl https://api-sandbox.circle.com/ping
curl https://api-sandbox.circle.com/v1/businessAccount/balances \
  -H "Authorization: Bearer SAND_API_KEY_XXXX"
```

### Verify Signature Validation Toggle

For webhook simulation tests, you may need to bypass ECDSA validation in sandbox:

```json
// appsettings.Development.json
{
  "Circle": {
    "BypassWebhookSignatureValidation": true
  }
}
```

> **NEVER enable in staging or production.** Development only.

---

## 2. Testnet USDC Faucet

For Scenario 2 (external blockchain) real end-to-end tests:

```bash
# Circle USDC faucet — get testnet USDC on supported chains
# URL: https://faucet.circle.com
# Chains available: ETH-Sepolia, Base-Sepolia, Polygon-Amoy, SOL-Devnet, ARB-Sepolia, etc.

# After getting testnet USDC, send to your payment intent deposit address
# MetaMask (Sepolia / Base Sepolia) → paste deposit address → send USDC → confirm
```

For most webhook tests, you do NOT need real testnet USDC — use the simulation payloads in section 14.

---

## 3. Webhook Simulation Setup

All webhook tests POST directly to your local endpoint with a crafted payload. In development, ECDSA validation bypass must be enabled (see section 1).

```bash
BASE_URL="https://abc123.ngrok.io"  # or http://localhost:5000

# Generic webhook POST function
post_webhook() {
  curl -s -o /dev/null -w "%{http_code}" \
    -X POST "$BASE_URL/api/webhooks/circle" \
    -H "Content-Type: application/json" \
    -H "X-Circle-Signature: test-sig" \
    -H "X-Circle-Key-Id: test-key-id" \
    -d "$1"
}
```

---

## 4. Test Data Conventions

| Item | Convention |
|---|---|
| Test customer IDs | Use fixed UUIDs per test class — never auto-generate in tests |
| Transfer IDs | `test-transfer-{group}-{number}` e.g. `test-transfer-a1-001` |
| Payout IDs | `test-payout-{group}-{number}` |
| Payment Intent IDs | Always create via API — use returned `id` |
| Amounts | Use round numbers: `100.00`, `250.00`, `500.00` |
| Cleanup | Each test group cleans its own data in `[Fact]` teardown |
| DB assertions | Always query via `funding_account_balances` view — never raw column |

---

## 5. Group A — Transient Intent Deposits

**Scope:** `paymentIntentType = "transient"` — single use, 24h expiry.

| # | Test Name | Steps | Expected Result | DB Assertion |
|---|---|---|---|---|
| A1 | Transient — happy path ETH | Create transient intent (ETH) → POST transfer webhook (complete) | Deposit=Settled; ledger Credit created | `SELECT status FROM deposits = 'Settled'` |
| A2 | Transient — happy path Base | Same on BASE chain | Same result; chain=BASE recorded | `SELECT chain FROM deposits = 'BASE'` |
| A3 | Transient — happy path SOL | Same on SOL chain | Same result | `SELECT chain FROM deposits = 'SOL'` |
| A4 | Transient — correct amount credited | Create intent $500 → simulate $500 transfer webhook | Ledger Credit = 500.00 | `SELECT amount FROM ledger_entries = 500.00` |
| A5 | Transient — expiry: no payment | Create intent → POST `status=expired` or wait 24h in sandbox | Deposit=Expired; no ledger entry | `SELECT status FROM deposits = 'Expired'` |
| A6 | Transient — second payment rejected | Create intent → POST complete webhook → POST second transfer webhook for same intent | Second payment ignored (intent closed after first) | Exactly 1 Credit entry |
| A7 | Transient — status lifecycle | Create intent → poll `GET /v1/paymentIntents/{id}` | Status: created → pending → complete | Matches internal deposit status |
| A8 | Transient — intent not found | POST webhook with unknown `paymentIntentId` | Logged; webhook=Received; no crash; return 200 | `webhook_events.status = 'Received'` |
| A9 | Transient — multi-chain intent | Create intent with ETH + BASE paymentMethods | Two deposit addresses returned; both functional | `payment_intents.deposit_address` per chain |
| A10 | Transient — balance correct after deposit | Fund via transient → query balance | Balance = deposited amount | `SELECT balance FROM funding_account_balances` |

---

## 6. Group B — Continuous Intent Deposits

**Scope:** `paymentIntentType = "continuous"` — permanent address, unlimited payments.

| # | Test Name | Steps | Expected Result | DB Assertion |
|---|---|---|---|---|
| B1 | Continuous — happy path first payment | Create continuous intent → POST transfer webhook #1 | Deposit #1 created=Settled; ledger Credit #1 | 1 deposit row; 1 ledger Credit |
| B2 | Continuous — second payment same intent | After B1 → POST transfer webhook #2 (different transfer.id, same paymentIntentId) | Deposit #2 created=Settled; ledger Credit #2 | 2 deposit rows same intent; 2 Credits |
| B3 | Continuous — five payments accumulate | POST 5 transfer webhooks for same intent | Balance = sum of 5 amounts | `SELECT balance` = sum; 5 Credit entries |
| B4 | Continuous — intent stays `open` after payment | After any payment, GET intent from Circle | Intent status = `open` (not `complete`) | `payment_intents.status = 'open'` |
| B5 | Continuous — different amounts per transfer | Post $100, $250, $500 on same intent | Three separate ledger Credits: 100, 250, 500 | Ledger shows all three distinct amounts |
| B6 | Continuous — close intent (expire) | Create intent → POST `POST /api/payment-intents/{id}/expire` | Intent status = `closed`; no more payments accepted | `payment_intents.status = 'closed'` |
| B7 | Continuous — payment after close rejected | Close intent → POST new transfer webhook for that intent | Webhook stored; deposit NOT settled; alert raised | `webhook_events.status = 'Received'` only |
| B8 | Continuous — one intent per client per chain | Create continuous intent for customer + chain → create second for same customer + chain | Second creation reuses existing intent (idempotent) | Only 1 continuous intent per customer+chain |
| B9 | Continuous — idempotency per transfer | POST same transfer webhook twice (same transfer.id) | Second delivery rejected; exactly 1 Credit | `SELECT COUNT(*) WHERE reference_id = ... = 1` |
| B10 | Continuous — no amount field required | Create continuous intent (no `amount` field) → verify | Intent created successfully; amount=null | API returns 201; intent active |
| B11 | Continuous — balance correct across multiple payments | Fund 3× via continuous intent → query balance | Balance = sum of 3 payments | `funding_account_balances.balance` |
| B12 | Continuous — transfer.id unique per payment | Verify each incoming transfer has unique `circle_transfer_id` in deposits | No duplicate `circle_transfer_id` | `SELECT DISTINCT circle_transfer_id FROM deposits` |

---

## 7. Group C — Scenario 1: Circle Mint Client Deposits

**Scope:** `transfer.source.type = "wallet"` — client sends from their Circle Mint account.

| # | Test Name | Steps | Expected Result | DB Assertion |
|---|---|---|---|---|
| C1 | Sc1 — happy path transient | POST webhook: `source.type="wallet"` + transient intent | Deposit=Settled; `transfer_source_type="wallet"` stored | `SELECT transfer_source_type = 'wallet'` |
| C2 | Sc1 — happy path continuous | POST webhook: `source.type="wallet"` + continuous intent | Deposit=Settled; `transfer_source_type="wallet"` | Same |
| C3 | Sc1 — source wallet ID recorded | POST webhook with `source.id = "client-wallet-uuid"` | `transfer_source_wallet_id = "client-wallet-uuid"` stored | `SELECT transfer_source_wallet_id FROM deposits` |
| C4 | Sc1 — no transactionHash required | POST webhook with `source.type="wallet"` and NO `transactionHash` | Processed successfully; `transaction_hash = NULL` | `SELECT transaction_hash IS NULL FROM deposits` |
| C5 | Sc1 — no AML screening triggered | POST wallet-type webhook | No compliance screening called for wallet source | Verify `_complianceService.ScreenAddressAsync` NOT called |
| C6 | Sc1 — near-instant (no confirmation wait) | POST wallet webhook; measure processing time | Settled in < 1 second | Timestamp delta < 1s |
| C7 | Sc1 — status=failed | POST webhook: `source.type="wallet"`, `status="failed"` | Deposit=Failed; no ledger Credit | `SELECT status FROM deposits = 'Failed'` |
| C8 | Sc1 — customer type validation | Create CircleMint customer → make Sc1 deposit | customer_type = 'CircleMint' in DB | `SELECT customer_type FROM customers` |

---

## 8. Group D — Scenario 2: External Wallet / Exchange Deposits

**Scope:** `transfer.source.type = "blockchain"` — MetaMask, Coinbase, Binance, Kraken, etc.

| # | Test Name | Steps | Expected Result | DB Assertion |
|---|---|---|---|---|
| D1 | Sc2 — happy path ETH (MetaMask) | POST webhook: `source.type="blockchain"`, `chain="ETH"` | Deposit=Settled; `transfer_source_type="blockchain"` | `SELECT transfer_source_type = 'blockchain'` |
| D2 | Sc2 — happy path Base (Coinbase) | POST webhook: `source.type="blockchain"`, `chain="BASE"` | Same; chain=BASE | `SELECT chain FROM deposits = 'BASE'` |
| D3 | Sc2 — happy path SOL (Phantom) | POST webhook: `source.type="blockchain"`, `chain="SOL"` | Same; chain=SOL | `SELECT chain FROM deposits = 'SOL'` |
| D4 | Sc2 — source address recorded | POST webhook with `source.address="0xabc123..."` | `transfer_source_address = "0xabc123..."` stored | `SELECT transfer_source_address FROM deposits` |
| D5 | Sc2 — transaction hash recorded | POST webhook with `transactionHash="0xdef456..."` | `transaction_hash = "0xdef456..."` stored | `SELECT transaction_hash FROM deposits` |
| D6 | Sc2 — AML screening triggered | POST blockchain webhook | `_complianceService.ScreenAddressAsync` called with source address | Verify method called with correct address |
| D7 | Sc2 — OFAC sanctioned address | POST webhook with sanctioned `source.address` | Deposit flagged for compliance review; NOT auto-credited | `deposits.status = 'ComplianceHold'` |
| D8 | Sc2 — status=failed | POST webhook: `source.type="blockchain"`, `status="failed"` | Deposit=Failed; no Credit | `SELECT status FROM deposits = 'Failed'` |
| D9 | Sc2 — missing paymentIntentId | POST webhook without `paymentIntentId` field | Stored in webhook_events (Received); deposit NOT settled; alert raised | `webhook_events.status = 'Received'`; monitoring alert fired |
| D10 | Sc2 — Coinbase processing delay simulation | Create intent → wait 35 min → POST webhook (payment arrives late) | Deposit settles correctly after late webhook | `deposits.status = 'Settled'` |
| D11 | Sc2 — real testnet USDC (Base Sepolia) | Get faucet USDC → send to payment intent deposit address | Real webhook received; deposit settled | `deposits.status = 'Settled'` |
| D12 | Sc2 — no Circle wallet ID in source | POST webhook: `source.type="blockchain"` (no `source.id`) | Processed; `transfer_source_wallet_id = NULL` | `SELECT transfer_source_wallet_id IS NULL` |

---

## 9. Group E — Withdrawals & Payouts

| # | Test Name | Steps | Expected Result | DB Assertion |
|---|---|---|---|---|
| E1 | Withdrawal — happy path | Fund account → POST /api/withdrawals → recipient active → payout complete webhook | Withdrawal=Complete; Debit ledger entry | `SELECT status FROM withdrawals = 'Complete'` |
| E2 | Withdrawal — balance deducted on payout submission | Create withdrawal → submit payout | Balance reduced immediately on debit ledger entry creation | `SELECT balance FROM funding_account_balances` reduced |
| E3 | Withdrawal — reversal on payout failure | Submit payout → POST failed webhook | Reversal Credit entry created; balance restored | Two ledger entries: Debit + Credit (reversal) |
| E4 | Withdrawal — insufficient funds | Attempt withdrawal > available balance | 422 Unprocessable Entity | `withdrawals` NOT created |
| E5 | Withdrawal — recipient pending → active | Create recipient → verify status=pending → webhook activates it | Payout submitted only after `status=active` | `addressBookRecipient.status = 'active'` |
| E6 | Withdrawal — recipient polling timeout (>30s) | Create recipient that never becomes active | Timeout error raised; withdrawal marked Failed | Error logged; no payout submitted |
| E7 | Withdrawal — payout error: insufficient_funds | POST failed payout webhook with errorCode=insufficient_funds | Alert raised; reversal Credit; withdrawal=Failed | Alert fired; balance restored |
| E8 | Withdrawal — payout error: transaction_denied | Same with `errorCode=transaction_denied` | Same; compliance review flag | Compliance flag on withdrawal |
| E9 | Withdrawal — duplicate payout webhook | POST payout complete webhook twice | 200 both times; exactly 1 Debit entry | `COUNT(ledger_entries WHERE reference_id=payout.id) = 1` |
| E10 | Withdrawal — idempotent payout creation | Retry `POST /v1/payouts` with same idempotencyKey | Circle returns same payout; no duplicate | Single payout in Circle; single withdrawal in DB |

---

## 10. Group F — Mastercard Settlement

| # | Test Name | Steps | Expected Result | DB Assertion |
|---|---|---|---|---|
| F1 | Settlement — happy path | Run settlement job with test amount → payout complete webhook | Withdrawal (is_settlement=true)=Complete; Debit entry | `SELECT is_settlement=true AND status='Complete'` |
| F2 | Settlement — idempotent: run twice same date | Run job → run again same date | Second run skips (idempotency key exists) | Exactly 1 settlement withdrawal per date |
| F3 | Settlement — insufficient Circle balance | Set Circle balance < settlement amount | Job throws InsufficientBalanceException; no payout submitted | No `circle_payout_id` on withdrawal |
| F4 | Settlement — Mastercard recipient reuse | Run settlement on day 1 → run day 2 | Same recipient ID used; no new recipient creation | Same recipient in both withdrawals |
| F5 | Settlement — wrong settlement chain | Configure `SettlementChain=INVALIDCHAIN` | Config validation error at startup | Application fails to start with clear error |
| F6 | Settlement — reconciliation after settlement | Run settlement → run reconciliation | All amounts match; no mismatch alert | Reconciliation result = Pass |
| F7 | Settlement — failed payout reversal | Run settlement → POST failed payout webhook | Alert raised; settlement marked Failed; Debit reversed | Balance restored; ops team notified |

---

## 11. Group G — Security & Idempotency

| # | Test Name | Steps | Expected Result |
|---|---|---|---|
| G1 | Invalid ECDSA signature | POST webhook with corrupted `X-Circle-Signature` | 401 Unauthorized; nothing processed |
| G2 | Missing signature header | POST webhook with no `X-Circle-Signature` | 401 Unauthorized |
| G3 | Missing key ID header | POST webhook with no `X-Circle-Key-Id` | 401 Unauthorized |
| G4 | Signature validation — whitespace matters | POST webhook with re-serialized body (whitespace stripped) | 401 — body must be raw string |
| G5 | IP outside Circle allowlist | Send valid webhook from non-Circle IP | 403 at WAF; never reaches application |
| G6 | Duplicate transfer webhook (same transfer.id) | POST same transfer webhook twice simultaneously | Exactly 1 Credit; second returns 200 but is no-op |
| G7 | 100× replay storm (same transfer.id) | POST 100 identical webhooks | Exactly 1 Credit; 99 idempotency rejections; no DB errors |
| G8 | Concurrent different webhooks (50 parallel) | POST 50 unique transfer webhooks simultaneously | All 50 processed; 50 Credits; no deadlocks |
| G9 | Webhook with unknown notificationType | POST webhook with `notificationType="unknown_type"` | 200 returned; stored in webhook_events; no crash |
| G10 | API key in logs | Check all log outputs | No `SAND_API_KEY_` or `LIVE_API_KEY_` in any log line |
| G11 | API key in error response | Trigger Circle API error | Error message does NOT contain API key |
| G12 | SQL injection attempt via webhook payload | POST webhook with SQL in payload fields | Parameterized query; no injection; 200 returned |
| G13 | Oversized webhook payload | POST webhook with 10MB body | 413 Payload Too Large; no processing |

---

## 12. Group H — Reconciliation

| # | Test Name | Steps | Expected Result |
|---|---|---|---|
| H1 | Clean reconciliation — no mismatches | Fund 3 accounts normally → run reconciliation | Result = Pass; zero mismatches |
| H2 | Missing ledger Credit | Settle deposit in Circle side (mock); skip ledger entry creation | Reconciliation detects missing Credit; alert raised |
| H3 | Missing ledger Debit | Complete payout in Circle; skip Debit creation | Reconciliation detects missing Debit; alert raised |
| H4 | Amount mismatch | Credit $100 when Circle shows $101 transfer | Mismatch detected; amount delta logged |
| H5 | Reconciliation scheduling | Configure 4-hour schedule | Reconciliation runs at correct intervals |
| H6 | Stale pending deposits | Create deposit intent → no webhook for 30 min | Polling job finds stale deposit; polls Circle; settles if complete |
| H7 | Expired intent cleanup | Create transient intent → no payment → expiry | Deposit marked Expired; reconciliation shows consistent |
| H8 | Continuous intent multi-payment reconciliation | Fund via continuous intent 5× → reconcile | All 5 deposits matched; all 5 Credits verified |

---

## 13. Group I — Performance

| # | Test Name | Target | Pass Criteria |
|---|---|---|---|
| I1 | Single webhook processing latency | P95 < 200ms | Webhook → ledger credit in under 200ms |
| I2 | 50 concurrent distinct webhooks | No errors | All 50 processed; 0 failures; no deadlocks |
| I3 | 50 concurrent Scenario 1 (wallet) webhooks | No errors | All 50 Credits created correctly |
| I4 | 50 concurrent Scenario 2 (blockchain) webhooks | No errors | All 50 Credits created correctly |
| I5 | 100× idempotency storm | Exactly 1 Credit | No DB errors; no unique constraint violation crashes |
| I6 | Sustained load: 1000 webhooks over 10 min | No errors | All 1000 processed; 0 dropped; memory stable |
| I7 | Continuous intent: 100 payments on single intent | All credited | 100 deposit rows; 100 Credit entries; balance = sum |
| I8 | API response time: GET /balance | P99 < 50ms | Balance query from ledger view < 50ms |
| I9 | Settlement job duration | < 30 seconds | Job completes within 30s for any realistic settlement amount |

---

## 14. Simulation Payloads — Ready to Use

### Transient Intent — Transfer Complete (Scenario 1: wallet)

```bash
curl -X POST http://localhost:5000/api/webhooks/circle \
  -H "Content-Type: application/json" \
  -H "X-Circle-Signature: test-bypass" \
  -H "X-Circle-Key-Id: test-key-id" \
  -d '{
    "clientId": "c60d2d5b-203c-45bb-9f6e-93641d40a599",
    "notificationType": "transfers",
    "transfer": {
      "id": "test-transfer-c1-001",
      "source": { "type": "wallet", "id": "client-circle-wallet-uuid-001" },
      "destination": { "type": "wallet", "id": "your-wallet-id" },
      "amount": { "amount": "100.00", "currency": "USD" },
      "status": "complete",
      "paymentIntentId": "<REPLACE_WITH_REAL_PAYMENT_INTENT_ID>"
    }
  }'
# Expected: 200 OK; transfer_source_type="wallet"; Credit=100.00
```

### Transient Intent — Transfer Complete (Scenario 2: blockchain / MetaMask ETH)

```bash
curl -X POST http://localhost:5000/api/webhooks/circle \
  -H "Content-Type: application/json" \
  -H "X-Circle-Signature: test-bypass" \
  -H "X-Circle-Key-Id: test-key-id" \
  -d '{
    "clientId": "c60d2d5b-203c-45bb-9f6e-93641d40a599",
    "notificationType": "transfers",
    "transfer": {
      "id": "test-transfer-d1-001",
      "source": { "type": "blockchain", "chain": "ETH", "address": "0xabc1234567890abcdef1234567890abcdef123456" },
      "destination": { "type": "wallet", "id": "your-wallet-id" },
      "amount": { "amount": "250.00", "currency": "USD" },
      "status": "complete",
      "transactionHash": "0xfake123abc456def789",
      "paymentIntentId": "<REPLACE_WITH_REAL_PAYMENT_INTENT_ID>"
    }
  }'
# Expected: 200 OK; transfer_source_type="blockchain"; transaction_hash stored; Credit=250.00
```

### Continuous Intent — First Payment (wallet)

```bash
curl -X POST http://localhost:5000/api/webhooks/circle \
  -H "Content-Type: application/json" \
  -H "X-Circle-Signature: test-bypass" \
  -H "X-Circle-Key-Id: test-key-id" \
  -d '{
    "clientId": "c60d2d5b-203c-45bb-9f6e-93641d40a599",
    "notificationType": "transfers",
    "transfer": {
      "id": "test-transfer-b1-001",
      "source": { "type": "wallet", "id": "client-circle-wallet-uuid-001" },
      "destination": { "type": "wallet", "id": "your-wallet-id" },
      "amount": { "amount": "100.00", "currency": "USD" },
      "status": "complete",
      "paymentIntentId": "<REPLACE_WITH_CONTINUOUS_INTENT_ID>"
    }
  }'
```

### Continuous Intent — Second Payment (same intent, different transfer ID)

```bash
curl -X POST http://localhost:5000/api/webhooks/circle \
  -H "Content-Type: application/json" \
  -H "X-Circle-Signature: test-bypass" \
  -H "X-Circle-Key-Id: test-key-id" \
  -d '{
    "clientId": "c60d2d5b-203c-45bb-9f6e-93641d40a599",
    "notificationType": "transfers",
    "transfer": {
      "id": "test-transfer-b1-002",
      "source": { "type": "wallet", "id": "client-circle-wallet-uuid-001" },
      "destination": { "type": "wallet", "id": "your-wallet-id" },
      "amount": { "amount": "200.00", "currency": "USD" },
      "status": "complete",
      "paymentIntentId": "<SAME_CONTINUOUS_INTENT_ID>"
    }
  }'
# Expected: Second deposit row created; second Credit=200.00; balance=300.00 total
```

### Transfer Failed (any scenario)

```bash
curl -X POST http://localhost:5000/api/webhooks/circle \
  -H "Content-Type: application/json" \
  -H "X-Circle-Signature: test-bypass" \
  -H "X-Circle-Key-Id: test-key-id" \
  -d '{
    "clientId": "c60d2d5b-203c-45bb-9f6e-93641d40a599",
    "notificationType": "transfers",
    "transfer": {
      "id": "test-transfer-fail-001",
      "source": { "type": "blockchain", "chain": "ETH", "address": "0xfail..." },
      "destination": { "type": "wallet", "id": "your-wallet-id" },
      "amount": { "amount": "100.00", "currency": "USD" },
      "status": "failed",
      "paymentIntentId": "<REPLACE>"
    }
  }'
# Expected: deposit.status = 'Failed'; NO ledger entry
```

### Payout Complete

```bash
curl -X POST http://localhost:5000/api/webhooks/circle \
  -H "Content-Type: application/json" \
  -H "X-Circle-Signature: test-bypass" \
  -H "X-Circle-Key-Id: test-key-id" \
  -d '{
    "clientId": "c60d2d5b-203c-45bb-9f6e-93641d40a599",
    "notificationType": "payouts",
    "payout": {
      "id": "<REPLACE_WITH_REAL_PAYOUT_ID>",
      "status": "complete",
      "amount": { "amount": "100.00", "currency": "USD" }
    }
  }'
# Expected: withdrawal.status = 'Complete'
```

### Payout Failed

```bash
curl -X POST http://localhost:5000/api/webhooks/circle \
  -H "Content-Type: application/json" \
  -H "X-Circle-Signature: test-bypass" \
  -H "X-Circle-Key-Id: test-key-id" \
  -d '{
    "clientId": "c60d2d5b-203c-45bb-9f6e-93641d40a599",
    "notificationType": "payouts",
    "payout": {
      "id": "<REPLACE_WITH_REAL_PAYOUT_ID>",
      "status": "failed",
      "errorCode": "insufficient_funds"
    }
  }'
# Expected: withdrawal.status = 'Failed'; reversal Credit entry created; balance restored
```

### Address Book Recipient Active

```bash
curl -X POST http://localhost:5000/api/webhooks/circle \
  -H "Content-Type: application/json" \
  -H "X-Circle-Signature: test-bypass" \
  -H "X-Circle-Key-Id: test-key-id" \
  -d '{
    "clientId": "c60d2d5b-203c-45bb-9f6e-93641d40a599",
    "notificationType": "addressBookRecipients",
    "addressBookRecipient": {
      "id": "<REPLACE_WITH_RECIPIENT_ID>",
      "status": "active",
      "chain": "BASE",
      "address": "0xDestination..."
    }
  }'
# Expected: recipient marked active; payout submission unblocked
```

### Duplicate Webhook (Idempotency Test)

```bash
# Run same curl twice — second must return 200 with no additional processing
for i in 1 2; do
  echo "Attempt $i:"
  curl -s -o /dev/null -w "HTTP %{http_code}\n" \
    -X POST http://localhost:5000/api/webhooks/circle \
    -H "Content-Type: application/json" \
    -H "X-Circle-Signature: test-bypass" \
    -H "X-Circle-Key-Id: test-key-id" \
    -d '{
      "clientId": "c60d2d5b-...",
      "notificationType": "transfers",
      "transfer": {
        "id": "idempotency-test-transfer-001",
        "source": { "type": "wallet", "id": "wallet-001" },
        "destination": { "type": "wallet", "id": "your-wallet" },
        "amount": { "amount": "100.00", "currency": "USD" },
        "status": "complete",
        "paymentIntentId": "<REPLACE>"
      }
    }'
done
# Expected: HTTP 200 both times; exactly 1 Credit entry in DB
```

---

## 15. Acceptance Criteria for Go-Live

All of the following must pass before production deployment is approved.

### Functional (Must be 100% pass)

- [ ] All Group A tests pass (10/10)
- [ ] All Group B tests pass (12/12)
- [ ] All Group C tests pass (8/8)
- [ ] All Group D tests pass (12/12)
- [ ] All Group E tests pass (10/10)
- [ ] All Group F tests pass (7/7)
- [ ] All Group G tests pass (13/13)
- [ ] All Group H tests pass (8/8)

### Performance (Must meet all targets)

- [ ] All Group I tests pass (9/9)
- [ ] P95 webhook processing < 200ms
- [ ] P99 balance query < 50ms
- [ ] 50 concurrent webhooks — zero failures

### Security

- [ ] External penetration test completed — zero critical or high findings unresolved
- [ ] ECDSA validation tested: invalid signature → 401 (verified, not bypassed)
- [ ] No API keys in any log output
- [ ] No API keys in any config file committed to git
- [ ] OFAC screening active for Scenario 2 source addresses

### Compliance

- [ ] Circle Mint KYB approved
- [ ] Mastercard BIN + MTN agreement signed
- [ ] AML policy in place
- [ ] KYC process operational for client onboarding

### Operational

- [ ] Production smoke test: real $1 USDC deposit (both scenarios) — verified end-to-end
- [ ] Reconciliation: clean pass on production smoke test data
- [ ] Monitoring alerts configured and tested (alert fires on simulated failures)
- [ ] On-call runbook accessible and rehearsed
- [ ] Rollback plan documented
