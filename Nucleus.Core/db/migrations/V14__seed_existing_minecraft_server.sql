-- Seed the existing 'mc' container that was previously managed by docker-compose.
-- This migration transfers management of the container to Nucleus.
--
-- IMPORTANT: After running this migration, you must:
-- 1. Update the persistence_location to the absolute path to your data directory
-- 2. Update the rcon_password to match your MinecraftRconPassword environment variable
-- 3. Optionally update the curseforge_page_url if it has changed

INSERT INTO minecraft_server (
    name,
    owner_id,
    persistence_location,
    container_name,
    cpu_reservation,
    ram_reservation,
    cpu_limit,
    ram_limit,
    server_type,
    minecraft_version,
    curseforge_page_url,
    rcon_password,
    max_players,
    motd,
    is_active
)
SELECT
    'Main Server',
    (SELECT id FROM discord_user where username = 'pluscosmic'),
    '/data/minecraft',  -- UPDATE THIS to your actual data path
    'mc-atm10',
    4.0,    -- CPU reservation (cores)
    10240,   -- RAM reservation (MB)
    6.0,    -- CPU limit (cores)
    12288,  -- RAM limit (MB) - matches MEMORY=10240M
    'curseforge',
    '1.21.1',
    'https://www.curseforge.com/minecraft/modpacks/all-the-mods-10/files/7223056',
    'CHANGE_ME',  -- UPDATE THIS to your actual RCON password
    5,
    'When the bank balance grows we get sweaty toes',
    TRUE
WHERE EXISTS (SELECT 1 FROM discord_user);
-- Only insert if at least one discord_user exists (requires a logged-in user)
