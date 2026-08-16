CREATE SCHEMA IF NOT EXISTS banking;

CREATE TABLE IF NOT EXISTS banking.bank_account
(
    id                  uuid PRIMARY KEY,
    tenant_id           integer NOT NULL,
    owner_id            uuid NOT NULL,
    balance             numeric(19,4) NOT NULL,
    pending_transactions numeric(19,4) NOT NULL DEFAULT 0,
    regulatory_hold     numeric(19,4) NOT NULL DEFAULT 0,
    daily_transferred   numeric(19,4) NOT NULL DEFAULT 0,
    daily_limit         numeric(19,4) NOT NULL,
    is_frozen           boolean NOT NULL DEFAULT false,
    CONSTRAINT ck_bank_account_amounts CHECK (balance >= 0 AND pending_transactions >= 0 AND regulatory_hold >= 0 AND daily_transferred >= 0 AND daily_limit >= 0)
);

CREATE TABLE IF NOT EXISTS banking.transfer_idempotency
(
    idempotency_key       text PRIMARY KEY,
    actor_id              uuid NOT NULL,
    tenant_id             integer NOT NULL,
    source_account_id     uuid NOT NULL,
    destination_account_id uuid NOT NULL,
    amount                numeric(19,4) NOT NULL,
    transfer_id           uuid NOT NULL UNIQUE,
    source_balance        numeric(19,4) NOT NULL,
    destination_balance   numeric(19,4) NOT NULL,
    created_at            timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS banking.transfer_audit
(
    id                    bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    transfer_id           uuid NOT NULL,
    action                text NOT NULL,
    actor_id              uuid NOT NULL,
    tenant_id             integer NOT NULL,
    source_account_id     uuid NOT NULL,
    destination_account_id uuid NOT NULL,
    amount                numeric(19,4) NOT NULL,
    created_at            timestamptz NOT NULL DEFAULT now()
);
