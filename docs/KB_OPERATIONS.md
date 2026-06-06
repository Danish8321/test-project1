# Circle USDC Integration — Operations Knowledge Base

| Field | Value |
|---|---|
| Version | 2.0 |
| Date | 2026-06-06 |
| Audience | DevOps · SRE · On-Call Engineers |
| Status | Active — Single Source of Truth |
| Index | [KB_INDEX.md](KB_INDEX.md) |

---

## Table of Contents

1. [System Overview for Ops](#1-system-overview-for-ops)
2. [Production Deployment Checklist](#2-production-deployment-checklist)
3. [Infrastructure & Configuration](#3-infrastructure--configuration)
4. [Monitoring & Alerting](#4-monitoring--alerting)
5. [Reconciliation](#5-reconciliation)
6. [Troubleshooting Runbook](#6-troubleshooting-runbook)
7. [On-Call Playbook](#7-on-call-playbook)
8. [Backup & Recovery](#8-backup--recovery)
9. [Maintenance & Housekeeping](#9-maintenance--housekeeping)
10. [Incident Post-Mortem Template](#10-incident-post-mortem-template)

---

## 1. System Overview for Ops

### What This System Does (Ops Perspective)

```
┌─────────────────────────────────────────────────────────────────────┐
│                    INBOUND FLOWS (Money In)                         │
│                                                                     │
│  Client (Circle Mint) ──→ Circle-internal transfer ──→ Webhook     │
│  Client (MetaMask etc.) ──→ Blockchain ──→ Confirmations ──→ Webhook│
│                                                                     │
│  Webhook ──→ /api/webhooks ──→ Validate ECDSA ──→ Credit Ledger    │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                    OUTBOUND FLOWS (Money Out)                       │
│                                                                     │
│  Mastercard Settlement File ──→ Create Withdrawal                  │
│  ──→ Create Address Book Recipient ──→ Poll Active                 │
│  ──→ POST /v1/payouts ──→ Webhook ──→ Debit Ledger                 │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                    CRITICAL DATA STORES                             │
│                                                                     │
│  PostgreSQL ──→ Ledger (append-only), Deposits, Withdrawals,       │
│                 Payment Intents, Webhook Events                     │
│  Circle API  ──→ Source of truth for payout/transfer status        │
└─────────────────────────────────────────────────────────────────────┘
```

### Key Failure Modes (Know Before On-Call)

| Failure | Detection | SLA Impact |
|---|---|---|
| Webhook endpoint down | Circle retries fail, deposits not credited | HIGH — clients lose funds visibility |
| ECDSA key cache miss | Every webhook returns 401 | HIGH — all events rejected |
| Continuous intent receiving USDC with no handler | Transfer stored, deposit NOT credited | HIGH — silent money loss |
| Payout stuck in `pending` | Withdrawal never closes | MEDIUM — settlement delay |
| Missing `paymentIntentId` on transfer | Deposit not linked, stays `Pending` | HIGH — client balance not updated |
| Circle API 429 rate limit | Recipient polling fails, payout delayed | MEDIUM |
| DB connection pool exhausted | API 500s | CRITICAL |

### What Is Out of Scope

- **Card authorization** — handled by separate card processor. Never page this team for card auth failures.
- **Client KYB/AML onboarding** — Circle handles KYB; AML address screening is an advisory flag, not a blocking gate in POC.

---

## 2. Production Deployment Checklist

Complete ALL items before first production USDC flow. Items marked `[BLOCKER]` must be done before go-live.

### 2.1 Circle Account Setup

- [ ] `[BLOCKER]` Circle Mint business account approved (KYB complete)
- [ ] `[BLOCKER]` Production API key created at https://console.circle.com — stored in Key Vault, NOT in code
- [ ] `[BLOCKER]` Webhook subscription created via `POST /v1/notifications/subscriptions` pointing to production endpoint
- [ ] `[BLOCKER]` Webhook subscription URL uses HTTPS with valid TLS certificate (no self-signed)
- [ ] Confirm subscription is active: `GET /v1/notifications/subscriptions` — status must be `enabled`
- [ ] Note the subscription ID — store in ops runbook for deregistration if needed
- [ ] Verify ECDSA public key fetch works: `GET /v2/notifications/publicKey/{keyId}` returns 200
- [ ] Test sandbox → production key rotation procedure

### 2.2 Mastercard MTN Setup

- [ ] `[BLOCKER]` Mastercard MTN recipient address confirmed in writing from Mastercard relationship manager
- [ ] `[BLOCKER]` Address Book recipient created in Circle production for Mastercard MTN address
- [ ] Recipient status must be `active` before first settlement — verify via `GET /v1/addressBook/recipients/{id}`
- [ ] Mastercard recipient ID stored securely in Key Vault / environment config
- [ ] Test payout of $1.00 USDC to Mastercard MTN address and confirm receipt (coordinate with Mastercard)

### 2.3 Infrastructure

- [ ] `[BLOCKER]` PostgreSQL 16 production instance provisioned with:
  - Daily automated backups, 30-day retention
  - Point-in-time recovery (PITR) enabled
  - Connection pooling (PgBouncer or equivalent)
  - SSL/TLS enforced
- [ ] `[BLOCKER]` All DB migrations run via DbUp on startup — verify migration history table populated
- [ ] `[BLOCKER]` Partial unique indexes present:
  ```sql
  -- Verify these exist in production DB
  SELECT indexname, indexdef FROM pg_indexes
  WHERE tablename = 'deposits' AND indexname LIKE 'uq_deposits_%';
  ```
- [ ] `[BLOCKER]` `webhook_events` table has `UNIQUE (circle_event_id)` constraint
- [ ] `[BLOCKER]` `funding_account_balances` view exists and returns correct values
- [ ] Read replica provisioned for reconciliation queries (do NOT run reconciliation on primary)
- [ ] API server auto-scaling configured (min 2 instances for HA)
- [ ] Load balancer health check: `GET /ping` returns 200

### 2.4 Secrets & Configuration

- [ ] `[BLOCKER]` `Circle:ApiKey` set in Key Vault — NOT in `appsettings.json`
- [ ] `[BLOCKER]` `ConnectionStrings:DefaultConnection` set in Key Vault
- [ ] `[BLOCKER]` No placeholder values (`SAND_API_KEY_HERE`, `localdev`) present in production config
- [ ] `[BLOCKER]` `Circle:BaseUrl` = `https://api.circle.com` (NOT sandbox)
- [ ] `appsettings.Production.json` committed with only non-secret keys
- [ ] Key Vault access policies confirmed for API managed identity

### 2.5 Allowlist & Network

- [ ] `[BLOCKER]` Circle webhook sender IPs allowlisted at WAF/firewall:
  ```
  3.230.111.7
  3.90.127.28
  35.169.154.32
  54.88.227.75
  ```
- [ ] Outbound HTTPS to `api.circle.com` port 443 allowed from API servers
- [ ] Outbound HTTPS to `api.circle.com` port 443 allowed from reconciliation job servers

### 2.6 Observability

- [ ] Application Insights / Datadog / Grafana connected and receiving traces
- [ ] Log sink (Azure Monitor / CloudWatch) receiving structured logs with `CorrelationId`, `CustomerId`
- [ ] Key dashboards deployed (see Section 4)
- [ ] Alert policies active for all P1 conditions (see Section 4)
- [ ] On-call rotation configured in PagerDuty / OpsGenie

### 2.7 Final Smoke Test (Production)

```
1. Create a test customer (CustomerType = ExternalWallet)
2. Create transient payment intent — verify Circle returns deposit address
3. Send $1.00 USDC from test wallet to the deposit address
4. Wait for webhook — verify:
   a. webhook_events row inserted with status = 'Processed'
   b. deposits row status = 'Completed'
   c. ledger_entries row with entry_type = 'Credit'
   d. funding_account_balances shows $1.00
5. Create withdrawal of $0.50 to a test address
   a. Verify recipient created and reaches 'active'
   b. Verify payout created
   c. Wait for payout webhook — verify:
      i.  withdrawals row status = 'Completed'
      ii. ledger_entries row with entry_type = 'Debit'
      iii. funding_account_balances shows $0.50
6. Remove test data or flag as test records
```

---

## 3. Infrastructure & Configuration

### 3.1 Environment Configuration

```
┌─────────────────┬────────────────────────────┬────────────────────────────┐
│ Config Key      │ Sandbox                    │ Production                 │
├─────────────────┼────────────────────────────┼────────────────────────────┤
│ BaseUrl         │ https://api-sandbox.circle │ https://api.circle.com     │
│                 │ .com                       │                            │
│ ApiKey          │ SAND_API_KEY_***           │ [Key Vault]                │
│ WebhookEndpoint │ ngrok / dev tunnel         │ https://api.yourdomain.com │
│                 │                            │ /api/webhooks              │
│ DB              │ localhost:5432/ifs_poc      │ [Key Vault conn string]    │
│ LogLevel        │ Debug                      │ Information                │
└─────────────────┴────────────────────────────┴────────────────────────────┘
```

### 3.2 Critical Database Objects

```sql
-- Verify these exist before any production traffic

-- Core tables
SELECT table_name FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name IN (
    'customers', 'funding_accounts', 'payment_intents',
    'deposits', 'withdrawals', 'ledger_entries', 'webhook_events'
  );

-- Balance view
SELECT * FROM funding_account_balances LIMIT 1;

-- Partial unique indexes (critical for continuous intents)
SELECT indexname FROM pg_indexes WHERE tablename = 'deposits';
-- Expected: uq_deposits_transient, uq_deposits_continuous

-- Idempotency constraint
SELECT constraint_name FROM information_schema.table_constraints
WHERE table_name = 'webhook_events' AND constraint_type = 'UNIQUE';
-- Expected: webhook_events_circle_event_id_key
```

### 3.3 ECDSA Public Key Cache

The API caches Circle's ECDSA public key in memory, keyed by `keyId`. On restart, the cache is empty — the first webhook after restart fetches the key from Circle.

**If Circle rotates their key:**
1. Old `keyId` webhooks continue to work until Circle stops sending them.
2. New `keyId` triggers a fresh fetch from `GET /v2/notifications/publicKey/{newKeyId}`.
3. Both keys can coexist in cache.

**If key fetch fails (Circle API down):**
- Webhook returns 401 (key not verifiable — reject, don't retry blindly).
- Circle will retry the webhook per their retry schedule.
- Monitor for sustained 401 rate above baseline.

### 3.4 Payment Intent Monitoring Notes

| Intent Type | Lifecycle | DB Rows | Webhook Count |
|---|---|---|---|
| `transient` | `created → pending → complete/expired` | 1 payment_intent + 1 deposit | 1–2 (status changes) |
| `continuous` | `created → open → ... → closed` | 1 payment_intent + N deposits | N per transfer + intent status changes |

**Continuous intent watchpoints:**
- An open continuous intent should only receive USDC from its assigned customer.
- Unexpected large USDC receipts on a continuous intent (far above usual amounts) may indicate address sharing — alert and review.
- When a continuous intent is manually closed (`POST /v1/paymentIntents/{id}/expire`), verify no in-flight transfers were orphaned (query deposits with `status = 'Pending'` linked to that intent).

---

## 4. Monitoring & Alerting

### 4.1 Key Metrics to Track

#### Business Metrics (Grafana / App Insights dashboard)

| Metric | Query / Source | Alert Threshold |
|---|---|---|
| Deposits credited (last 1h) | `COUNT(*) FROM deposits WHERE status='Completed' AND updated_at > NOW()-'1h'` | Alert if 0 AND expected volume > 0 |
| Withdrawals completed (last 1h) | `COUNT(*) FROM withdrawals WHERE status='Completed' AND updated_at > NOW()-'1h'` | Alert if 0 AND settlement window open |
| Pending deposits > 30 min | `COUNT(*) FROM deposits WHERE status='Pending' AND created_at < NOW()-'30m'` | Alert if > 0 |
| Pending withdrawals > 60 min | `COUNT(*) FROM withdrawals WHERE status='Pending' AND created_at < NOW()-'60m'` | Alert if > 0 |
| Unprocessed webhooks | `COUNT(*) FROM webhook_events WHERE status='Received' AND created_at < NOW()-'10m'` | Alert if > 5 |
| Ledger integrity | `SELECT ABS(SUM(CASE WHEN entry_type='Credit' THEN amount ELSE -amount END)) FROM ledger_entries` vs Circle balance | Alert if diff > $0.01 |

#### Technical Metrics

| Metric | Alert Threshold | Severity |
|---|---|---|
| Webhook endpoint HTTP 4xx rate | > 5% over 5 min | P2 |
| Webhook endpoint HTTP 5xx rate | > 1% over 5 min | P1 |
| Webhook ECDSA validation failures | > 3 in 5 min | P2 — possible key rotation or attack |
| Circle API response time (p99) | > 5s | P2 |
| Circle API error rate | > 10% over 5 min | P1 |
| DB connection pool utilization | > 80% | P2 |
| DB query time (p99) | > 1s | P3 |
| API response time (p99) | > 2s | P2 |
| Reconciliation job failures | Any failure | P1 |
| Continuous intent open > 30 days | COUNT > 0 | P3 — review if intentional |

### 4.2 Alert Routing

```
P1 (Critical — page immediately):
  → PagerDuty → On-call SRE + Engineering Lead
  Examples: webhook 5xx, reconciliation failure, DB down

P2 (High — alert within 5 min):
  → PagerDuty (low urgency) → On-call SRE
  Examples: pending deposits stuck, ECDSA failures, Circle API degraded

P3 (Medium — Slack #ops-alerts):
  → Slack notification only
  Examples: open continuous intents review, slow queries

Business Hours Only:
  → Slack #fin-ops
  Examples: ledger drift warnings, reconciliation mismatches needing manual review
```

### 4.3 Dashboard Layout

```
Row 1 — Financial Health
  [Circle USDC Balance]  [Total Credits Today]  [Total Debits Today]  [Pending Items]

Row 2 — Deposit Flow
  [Deposits/hr by type (transient/continuous)]  [Source type split (wallet/blockchain)]
  [Deposit success rate]  [Avg time to credit]

Row 3 — Withdrawal Flow
  [Withdrawals/hr]  [Payout success rate]  [Avg time to complete]  [Failed payouts]

Row 4 — Webhook Health
  [Webhooks received/min]  [Validation success rate]  [Processing lag]  [Failed events]

Row 5 — Reconciliation
  [Last reconciliation run]  [Matched %]  [Mismatches]  [Unlinked transfers]

Row 6 — Infrastructure
  [API latency p50/p99]  [DB connections]  [Error rate]  [Circle API latency]
```

### 4.4 Log Query Cheat Sheet

```sql
-- All webhook events in last hour (PostgreSQL)
SELECT created_at, event_type, circle_event_id, status, error_message
FROM webhook_events
WHERE created_at > NOW() - INTERVAL '1 hour'
ORDER BY created_at DESC;

-- Stuck pending deposits
SELECT d.id, d.customer_id, d.amount, d.status, d.created_at,
       pi.intent_type, pi.circle_payment_intent_id
FROM deposits d
JOIN payment_intents pi ON pi.id = d.payment_intent_id
WHERE d.status = 'Pending'
  AND d.created_at < NOW() - INTERVAL '30 minutes'
ORDER BY d.created_at;

-- Unlinked transfer webhooks (transfer received, no deposit settled)
SELECT we.circle_event_id, we.created_at, we.payload
FROM webhook_events we
WHERE we.event_type = 'transfers'
  AND we.status = 'Received'
  AND we.created_at < NOW() - INTERVAL '10 minutes';

-- Balance drift check
SELECT fa.id, fa.customer_id, fa.currency,
       COALESCE(SUM(CASE WHEN le.entry_type='Credit' THEN le.amount ELSE -le.amount END), 0) AS derived_balance
FROM funding_accounts fa
LEFT JOIN ledger_entries le ON le.funding_account_id = fa.id
GROUP BY fa.id, fa.customer_id, fa.currency
ORDER BY derived_balance DESC;

-- Continuous intents still open
SELECT pi.id, pi.customer_id, pi.circle_payment_intent_id,
       pi.created_at, COUNT(d.id) AS deposit_count, SUM(d.amount) AS total_deposited
FROM payment_intents pi
LEFT JOIN deposits d ON d.payment_intent_id = pi.id AND d.status = 'Completed'
WHERE pi.intent_type = 'continuous'
  AND pi.status = 'open'
GROUP BY pi.id, pi.customer_id, pi.circle_payment_intent_id, pi.created_at
ORDER BY pi.created_at;
```

---

## 5. Reconciliation

### 5.1 What Reconciliation Does

Reconciliation compares FundManagement's internal ledger against Circle's API records to detect:

1. **Missing credits** — Circle shows a completed transfer, but no ledger entry exists
2. **Missing debits** — Circle shows a completed payout, but no ledger debit exists
3. **Phantom entries** — Ledger has entries with no corresponding Circle record
4. **Status mismatches** — Circle says `complete`, internal DB says `Pending`
5. **Amount mismatches** — Circle amount ≠ ledger amount
6. **Unlinked transfers** — Transfers in Circle not linked to any payment intent (continuous intent gap)

### 5.2 Reconciliation Schedule

| Job | Frequency | Time Window | Target |
|---|---|---|---|
| Deposits reconciliation | Every 4 hours | Last 8 hours | All deposits |
| Payouts reconciliation | Every 4 hours | Last 8 hours | All withdrawals |
| Full ledger reconciliation | Daily at 02:00 UTC | Previous calendar day | All records |
| Balance reconciliation | Every 15 minutes | Current balances only | Circle balance vs ledger |

### 5.3 Reconciliation Logic

#### Deposits (Transient Intents)

```
FOR EACH deposit WHERE status IN ('Pending', 'Processing') AND created_at > (NOW - window):
    GET /v1/paymentIntents/{circle_payment_intent_id}
    
    IF Circle status = 'complete' AND internal status != 'Completed':
        ALERT: "Deposit {id} complete in Circle but not settled internally"
        LOG for manual review
    
    IF Circle status = 'expired' AND internal status = 'Pending':
        UPDATE deposits SET status = 'Expired'
        LOG INFO: "Expired deposit {id} synced"
    
    IF Circle status = 'failed':
        UPDATE deposits SET status = 'Failed'
        LOG WARNING: "Failed deposit {id} synced"
```

#### Deposits (Continuous Intents)

```
FOR EACH payment_intent WHERE intent_type = 'continuous' AND status = 'open':
    GET /v1/paymentIntents/{circle_payment_intent_id}
    
    FOR EACH transfer in circle_intent.payments:
        IF no deposits row WHERE circle_transfer_id = transfer.id AND status = 'Completed':
            ALERT: "Unmatched transfer {transfer.id} on continuous intent {intent_id}"
            LOG for manual credit review
```

#### Payouts / Withdrawals

```
FOR EACH withdrawal WHERE status = 'Processing' AND created_at > (NOW - window):
    GET /v1/payouts/{circle_payout_id}
    
    IF Circle status = 'complete' AND internal status != 'Completed':
        ALERT: "Withdrawal {id} complete in Circle but not closed internally"
    
    IF Circle status = 'failed' AND internal status = 'Processing':
        ALERT P1: "Payout failed — USDC did NOT leave Circle"
        LOG error_code from Circle response
```

#### Balance Reconciliation

```
GET /v1/businessAccount/balances  → Circle USDC balance (source of truth)
SELECT SUM(CASE WHEN entry_type='Credit' THEN amount ELSE -amount END)
  FROM ledger_entries             → Internal derived balance

IF ABS(Circle balance - internal balance) > 0.01:
    ALERT P1: "Balance drift detected: Circle=${circle}, Internal=${internal}, Diff=${diff}"
```

### 5.4 Reconciliation SQL Queries

```sql
-- Completed Circle intents not settled internally
-- Run after fetching Circle data into temp table
SELECT d.id AS deposit_id, d.circle_payment_intent_id, d.status AS internal_status,
       c.status AS circle_status, c.amount AS circle_amount, d.amount AS internal_amount
FROM deposits d
JOIN circle_intents_snapshot c ON c.id = d.circle_payment_intent_id  -- temp table from API
WHERE d.status != 'Completed'
  AND c.status = 'complete';

-- Ledger entries without corresponding deposit/withdrawal
SELECT le.id, le.funding_account_id, le.entry_type, le.amount, le.reference_id, le.created_at
FROM ledger_entries le
LEFT JOIN deposits d ON d.id::TEXT = le.reference_id AND le.entry_type = 'Credit'
LEFT JOIN withdrawals w ON w.id::TEXT = le.reference_id AND le.entry_type = 'Debit'
WHERE d.id IS NULL AND w.id IS NULL
ORDER BY le.created_at DESC;

-- Duplicate ledger entries (should be zero — idempotency check)
SELECT reference_id, entry_type, COUNT(*) AS count
FROM ledger_entries
GROUP BY reference_id, entry_type
HAVING COUNT(*) > 1;

-- Transfers received but not linked to any deposit
SELECT we.id, we.circle_event_id, we.created_at, we.payload
FROM webhook_events we
WHERE we.event_type = 'transfers'
  AND we.status = 'Received'
  AND NOT EXISTS (
      SELECT 1 FROM deposits d
      WHERE d.circle_transfer_id = we.circle_event_id
        AND d.status = 'Completed'
  )
ORDER BY we.created_at DESC;
```

### 5.5 Reconciliation Mismatch Resolution

| Mismatch Type | Action | Who |
|---|---|---|
| Circle complete, internal Pending | Check `webhook_events` for transfer event. If missing: manual credit via reversal entry. | On-call Eng |
| Continuous intent unlinked transfer | Identify customer via `paymentIntent.customerId`. Create deposit + credit ledger entry manually. | Senior Eng |
| Balance drift < $1 | Investigate specific deposits/withdrawals in drift window. Usually a processing race. | On-call Eng |
| Balance drift > $1 | P1 alert. Hold all new payouts. Escalate to Finance + Circle support. | Eng Lead + Finance |
| Phantom ledger entry (no Circle record) | Do NOT delete. Flag with metadata. Review for fraud/system error. | Senior Eng + Compliance |
| Payout complete in Circle, no ledger debit | Check webhook_events for `payouts` event. If missing: manual debit via correction entry. | On-call Eng |

---

## 6. Troubleshooting Runbook

### RB-001: Webhook Returns 401 — ECDSA Validation Failed

**Symptoms:** Circle retries piling up; `webhook_events` table not growing; 401 logs in API.

**Possible Causes:**
1. `X-Circle-Signature` or `X-Circle-Key-Id` header missing (non-Circle source)
2. Circle rotated public key — new `keyId` in cache miss
3. Raw body was re-serialized (whitespace altered) before verification
4. Public key fetch from Circle failing (Circle API degraded)

**Steps:**
```
1. Check logs for the keyId value:
   grep "X-Circle-Key-Id" in request logs

2. Attempt manual key fetch:
   curl -H "Authorization: Bearer {API_KEY}" \
        https://api.circle.com/v2/notifications/publicKey/{keyId}
   → 200 = key exists, cache issue
   → 404 = invalid keyId, possible attack or misconfiguration
   → 5xx = Circle API degraded

3. If Circle API degraded:
   - Monitor Circle status: https://status.circle.com
   - Webhooks will be retried by Circle — do NOT replay manually yet
   - Alert P2, monitor

4. If body re-serialization suspected:
   - Check WebhooksController — confirm raw body read from HttpContext.Request.Body
   - Confirm body is NOT parsed-then-re-serialized before signature check
```

---

### RB-002: Deposit Stuck in Pending — Transfer Received, Not Settled

**Symptoms:** Customer reports USDC sent but balance not updated. Webhook event stored with `status = 'Received'`.

**Possible Causes:**
1. `transfer.paymentIntentId` absent in Circle payload (known gap — see KB_TECHNICAL.md § Known Gap)
2. `payment_intent_id` in deposit record doesn't match any `payment_intents` row
3. Exception during deposit settlement — webhook stored but handler threw

**Steps:**
```sql
-- Step 1: Find the unlinked transfer
SELECT we.id, we.circle_event_id, we.payload, we.error_message
FROM webhook_events we
WHERE we.event_type = 'transfers'
  AND we.status = 'Received'
ORDER BY we.created_at DESC LIMIT 20;

-- Step 2: Check the payload for paymentIntentId
-- Parse payload JSON and look for transfer.paymentIntentId

-- Step 3: If paymentIntentId absent, find the matching payment intent by:
--   a. The customer's deposit address (payment_intents.deposit_address)
--   b. The transfer.destination.address in the payload

-- Step 4: Manual settlement (if confirmed legitimate transfer)
BEGIN;
  UPDATE deposits SET status = 'Completed', updated_at = NOW()
  WHERE circle_payment_intent_id = '{intentId}';
  
  INSERT INTO ledger_entries (id, funding_account_id, entry_type, amount, reference_id, created_at)
  VALUES (gen_random_uuid(), '{fundingAccountId}', 'Credit', {amount}, '{depositId}', NOW());
  
  UPDATE webhook_events SET status = 'Processed', updated_at = NOW()
  WHERE circle_event_id = '{transferId}';
COMMIT;

-- Step 5: File incident report
-- Step 6: Long-term: implement polling fallback (GET /v1/paymentIntents/{id} on schedule)
```

---

### RB-003: Continuous Intent — USDC Received, Deposit Not Created

**Symptoms:** Circle shows transfer on a continuous intent. No new deposit row in DB. Balance not updated.

**Possible Causes:**
1. `webhook_events` row stored but handler failed to create deposit (check `error_message`)
2. Continuous intent no longer in DB (`payment_intents` row missing or status = `closed`)
3. Transfer's `paymentIntentId` not in transfer payload (same gap as RB-002)

**Steps:**
```sql
-- Find the continuous intent
SELECT pi.*, COUNT(d.id) AS deposit_count
FROM payment_intents pi
LEFT JOIN deposits d ON d.payment_intent_id = pi.id
WHERE pi.intent_type = 'continuous'
GROUP BY pi.id
ORDER BY pi.created_at DESC;

-- Find webhook events for this intent
SELECT * FROM webhook_events
WHERE payload::TEXT LIKE '%{circle_payment_intent_id}%'
ORDER BY created_at DESC;

-- If webhook was processed but deposit not created:
-- Check for exception in webhook_events.error_message
-- If no exception: possible race condition — check for deposit with status Pending

-- Manual deposit creation (confirmed legitimate):
BEGIN;
  INSERT INTO deposits (id, customer_id, funding_account_id, payment_intent_id,
                        circle_transfer_id, amount, status, created_at, updated_at)
  VALUES (gen_random_uuid(), '{customerId}', '{fundingAccountId}', '{paymentIntentId}',
          '{transferId}', {amount}, 'Completed', NOW(), NOW());
  
  INSERT INTO ledger_entries (id, funding_account_id, entry_type, amount, reference_id, created_at)
  VALUES (gen_random_uuid(), '{fundingAccountId}', 'Credit', {amount}, '{depositId}', NOW());
COMMIT;
```

---

### RB-004: Payout Stuck in Pending

**Symptoms:** Withdrawal row status = `Processing`/`Pending` for > 60 min. No payout webhook received.

**Steps:**
```bash
# Step 1: Check Circle payout status
curl -H "Authorization: Bearer {API_KEY}" \
     https://api.circle.com/v1/payouts/{circle_payout_id}

# Expected: {"data": {"status": "pending|complete|failed", ...}}
```

```
If status = 'complete':
  → Webhook was lost or not delivered
  → Check webhook_events for this payout.id
  → If absent: manually close withdrawal + create debit ledger entry
  → File ticket with Circle support for missed webhook

If status = 'failed':
  → Check errorCode: insufficient_funds | transaction_denied | transaction_failed | transaction_returned
  → insufficient_funds: Circle wallet balance too low — fund the wallet
  → transaction_denied: compliance/OFAC issue — escalate to Compliance
  → transaction_failed / transaction_returned: retry payout (create new withdrawal)
  → Reverse the debit ledger entry (create Credit reversal entry)

If status = 'pending' > 6 hours:
  → Circle API degraded or blockchain congestion
  → Check https://status.circle.com
  → Do NOT retry — duplicate payout risk
  → Monitor and wait
```

---

### RB-005: Address Book Recipient Never Reaches Active

**Symptoms:** Withdrawal creation times out. Logs show "Recipient still pending after 30s".

**Possible Causes:**
1. Circle compliance hold on the destination address
2. Circle API performance issue
3. Invalid address format for the chain

**Steps:**
```bash
# Check recipient status
curl -H "Authorization: Bearer {API_KEY}" \
     https://api.circle.com/v1/addressBook/recipients/{recipientId}
```

```
If status = 'pending' (prolonged):
  → Wait up to 10 minutes — some addresses take longer
  → If still pending after 10 min: contact Circle support with recipient ID

If status = 'failed':
  → Verify address format is valid for the chain
  → Check Circle support for compliance hold
  → Update destination address if incorrect

Retry flow:
  1. Create new withdrawal with corrected address
  2. Old withdrawal: mark as Failed
  3. Reverse any debit ledger entry that was created pre-emptively
```

---

### RB-006: Circle API 401 Unauthorized

**Symptoms:** All Circle API calls return 401. ECDSA key fetches fail. Deposit creation fails.

**Steps:**
```
1. Verify API key is not expired:
   curl -H "Authorization: Bearer {API_KEY}" https://api.circle.com/ping
   → 200 = key valid
   → 401 = key invalid/expired

2. If key expired or rotated:
   a. Log into Circle console: https://console.circle.com
   b. Generate new API key
   c. Update Key Vault secret
   d. Restart API servers (key loaded at startup via config)
   e. Verify: curl -H "Authorization: Bearer {NEW_KEY}" https://api.circle.com/ping

3. If key valid but still 401:
   → Check request format — Bearer scheme required
   → Check for trailing whitespace in stored key
   → Check Circle status page for API auth issues
```

---

### RB-007: Duplicate Credits — Ledger Has Two Credit Entries for Same Transfer

**Symptoms:** Client reports inflated balance. Reconciliation finds duplicate ledger entries.

**Possible Causes:**
1. Idempotency check bypassed (bug introduced in code change)
2. Race condition — two webhook deliveries processed simultaneously
3. Manual credit applied on top of automatic settlement

**Steps:**
```sql
-- Identify duplicates
SELECT reference_id, entry_type, COUNT(*) AS count, SUM(amount) AS total
FROM ledger_entries
WHERE entry_type = 'Credit'
GROUP BY reference_id, entry_type
HAVING COUNT(*) > 1;

-- For each duplicate: identify the erroneous entry
-- The FIRST entry by created_at is typically legitimate

-- Correction: create a reversal (do NOT delete)
INSERT INTO ledger_entries (id, funding_account_id, entry_type, amount, reference_id, created_at, notes)
VALUES (
    gen_random_uuid(),
    '{fundingAccountId}',
    'Debit',         -- reversal of erroneous Credit
    {amount},
    'REVERSAL:{original_entry_id}',
    NOW(),
    'Duplicate credit reversal — Incident #{incidentId}'
);

-- Verify balance after reversal
SELECT * FROM funding_account_balances WHERE id = '{fundingAccountId}';
```

---

### RB-008: Mastercard Settlement Failure

**Symptoms:** Settlement payout failed. Mastercard balance not updated. Potential settlement deadline miss.

**Steps:**
```
1. Check withdrawal status in DB:
   SELECT * FROM withdrawals WHERE purpose = 'MastercardSettlement' ORDER BY created_at DESC LIMIT 5;

2. Check Circle payout status (RB-004 applies)

3. If payout failed:
   a. Check errorCode
   b. insufficient_funds → Circle wallet balance insufficient:
      i.  Verify Circle business account balance: GET /v1/businessAccount/balances
      ii. If balance low: investigate if large payouts were missed or a deposit failed
      iii. Fund Circle account via bank wire
   c. transaction_denied → Mastercard MTN address issue:
      i.  Verify address in Circle address book is still active
      ii. Contact Mastercard relationship manager

4. Escalation path for settlement deadline miss:
   → Notify Finance within 15 minutes of confirmed failure
   → Notify Mastercard via relationship manager within 30 minutes
   → Document timeline for incident report

5. Do NOT retry settlement payout until root cause confirmed — duplicate settlement risk
```

---

### RB-009: Webhook Endpoint Returning 500

**Symptoms:** All Circle webhooks returning 500. Circle retry queue growing.

**Immediate Actions:**
```
1. Check API server health:
   curl https://api.yourdomain.com/ping

2. Check recent deploy:
   Check deployment pipeline — did a deploy just happen?
   If yes: rollback via deployment system

3. Check DB connectivity:
   Monitor DB connection count vs pool limit
   If pool exhausted: restart API servers sequentially (not all at once)

4. Check structured logs for exception details:
   → ApplicationInsights / CloudWatch for last 30 min
   → Filter by StatusCode = 500 AND Path = "/api/webhooks"

5. If webhook endpoint recovering:
   → Circle will retry on its own schedule
   → Do NOT replay webhooks manually yet
   → Monitor webhook_events table — count should grow once endpoint recovers

6. If extended outage (> 15 min):
   → Contact Circle support to suspend webhook delivery temporarily
   → Prevents retry queue overflow
   → Resume delivery once endpoint stable
   → After resume: check for missed events via reconciliation (Section 5)
```

---

### RB-010: High Webhook ECDSA Validation Failure Rate (Potential Attack)

**Symptoms:** > 10 ECDSA validation failures in 5 minutes. Not all are from Circle IPs.

**Steps:**
```
1. Check source IPs of failing requests:
   Select X-Forwarded-For / RemoteIpAddress from logs
   
   Circle legitimate IPs:
   3.230.111.7 / 3.90.127.28 / 35.169.154.32 / 54.88.227.75

2. If failures from non-Circle IPs:
   → Potential probing/spoofing attempt
   → ECDSA validation is correctly rejecting them (working as designed)
   → Consider IP allowlist enforcement at WAF level (not just as a validation hint)
   → Alert security team if volume is high

3. If failures from Circle IPs:
   → Possible key rotation — see RB-001
   → Check X-Circle-Key-Id in failing requests

4. WAF IP allowlist enforcement (if not already in place):
   Add rule: Block POST /api/webhooks from any IP not in Circle allowlist
   This reduces attack surface — ECDSA is defense in depth, not the first gate
```

---

## 7. On-Call Playbook

### 7.1 On-Call Responsibilities

| Time | Task |
|---|---|
| Start of shift | Check reconciliation results from overnight run; review pending deposits > 30 min |
| During Mastercard settlement window | Active monitoring of withdrawal/payout flow; P1 on-call readiness |
| End of shift | Hand off any open incidents with status and next steps |

### 7.2 Incident Severity Classification

| Severity | Definition | Response Time | Example |
|---|---|---|---|
| P1 | Money at risk / balance drift / system down | Page immediately, 15 min response | Webhook 500, balance drift, payout failure during settlement |
| P2 | Degraded service / stuck transactions | Alert within 5 min, 30 min response | Pending deposits > 30 min, Circle API degraded |
| P3 | Non-critical / informational | Slack, next business day | Slow queries, open continuous intents review |

### 7.3 Escalation Path

```
On-Call SRE
    ↓ (if P1 unresolved in 30 min)
Engineering Lead
    ↓ (if financial impact confirmed)
Finance Lead + Compliance
    ↓ (if Circle API issue)
Circle Support: support@circle.com / https://console.circle.com/support
    ↓ (if Mastercard settlement at risk)
Mastercard Relationship Manager
```

### 7.4 Circle Support Contact

- Support portal: https://console.circle.com (submit ticket from account)
- Status page: https://status.circle.com
- For production incidents involving USDC movement: reference payout ID / transfer ID / payment intent ID in all tickets

---

## 8. Backup & Recovery

### 8.1 Database Backup

| Backup Type | Frequency | Retention | Recovery Time Objective |
|---|---|---|---|
| Full snapshot | Daily at 01:00 UTC | 30 days | < 4 hours |
| WAL / PITR logs | Continuous | 7 days | < 30 min (to specific transaction) |
| Logical dump | Weekly | 12 weeks | < 6 hours (full restore) |

**Critical**: The `ledger_entries` table is append-only. Any restore must be to a point AFTER the last confirmed reconciliation, not before, to avoid re-crediting already-settled deposits.

### 8.2 Recovery Procedure

```
1. Identify recovery point:
   a. Find last successful reconciliation run time
   b. Identify any in-flight transactions at failure time
   c. Choose PITR target = last reconciliation run time

2. Restore DB to target:
   pg_restore / AWS RDS PITR / Azure Flexible Server PITR

3. Post-restore validation:
   a. Run reconciliation against Circle API for the window between restore point and now
   b. Re-apply any confirmed transactions that occurred after restore point
   c. Check for duplicate entries (transactions that processed during downtime may replay via webhook)

4. Resume webhook endpoint:
   a. Inform Circle support to resume webhook delivery if suspended
   b. Monitor for replayed webhooks — idempotency constraints will prevent duplicates

5. Communicate to stakeholders:
   a. Finance: report any gaps in ledger
   b. Operations: status update
   c. Affected customers: per communication runbook
```

---

## 9. Maintenance & Housekeeping

### 9.1 Routine Tasks

| Task | Frequency | Owner |
|---|---|---|
| Review open continuous intents > 30 days | Weekly | On-call SRE |
| Archive expired transient intents | Weekly (automated) | Scheduled job |
| Review failed webhook events | Daily | On-call SRE |
| Rotate Circle API key | Every 90 days | Engineering Lead |
| Review address book recipients (active but unused > 90 days) | Monthly | Engineering Lead |
| Full reconciliation audit | Monthly | Finance + Engineering |
| Review Circle IP allowlist for changes | Quarterly | SRE |

### 9.2 Archival Queries

```sql
-- Archive expired transient intents older than 30 days
UPDATE payment_intents
SET status = 'expired'
WHERE intent_type = 'transient'
  AND status = 'created'
  AND expires_on < NOW() - INTERVAL '24 hours';

-- Find address book recipients not used in > 90 days
SELECT w.circle_recipient_id, MAX(w.created_at) AS last_used
FROM withdrawals w
GROUP BY w.circle_recipient_id
HAVING MAX(w.created_at) < NOW() - INTERVAL '90 days';

-- Cleanup stale 'Received' webhook events older than 7 days
-- (Only after confirming these are truly unprocessable)
-- NOTE: Do NOT delete — mark as Abandoned for audit trail
UPDATE webhook_events
SET status = 'Abandoned',
    error_message = 'No matching intent found after 7-day review period'
WHERE status = 'Received'
  AND created_at < NOW() - INTERVAL '7 days';
```

### 9.3 API Key Rotation Procedure

```
1. Log into Circle console: https://console.circle.com
2. Generate new API key (do NOT revoke old key yet)
3. Update Key Vault secret with new key value
4. Deploy API server with new key (rolling restart, zero downtime)
5. Verify new key works: GET /ping from each API server instance
6. Revoke old API key in Circle console
7. Verify webhook subscription still active: GET /v1/notifications/subscriptions
8. Document rotation in ops log with date, engineer, reason
```

---

## 10. Incident Post-Mortem Template

```markdown
# Incident Post-Mortem — {Incident ID}

**Date:** YYYY-MM-DD  
**Severity:** P1 / P2 / P3  
**Duration:** HH:MM  
**Financial Impact:** $X USDC affected / No financial impact  
**Systems Affected:** Webhooks / Deposits / Withdrawals / Reconciliation

## Timeline (all times UTC)

| Time | Event |
|------|-------|
| HH:MM | Incident detected via [alert/user report/monitoring] |
| HH:MM | On-call engaged |
| HH:MM | Root cause identified |
| HH:MM | Mitigation applied |
| HH:MM | Service restored |
| HH:MM | Post-incident reconciliation complete |

## Root Cause

[One paragraph. What failed and why.]

## Impact

- Deposits affected: N
- Withdrawals affected: N
- Customers affected: N
- USDC balance discrepancy: $X (resolved / outstanding)
- Circle webhooks lost: N (recovered via retry / manual)

## Resolution

[Steps taken to resolve.]

## Action Items

| Item | Owner | Due Date |
|------|-------|----------|
| | | |

## Lessons Learned

[What did this incident teach us? What would prevent recurrence?]
```

---

## Reference

| Resource | Link |
|---|---|
| Circle Status | https://status.circle.com |
| Circle Console | https://console.circle.com |
| Circle Support | https://console.circle.com/support |
| Circle API Sandbox | https://api-sandbox.circle.com |
| Circle API Production | https://api.circle.com |
| Circle Webhook Docs | https://developers.circle.com/wallets/webhook-notifications |
| Mastercard MTN | https://developer.mastercard.com/multi-token-network |
| KB Index | [KB_INDEX.md](KB_INDEX.md) |
| Technical Reference | [KB_TECHNICAL.md](KB_TECHNICAL.md) |
| QA Reference | [KB_QA.md](KB_QA.md) |
| Management Reference | [KB_MANAGEMENT.md](KB_MANAGEMENT.md) |
