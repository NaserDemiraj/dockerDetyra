#!/bin/bash

# Build the worker container image
docker build -t codelab-worker:latest .

echo "✓ Worker image built successfully!"
echo "Usage: docker run -d --name <container-name> -m 512m --cpus 0.5 codelab-worker sleep 3600"
