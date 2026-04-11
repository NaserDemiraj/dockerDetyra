<!-- CodeLab - Interactive Code Execution Platform -->

# Project Structure & Setup

This is a full-stack application for executing student code in isolated Docker containers.

## Folder Organization

- **backend/**: ASP.NET Core API with JWT auth, Docker orchestration
- **frontend/**: React web IDE with code editor
- **worker/**: Docker container image for isolated code execution
- **docker/**: Docker Compose configuration
- **docs/**: Documentation (API, Config, Deployment)

## Next Steps

1. Build worker image: `cd worker && docker build -t codelab-worker:latest .`
2. Start services: `docker-compose up -d`
3. Access app: http://localhost:3000
4. Register and test code execution

## Key Technologies

- Backend: ASP.NET Core 8, Entity Framework, JWT
- Frontend: React 18, Axios, CodeMirror
- Database: SQL Server
- Containerization: Docker & Docker Compose
- Languages Supported: Python, C#

## Configuration

See `docs/CONFIGURATION.md` for environment variables and port mappings.
See `README.md` for quick start and architecture overview.
