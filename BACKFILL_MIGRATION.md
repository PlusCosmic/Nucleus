# Clip Metadata Backfill Migration

## Overview

This one-off migration populates missing Bunny CDN video metadata for existing clips in the database. New clips automatically populate this data during creation, but older clips may have NULL values in these fields:

- `title` - Video title
- `length` - Duration in seconds
- `thumbnail_file_name` - Thumbnail filename for display
- `date_uploaded` - When uploaded to Bunny CDN
- `storage_size` - File size in bytes
- `video_status` - Encoding status (0=new, 1=uploading, 2=processing, 3=ready, 4=error)
- `encode_progress` - Encoding progress 0-100

## How It Works

The migration:
1. Queries the database for clips with NULL metadata fields
2. Calls the Bunny CDN API for each `video_id` to fetch current metadata
3. Updates the clip record with the fetched data
4. Handles errors gracefully (logs warnings if videos no longer exist in Bunny)

## Running the Migration

### Option 1: Via HTTP Endpoint (Recommended)

Start the application and make a POST request to the backfill endpoint:

```bash
# Run with default batch size (100 clips)
curl -X POST "https://your-backend.com/clips/backfill-metadata" \
  --cookie "your-auth-cookie"

# Or specify a custom batch size
curl -X POST "https://your-backend.com/clips/backfill-metadata?batchSize=50" \
  --cookie "your-auth-cookie"
```

**Response:**
```json
{
  "total_processed": 100,
  "success_count": 98,
  "failure_count": 2
}
```

### Option 2: Multiple Runs for Large Datasets

If you have many clips needing backfill, run the endpoint multiple times until all clips are processed:

```bash
# Run until no more clips need backfilling
while true; do
  response=$(curl -s -X POST "https://your-backend.com/clips/backfill-metadata" \
    --cookie "your-auth-cookie")

  total=$(echo $response | jq '.total_processed')

  if [ "$total" -eq 0 ]; then
    echo "Backfill complete!"
    break
  fi

  echo "Processed $total clips, continuing..."
  sleep 1
done
```

## Implementation Details

### Files Created

1. **Nucleus/Clips/ClipsBackfillStatements.cs** - Dapper queries for finding and updating clips
2. **Nucleus/Clips/ClipsBackfillService.cs** - Business logic for the backfill process
3. **Nucleus/Clips/ClipsEndpoints.cs** - Added `/clips/backfill-metadata` endpoint
4. **Nucleus/Program.cs** - Registered `ClipsBackfillStatements` and `ClipsBackfillService`

### Endpoint Details

- **Route:** `POST /clips/backfill-metadata`
- **Auth:** Requires authentication (uses whitelist middleware)
- **Parameters:**
  - `batchSize` (optional, default: 100) - Number of clips to process per request
- **Returns:** `BackfillResult` with counts of processed, successful, and failed clips

### Database Query

Finds clips needing backfill:
```sql
SELECT id, video_id
FROM clip
WHERE title IS NULL OR length IS NULL OR thumbnail_file_name IS NULL
ORDER BY created_at ASC
LIMIT @Limit
```

### Error Handling

- **Video Not Found:** Logs a warning if a video no longer exists in Bunny CDN (doesn't halt migration)
- **API Errors:** Catches and logs exceptions for individual clips, continues processing remaining clips
- **Transaction Safety:** Each clip update is independent - failures don't rollback successful updates

## Logs

The service logs progress at INFO level:

```
info: Nucleus.Clips.ClipsBackfillService[0]
      Found 100 clips needing backfill
info: Nucleus.Clips.ClipsBackfillService[0]
      Backfilled clip a1b2c3d4-... with video e5f6g7h8-...
warn: Nucleus.Clips.ClipsBackfillService[0]
      Video e5f6g7h8-... for clip a1b2c3d4-... not found in Bunny CDN
info: Nucleus.Clips.ClipsBackfillService[0]
      Backfill completed: 98 succeeded, 2 failed
```

## Performance Considerations

- **Batch Size:** Default is 100 clips per request to balance API rate limits and processing time
- **Rate Limiting:** Consider Bunny CDN API rate limits when processing large batches
- **Incremental Processing:** The migration can be safely run multiple times until completion

## Verification

After running the migration, verify completion:

```sql
-- Check how many clips still need backfilling
SELECT COUNT(*)
FROM clip
WHERE title IS NULL OR length IS NULL OR thumbnail_file_name IS NULL;
```

Should return `0` when complete.

## Future Clips

This migration is a one-off fix for existing data. All newly created clips will automatically populate these fields during creation via `ClipService.CreateClip()`.
