-- Schema for the Postgres-backed stores.
-- Applied automatically by docker-compose (mounted into /docker-entrypoint-initdb.d).

create table if not exists exchange_rates (
    from_ccy    char(3)        not null,
    to_ccy      char(3)        not null,
    rate        numeric(20, 8) not null check (rate > 0),
    as_of       timestamptz    not null,
    primary key (from_ccy, to_ccy, as_of)
);

create table if not exists quotes (
    id              uuid primary key,
    corridor        varchar(7)     not null,
    send_amount     numeric(18, 3) not null,
    send_ccy        char(3)        not null,
    fee             numeric(18, 3) not null,
    receive_amount  numeric(18, 3) not null,
    receive_ccy     char(3)        not null,
    mid_rate        numeric(20, 8) not null,
    customer_rate   numeric(20, 8) not null,
    created_at      timestamptz    not null,
    expires_at      timestamptz    not null
);

create table if not exists transfers (
    id          uuid primary key,
    quote_id    uuid         not null unique references quotes (id),
    reference   varchar(20)  not null unique,
    sender      jsonb        not null,
    recipient   jsonb        not null,
    status      varchar(20)  not null,
    history     jsonb        not null default '[]',
    created_at  timestamptz  not null
);

create table if not exists idempotency_keys (
    key            varchar(100) primary key,
    request_hash   char(64)     not null,
    status_code    int,
    response_body  text,
    created_at     timestamptz  not null default now()
);
