# Circle APIs

Always use Circle MCP to verify endpoints before implementing. Sandbox base URL: `https://api-sandbox.circle.com`

## Connectivity
```
GET  /ping
GET  /v1/configuration
GET  /v1/stablecoins
GET  /v1/businessAccount/balances
```

## Deposits (Payment Intents)
```
POST /v1/paymentIntents
GET  /v1/paymentIntents/{id}
GET  /v1/paymentIntents
```

## Payouts
```
POST /v1/businessAccount/wallets/addresses/recipient
GET  /v1/businessAccount/wallets/addresses/recipient
POST /v1/businessAccount/payouts
GET  /v1/businessAccount/payouts/{id}
GET  /v1/businessAccount/payouts
```

## Webhooks
```
POST   /v1/notifications/subscriptions
GET    /v1/notifications/subscriptions
DELETE /v1/notifications/subscriptions/{id}
```

## Usage Rules

- Circle MCP: explore API + sandbox test before code
- Circle Skills: generate integration code
- No hardcoded API keys — use User Secrets / env vars
- Store: CirclePaymentIntentId, CirclePayoutId, CircleEventId for idempotency