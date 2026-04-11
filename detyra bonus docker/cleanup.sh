#!/bin/bash

# Cleanup and remove all containers, images related to CodeLab
echo "🧹 Cleaning up CodeLab containers and images..."

docker compose down -v
docker rmi codelab-worker:latest
docker rmi codelab-backend:latest
docker rmi codelab-frontend:latest

echo "✅ Cleanup complete!"
