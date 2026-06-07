-- Enforce idempotency at the DB level: one ledger entry per Circle resource ID.
-- Partial index excludes legacy rows with empty reference_id (schema default).
CREATE UNIQUE INDEX uq_ledger_reference_id
    ON ledger_entries(reference_id)
    WHERE reference_id != '';
