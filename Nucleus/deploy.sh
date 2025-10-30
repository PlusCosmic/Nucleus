#!/bin/bash
set -e

echo "======================================"
echo "Zero-Downtime Deployment Script"
echo "======================================"

# Load environment variables
if [ -f .env ]; then
    export $(cat .env | grep -v '^#' | xargs)
fi

IMAGE="nucleus:latest"
CONNECTION_STRING="Host=postgres;Port=5432;Database=nucleus_db;Username=nucleus_user;Password=${POSTGRES_PASSWORD}"

# Build new image
echo "Building new Docker image..."
docker compose build nucleus

# Run migrations
echo "Running database migrations..."
if ! docker run --rm \
  --network nucleus_app-network \
  --entrypoint ./efbundle \
  -e POSTGRES_PASSWORD="$POSTGRES_PASSWORD" \
  $IMAGE \
  --connection "$CONNECTION_STRING"; then
    echo "ERROR: Migration failed! Aborting deployment."
    exit 1
fi

echo "Migrations completed successfully!"

# Get current running container
CURRENT_CONTAINER=$(docker ps --filter "name=nucleus" --filter "status=running" --format "{{.Names}}" | grep -v migrate | head -1)

if [ -z "$CURRENT_CONTAINER" ]; then
    echo "No running container found. Starting fresh..."
    docker compose up -d nucleus
    echo "Deployment complete!"
    exit 0
fi

echo "Current container: $CURRENT_CONTAINER"

# Start new container alongside old one
echo "Starting new container..."
docker compose up -d --no-deps --scale nucleus=2 nucleus

# Wait for new container to be healthy
echo "Waiting for new container to be healthy..."
MAX_ATTEMPTS=30
ATTEMPT=0

while [ $ATTEMPT -lt $MAX_ATTEMPTS ]; do
    NEW_CONTAINER=$(docker ps --filter "name=nucleus" --filter "status=running" --format "{{.Names}}" | grep -v migrate | grep -v "$CURRENT_CONTAINER" | head -1)
    
    if [ ! -z "$NEW_CONTAINER" ]; then
        HEALTH=$(docker inspect --format='{{.State.Health.Status}}' $NEW_CONTAINER 2>/dev/null || echo "none")
        
        if [ "$HEALTH" == "healthy" ]; then
            echo "New container is healthy!"
            break
        fi
        
        echo "Attempt $((ATTEMPT+1))/$MAX_ATTEMPTS - Health status: $HEALTH"
    fi
    
    sleep 2
    ATTEMPT=$((ATTEMPT+1))
done

if [ $ATTEMPT -eq $MAX_ATTEMPTS ]; then
    echo "ERROR: New container failed to become healthy. Rolling back..."
    docker stop $NEW_CONTAINER 2>/dev/null || true
    docker rm $NEW_CONTAINER 2>/dev/null || true
    docker compose up -d --no-deps --scale nucleus=1 nucleus
    exit 1
fi

# Stop old container
echo "Stopping old container: $CURRENT_CONTAINER"
docker stop $CURRENT_CONTAINER
docker rm $CURRENT_CONTAINER

# Scale back to 1
echo "Scaling back to 1 instance..."
docker compose up -d --no-deps --scale nucleus=1 nucleus

# Cleanup
echo "Cleaning up old images..."
docker image prune -f

echo "======================================"
echo "Deployment completed successfully!"
echo "======================================"