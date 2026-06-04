INSERT INTO customers (id, name, email, customer_type) VALUES
    ('a0000000-0000-0000-0000-000000000001', 'Alice Circle', 'alice@example.com', 'Circle'),
    ('a0000000-0000-0000-0000-000000000002', 'Bob Wallet', 'bob@example.com', 'ExternalWallet');

INSERT INTO funding_accounts (id, customer_id, currency) VALUES
    ('b0000000-0000-0000-0000-000000000001', 'a0000000-0000-0000-0000-000000000001', 'USDC'),
    ('b0000000-0000-0000-0000-000000000002', 'a0000000-0000-0000-0000-000000000002', 'USDC');
