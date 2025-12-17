-- Create enum type for Minecraft server types
CREATE TYPE minecraft_server_type AS ENUM ('vanilla', 'curseforge', 'neoforge', 'fabric');

-- Create Minecraft server configuration table
CREATE TABLE minecraft_server (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    owner_id UUID NOT NULL REFERENCES discord_user(id) ON DELETE CASCADE,
    persistence_location TEXT NOT NULL,
    container_name VARCHAR(100) NOT NULL,
    cpu_reservation DECIMAL(4,2) NOT NULL,
    ram_reservation INTEGER NOT NULL,
    cpu_limit DECIMAL(4,2) NOT NULL,
    ram_limit INTEGER NOT NULL,
    server_type minecraft_server_type NOT NULL,
    minecraft_version VARCHAR(20) NOT NULL,
    modloader_version VARCHAR(50),
    curseforge_page_url TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Indexes for common query patterns
CREATE INDEX idx_minecraft_server_owner_id ON minecraft_server(owner_id);
CREATE UNIQUE INDEX uq_minecraft_server_container_name ON minecraft_server(container_name);
CREATE INDEX idx_minecraft_server_is_active ON minecraft_server(is_active) WHERE is_active = TRUE;

-- Add server_id foreign key to existing minecraft log tables for multi-server support
ALTER TABLE minecraft_command_log
    ADD COLUMN server_id UUID REFERENCES minecraft_server(id) ON DELETE CASCADE;

ALTER TABLE minecraft_file_log
    ADD COLUMN server_id UUID REFERENCES minecraft_server(id) ON DELETE CASCADE;

CREATE INDEX idx_minecraft_command_log_server_id ON minecraft_command_log(server_id);
CREATE INDEX idx_minecraft_file_log_server_id ON minecraft_file_log(server_id);
