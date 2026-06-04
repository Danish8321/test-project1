CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE TYPE customer_type AS ENUM ('Circle', 'ExternalWallet');
CREATE TYPE deposit_status AS ENUM ('Pending', 'Completed', 'Failed', 'Cancelled');
CREATE TYPE withdrawal_status AS ENUM ('Pending', 'Completed', 'Failed');
CREATE TYPE entry_type AS ENUM ('Credit', 'Debit');
CREATE TYPE webhook_status AS ENUM ('Received', 'Processed', 'Failed', 'Duplicate');

CREATE TABLE customers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    email VARCHAR(255) NOT NULL UNIQUE,
    customer_type customer_type NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE funding_accounts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id UUID NOT NULL REFERENCES customers(id),
    currency VARCHAR(10) NOT NULL DEFAULT 'USDC',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE deposits (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id UUID NOT NULL REFERENCES customers(id),
    funding_account_id UUID NOT NULL REFERENCES funding_accounts(id),
    circle_payment_intent_id VARCHAR(255) NOT NULL UNIQUE,
    amount NUMERIC(18,6) NOT NULL,
    status deposit_status NOT NULL DEFAULT 'Pending',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE withdrawals (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id UUID NOT NULL REFERENCES customers(id),
    funding_account_id UUID NOT NULL REFERENCES funding_accounts(id),
    circle_payout_id VARCHAR(255) NOT NULL UNIQUE,
    amount NUMERIC(18,6) NOT NULL,
    status withdrawal_status NOT NULL DEFAULT 'Pending',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE ledger_entries (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    funding_account_id UUID NOT NULL REFERENCES funding_accounts(id),
    entry_type entry_type NOT NULL,
    amount NUMERIC(18,6) NOT NULL,
    reference_id VARCHAR(255) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE webhook_events (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    circle_event_id VARCHAR(255) NOT NULL UNIQUE,
    event_type VARCHAR(100) NOT NULL,
    payload JSONB NOT NULL,
    status webhook_status NOT NULL DEFAULT 'Received',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    processed_at TIMESTAMPTZ
);

CREATE TABLE reconciliation_records (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    circle_reference_id VARCHAR(255) NOT NULL,
    record_type VARCHAR(50) NOT NULL,
    amount NUMERIC(18,6) NOT NULL,
    status VARCHAR(50) NOT NULL,
    mismatch_reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
