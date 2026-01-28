-- Add video metadata columns to clip table to avoid fetching all videos from Bunny CDN
-- This improves performance by storing essential video data in PostgreSQL

ALTER TABLE clip ADD COLUMN IF NOT EXISTS title text;
ALTER TABLE clip ADD COLUMN IF NOT EXISTS length integer; -- duration in seconds
ALTER TABLE clip ADD COLUMN IF NOT EXISTS thumbnail_file_name text;
ALTER TABLE clip ADD COLUMN IF NOT EXISTS date_uploaded timestamp with time zone;
ALTER TABLE clip ADD COLUMN IF NOT EXISTS storage_size bigint;
ALTER TABLE clip ADD COLUMN IF NOT EXISTS video_status integer; -- Bunny encoding status
ALTER TABLE clip ADD COLUMN IF NOT EXISTS encode_progress integer; -- 0-100
