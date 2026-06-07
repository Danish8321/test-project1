ALTER TABLE deposits
    ADD COLUMN circle_payment_id  TEXT,
    ADD COLUMN deposit_address    TEXT,
    ADD COLUMN chain              TEXT,
    ADD COLUMN expires_on         TIMESTAMPTZ,
    ADD COLUMN transaction_hash   TEXT;

CREATE INDEX idx_deposits_circle_payment_id ON deposits(circle_payment_id);
