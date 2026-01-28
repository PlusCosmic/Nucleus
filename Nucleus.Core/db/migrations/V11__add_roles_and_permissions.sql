-- Add role column to discord_user table
-- Default to 'Viewer' for existing users
ALTER TABLE discord_user
    ADD COLUMN role VARCHAR(50) NOT NULL DEFAULT 'Viewer';

-- Create table for additional permissions beyond role defaults
-- This allows granting specific permissions to users without changing their role
CREATE TABLE user_additional_permission (
    user_id UUID NOT NULL REFERENCES discord_user(id) ON DELETE CASCADE,
    permission VARCHAR(100) NOT NULL,
    granted_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    granted_by UUID REFERENCES discord_user(id) ON DELETE SET NULL,
    PRIMARY KEY (user_id, permission)
);

-- Index for looking up permissions by user
CREATE INDEX idx_user_additional_permission_user_id ON user_additional_permission(user_id);
