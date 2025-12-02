-- Create Minecraft command log table
CREATE TABLE minecraft_command_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES discord_user(id) ON DELETE CASCADE,
    command TEXT NOT NULL,
    response TEXT,
    success BOOLEAN NOT NULL,
    error TEXT,
    executed_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_minecraft_command_log_user_id ON minecraft_command_log(user_id);
CREATE INDEX idx_minecraft_command_log_executed_at ON minecraft_command_log(executed_at DESC);

-- Create Minecraft file operation log table
CREATE TABLE minecraft_file_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES discord_user(id) ON DELETE CASCADE,
    operation VARCHAR(50) NOT NULL,
    file_path TEXT NOT NULL,
    success BOOLEAN NOT NULL,
    error TEXT,
    executed_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_minecraft_file_log_user_id ON minecraft_file_log(user_id);
CREATE INDEX idx_minecraft_file_log_executed_at ON minecraft_file_log(executed_at DESC);
