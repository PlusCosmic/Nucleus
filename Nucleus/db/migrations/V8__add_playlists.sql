CREATE TABLE playlists
(
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name            VARCHAR(255) NOT NULL,
    description     TEXT,
    creator_user_id UUID         NOT NULL REFERENCES discord_user (id),
    created_at      TIMESTAMP        DEFAULT NOW(),
    updated_at      TIMESTAMP        DEFAULT NOW()
);

CREATE TABLE playlist_collaborators
(
    playlist_id      UUID REFERENCES playlists (id) ON DELETE CASCADE,
    user_id          UUID REFERENCES discord_user (id),
    added_at         TIMESTAMP DEFAULT NOW(),
    added_by_user_id UUID REFERENCES discord_user (id),
    PRIMARY KEY (playlist_id, user_id)
);

CREATE TABLE playlist_clips
(
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    playlist_id      UUID REFERENCES playlists (id) ON DELETE CASCADE,
    clip_id          UUID REFERENCES clip (id) ON DELETE CASCADE,
    position         INTEGER NOT NULL, -- for ordering
    added_by_user_id UUID REFERENCES discord_user (id),
    added_at         TIMESTAMP        DEFAULT NOW(),
    UNIQUE (playlist_id, clip_id)      -- prevent duplicate clips in same playlist
);

CREATE INDEX idx_playlists_creator ON playlists (creator_user_id);
CREATE INDEX idx_playlist_collaborators_user ON playlist_collaborators (user_id);
CREATE INDEX idx_playlist_clips_playlist ON playlist_clips (playlist_id);
CREATE INDEX idx_playlist_clips_position ON playlist_clips (playlist_id, position);