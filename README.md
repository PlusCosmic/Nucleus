# Nucleus

A backend API and service layer built with ASP.NET Core, powering various web applications with shared functionality and background services.

## Modules

### Clips
Video clip management with CDN-backed storage and streaming. Supports categorization, tagging, view tracking, and collaborative playlists. Includes HLS-to-MP4 conversion via FFmpeg.

### Links
Bookmark storage with automatic page metadata extraction (titles, favicons).

### Minecraft
Server administration tools including RCON command execution, live console streaming via WebSocket, and file system browsing/editing.

### Apex Legends
Game data integration with background polling for map rotation schedules.

### Dropzone
Temporary file sharing with PIN-based access groups.

## Tech Stack

- **.NET 9.0** with ASP.NET Core Minimal APIs
- **PostgreSQL** with Dapper / **MongoDB** for document storage
- **Discord OAuth** for authentication
- **Bunny CDN** for video hosting
- **FFmpeg** for video processing
- **Docker** for deployment

## Architecture

The codebase follows a modular pattern:
- `*Endpoints.cs` — API route definitions
- `*Service.cs` — Business logic
- `*Statements.cs` — Database queries with raw SQL

## Running Locally

```bash
# Using Docker Compose (recommended)
docker compose up --build

# Or manually with .NET CLI
dotnet run --project Nucleus/Nucleus.csproj
```

Requires PostgreSQL and appropriate environment configuration.