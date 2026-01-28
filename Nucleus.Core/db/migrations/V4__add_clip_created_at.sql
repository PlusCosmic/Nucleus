-- Add created_at timestamp to clip table
ALTER TABLE clip ADD COLUMN IF NOT EXISTS created_at timestamp with time zone NOT NULL DEFAULT now();
