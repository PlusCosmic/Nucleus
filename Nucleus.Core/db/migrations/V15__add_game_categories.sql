-- Create the new game_category table
CREATE TABLE game_category (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    igdb_id INTEGER UNIQUE,                          -- IGDB game ID (NULL for custom categories)
    name TEXT NOT NULL,                              -- Display name
    slug TEXT NOT NULL UNIQUE,                       -- URL-friendly identifier
    cover_url TEXT,                                  -- Cover art URL from IGDB or user-provided
    is_custom BOOLEAN NOT NULL DEFAULT FALSE,        -- True for non-game categories
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_game_category_slug ON game_category(slug);
CREATE INDEX idx_game_category_igdb_id ON game_category(igdb_id);

-- Seed existing categories (let database auto-generate UUIDs)
INSERT INTO game_category (igdb_id, name, slug, cover_url, is_custom) VALUES
    (114795, 'Apex Legends', 'apex-legends',
     'https://images.igdb.com/igdb/image/upload/t_cover_big/co1wj6.jpg', FALSE),
    (131800, 'Call of Duty: Warzone', 'warzone',
     'https://images.igdb.com/igdb/image/upload/t_cover_big/co2k2r.jpg', FALSE),
    (NULL, 'Snowboarding', 'snowboarding',
     '/images/snowboarding.png', TRUE);

-- Add foreign key column to clip table
ALTER TABLE clip ADD COLUMN game_category_id UUID;

-- Migrate existing data from integer category to UUID (lookup by slug)
UPDATE clip SET game_category_id =
    CASE category
        WHEN 0 THEN (SELECT id FROM game_category WHERE slug = 'apex-legends')
        WHEN 1 THEN (SELECT id FROM game_category WHERE slug = 'warzone')
        WHEN 2 THEN (SELECT id FROM game_category WHERE slug = 'snowboarding')
    END;

-- Make NOT NULL and add constraint
ALTER TABLE clip ALTER COLUMN game_category_id SET NOT NULL;
ALTER TABLE clip ADD CONSTRAINT fk_clip_game_category
    FOREIGN KEY (game_category_id) REFERENCES game_category(id);

-- Drop legacy column
ALTER TABLE clip DROP COLUMN category;

-- Similarly update clip_collection
ALTER TABLE clip_collection ADD COLUMN game_category_id UUID;

UPDATE clip_collection SET game_category_id =
    CASE category
        WHEN 0 THEN (SELECT id FROM game_category WHERE slug = 'apex-legends')
        WHEN 1 THEN (SELECT id FROM game_category WHERE slug = 'warzone')
        WHEN 2 THEN (SELECT id FROM game_category WHERE slug = 'snowboarding')
    END;

ALTER TABLE clip_collection ALTER COLUMN game_category_id SET NOT NULL;
ALTER TABLE clip_collection ADD CONSTRAINT fk_clip_collection_game_category
    FOREIGN KEY (game_category_id) REFERENCES game_category(id);
ALTER TABLE clip_collection DROP COLUMN category;

-- Create user_game_category for per-user category subscriptions
-- Categories are global, but users subscribe to the ones they want
CREATE TABLE user_game_category (
    user_id UUID NOT NULL REFERENCES discord_user(id) ON DELETE CASCADE,
    game_category_id UUID NOT NULL REFERENCES game_category(id) ON DELETE CASCADE,
    added_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (user_id, game_category_id)
);

-- Seed existing user-category relationships based on existing clips
INSERT INTO user_game_category (user_id, game_category_id)
SELECT DISTINCT owner_id, game_category_id FROM clip
ON CONFLICT DO NOTHING;
