-- Add columns needed for dynamic container provisioning

ALTER TABLE minecraft_server
    ADD COLUMN rcon_password TEXT,
    ADD COLUMN max_players INTEGER NOT NULL DEFAULT 20,
    ADD COLUMN motd TEXT NOT NULL DEFAULT 'A Minecraft Server';

-- Add comment for documentation
COMMENT ON COLUMN minecraft_server.rcon_password IS 'RCON password set during container provisioning. NULL for legacy containers that read from server.properties.';
