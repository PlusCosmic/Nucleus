CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251030141246_InitialCreate') THEN
    CREATE TABLE discord_user (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        discord_id text NOT NULL,
        username text NOT NULL,
        CONSTRAINT discord_user_pkey PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251030141246_InitialCreate') THEN
    CREATE TABLE user_frequent_link (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid NOT NULL,
        title text NOT NULL,
        url text NOT NULL,
        thumbnail_url text,
        CONSTRAINT user_frequent_link_pkey PRIMARY KEY (id),
        CONSTRAINT fk_user_frequent_link__user FOREIGN KEY (user_id) REFERENCES discord_user (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251030141246_InitialCreate') THEN
    CREATE UNIQUE INDEX uq_discord_user_discord_id ON discord_user (discord_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251030141246_InitialCreate') THEN
    CREATE INDEX ix_user_frequent_link__user_id ON user_frequent_link (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251030141246_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251030141246_InitialCreate', '10.0.0-rc.2.25502.107');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251030221051_InitialClips') THEN
    CREATE TABLE clip (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        owner_id uuid NOT NULL,
        video_id uuid NOT NULL,
        category integer NOT NULL,
        CONSTRAINT clip_pkey PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251030221051_InitialClips') THEN
    CREATE TABLE clip_collection (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        collection_id uuid NOT NULL,
        owner_id uuid NOT NULL,
        category integer NOT NULL,
        CONSTRAINT clip_collection_pkey PRIMARY KEY (id),
        CONSTRAINT fk_clip_collection__discord_user FOREIGN KEY (owner_id) REFERENCES discord_user (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251030221051_InitialClips') THEN
    CREATE INDEX ix_clip_collection__owner_id ON clip_collection (owner_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251030221051_InitialClips') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251030221051_InitialClips', '10.0.0-rc.2.25502.107');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251031185808_ApexMapRotations') THEN
    CREATE TABLE apex_map_rotation (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        map integer NOT NULL,
        start_time timestamp with time zone NOT NULL,
        end_time timestamp with time zone NOT NULL,
        gamemode integer NOT NULL,
        CONSTRAINT apex_map_rotation_pkey PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251031185808_ApexMapRotations') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251031185808_ApexMapRotations', '10.0.0-rc.2.25502.107');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251101153451_Avatar') THEN
    ALTER TABLE discord_user ALTER COLUMN username TYPE character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251101153451_Avatar') THEN
    ALTER TABLE discord_user ALTER COLUMN discord_id TYPE character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251101153451_Avatar') THEN
    ALTER TABLE discord_user ADD avatar character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251101153451_Avatar') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251101153451_Avatar', '10.0.0-rc.2.25502.107');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251101194604_Tags') THEN
    -- Column already exists as 'avatar' from previous migration, no rename needed
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251101194604_Tags') THEN
    CREATE TABLE tag (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        name text NOT NULL,
        CONSTRAINT tag_pkey PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251101194604_Tags') THEN
    CREATE TABLE clip_tag (
        clip_id uuid NOT NULL,
        tag_id uuid NOT NULL,
        CONSTRAINT clip_tag_pkey PRIMARY KEY (clip_id, tag_id),
        CONSTRAINT fk_clip_tag__clip FOREIGN KEY (clip_id) REFERENCES clip (id) ON DELETE CASCADE,
        CONSTRAINT fk_clip_tag__tag FOREIGN KEY (tag_id) REFERENCES tag (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251101194604_Tags') THEN
    CREATE INDEX ix_clip_tag__tag_id ON clip_tag (tag_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251101194604_Tags') THEN
    CREATE UNIQUE INDEX uq_tag__name ON tag (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251101194604_Tags') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251101194604_Tags', '10.0.0-rc.2.25502.107');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251101215537_ClipViews') THEN
    CREATE TABLE clip_view (
        user_id uuid NOT NULL,
        clip_id uuid NOT NULL,
        viewed_at timestamp with time zone NOT NULL,
        CONSTRAINT clip_view_pkey PRIMARY KEY (user_id, clip_id),
        CONSTRAINT fk_clip_view__clip FOREIGN KEY (clip_id) REFERENCES clip (id) ON DELETE CASCADE,
        CONSTRAINT fk_clip_view__user FOREIGN KEY (user_id) REFERENCES discord_user (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251101215537_ClipViews') THEN
    CREATE INDEX ix_clip_view__clip_id ON clip_view (clip_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251101215537_ClipViews') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251101215537_ClipViews', '10.0.0-rc.2.25502.107');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251102001238_Md5Hash') THEN
    ALTER TABLE clip ADD md5_hash text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251102001238_Md5Hash') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251102001238_Md5Hash', '10.0.0-rc.2.25502.107');
    END IF;
END $EF$;

