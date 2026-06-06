# Circle USDC Integration — Master Knowledge Base Index

| Field | Value |
|---|---|
| Version | 2.0 |
| Date | 2026-06-06 |
| Status | Active — Single Source of Truth |
| System | FundManagement · Circle Mint · Mastercard MTN |

---

## Document Map

| File | Audience | Contents |
|---|---|---|
| **[KB_MANAGEMENT.md](KB_MANAGEMENT.md)** | CEO, CFO, Product, Compliance | Business model, flows in plain language, risk, go-to-market, fees |
| **[KB_TECHNICAL.md](KB_TECHNICAL.md)** | Engineering, Architecture | API reference, payment intent types, deposit/payout flows, webhooks, schema, error handling |
| **[KB_QA.md](KB_QA.md)** | QA, Test Engineers | Full test matrix (37 scenarios), sandbox setup, simulation commands, acceptance criteria |
| **[KB_OPERATIONS.md](KB_OPERATIONS.md)** | DevOps, SRE, On-Call | Deployment checklist, monitoring, alerting, troubleshooting runbook, reconciliation |

> **Start here if unsure which file to read.** Each file is self-contained for its audience.

---

## System in One Paragraph

FundManagement operates as a **BIN Sponsor** — a licensed entity holding a Mastercard BIN, issuing prepaid/debit cards to clients. It holds a **Circle Mint** business account for USDC treasury management. Clients fund their FundManagement accounts by sending USDC via Circle (either from their own Circle Mint account, or from any external blockchain wallet / exchange). FundManagement tracks all balances via an internal append-only ledger. When clients accumulate card transaction obligations, FundManagement settles directly with **Mastercard's Multi-Token Network (MTN)** by sending USDC via Circle Payout — replacing traditional USD wire settlement.

**There is no card authorization logic in this system.** Card authorization is handled by a separate card-processing system (out of scope). This document covers only: USDC deposit flows, ledger management, Circle payout flows, and Mastercard MTN settlement.

---

## Two Client Funding Scenarios

| | Scenario 1 | Scenario 2 |
|---|---|---|
| Client type | Circle Mint account holder | External wallet / exchange user |
| Transfer path | Circle-internal (no blockchain) | On-chain (ETH, Base, SOL, etc.) |
| Settlement speed | Seconds | Minutes (chain-dependent) |
| Webhook `source.type` | `"wallet"` | `"blockchain"` |

---

## Two Payment Intent Types

| | Transient | Continuous |
|---|---|---|
| Use case | One-time deposit request | Permanent reusable deposit address per client |
| Expiry | 24h (default) | Never (until manually expired) |
| Payments accepted | 1 (then complete) | Unlimited |
| Intent status after payment | `complete` | `open` (stays active) |
| Full reference | [KB_TECHNICAL.md § Payment Intent Types](KB_TECHNICAL.md#payment-intent-types) | Same |

---

## Key Flows at a Glance

```
CLIENT DEPOSIT (Scenario 1 — Circle Mint):
Client Circle Mint wallet → Circle-internal transfer → Webhook (source.type=wallet) → Credit ledger

CLIENT DEPOSIT (Scenario 2 — External wallet/exchange):
MetaMask / Coinbase / Binance → On-chain USDC → Blockchain confirmations → Webhook (source.type=blockchain) → Credit ledger

MASTERCARD SETTLEMENT:
Mastercard settlement file → Circle Payout to Mastercard MTN address → On-chain USDC → Mastercard confirms → Debit ledger
```

---

## Critical Rules (All Teams Must Know)

1. **Balances are never stored** — always derived from `ledger_entries` view
2. **Ledger is append-only** — no updates, no deletes; corrections = reversal Credit entries
3. **All webhooks are idempotent** — store `transfer.id` / `payout.id` before processing; `ON CONFLICT DO NOTHING`
4. **Circle Mint uses ECDSA signatures** — NOT HMAC; validate every webhook before processing
5. **Never commit API keys** — all secrets go to secrets manager (Key Vault / AWS SM)
6. **Card authorization is out of scope** — handled by separate card processor; not part of this system

---

## Glossary

| Term | Definition |
|---|---|
| BIN | Bank Identification Number — identifies the card-issuing institution (first 6–8 digits of card number) |
| BIN Sponsor | Licensed entity holding a Mastercard BIN; issues cards under it |
| Circle Mint | Circle's institutional USDC product — business account for USDC treasury management |
| Crypto Deposits API | Circle's API for receiving USDC (formerly Payments API) — uses Payment Intents |
| Crypto Payouts API | Circle's API for sending USDC (relaunched Sep 2025) — uses Address Book + Payouts |
| Transient Intent | Single-use payment intent; expires in 24h; accepts one USDC transfer |
| Continuous Intent | Reusable payment intent; never expires; accepts unlimited USDC transfers |
| MTN | Mastercard Multi-Token Network — blockchain-based settlement infrastructure |
| USDC | USD Coin — stablecoin pegged 1:1 to USD, issued and backed by Circle |
| ECDSA | Elliptic Curve Digital Signature Algorithm — used by Circle for webhook signatures |
| Ledger Entry | Append-only financial record — Credit (money in) or Debit (money out) |
| Idempotency Key | UUID sent with every API call to prevent duplicate processing on retry |
| KYB | Know Your Business — regulatory identity verification for business accounts |
| AML | Anti-Money Laundering |
| OFAC | Office of Foreign Assets Control — US sanctions list |
| Address Book | Circle's registry of pre-verified recipient addresses for payouts |

---

## Key External URLs

| Resource | URL |
|---|---|
| Circle Developer Console | https://console.circle.com |
| Circle API Documentation | https://developers.circle.com/circle-mint |
| Circle API Reference | https://developers.circle.com/api-reference/circle-mint |
| Circle Status Page | https://status.circle.com |
| Circle Sandbox API | https://api-sandbox.circle.com |
| Circle Production API | https://api.circle.com |
| Circle Faucet (testnet USDC) | https://faucet.circle.com |
| Circle Sample App | https://github.com/circlefin/payments-sample-app |
| Mastercard MTN | https://developer.mastercard.com/multi-token-network |
| Webhook Signature Docs | https://developers.circle.com/wallets/webhook-notifications |
