-- Add global_name column to discord_user table
ALTER TABLE discord_user ADD COLUMN IF NOT EXISTS global_name character varying(100);
