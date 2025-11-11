CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
)
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20251111165032_Initial') THEN
    CREATE TABLE outbox (
        id bigint GENERATED ALWAYS AS IDENTITY,
        topic character varying(255) NOT NULL,
        key bytea,
        value bytea,
        headers jsonb,
        status smallint NOT NULL DEFAULT 0,
        instance_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone
    ) PARTITION BY RANGE (created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20251111165032_Initial') THEN
    CREATE INDEX ix__outbox__id ON outbox (id) WHERE status in (0, 1);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20251111165032_Initial') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20251111165032_Initial', '9.0.11');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20251111165145_AddPartitions') THEN
    CREATE TABLE outbox_202511 PARTITION OF outbox FOR VALUES FROM ('2025-10-01') TO ('2025-11-01');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20251111165145_AddPartitions') THEN
    CREATE TABLE outbox_202512 PARTITION OF outbox FOR VALUES FROM ('2025-11-01') TO ('2025-12-01');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20251111165145_AddPartitions') THEN
    CREATE TABLE outbox_202601 PARTITION OF outbox FOR VALUES FROM ('2025-12-01') TO ('2026-01-01');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20251111165145_AddPartitions') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20251111165145_AddPartitions', '9.0.11');
    END IF;
END $EF$;
COMMIT;

