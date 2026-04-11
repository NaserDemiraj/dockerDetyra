#!/bin/bash

# Build Backend
echo "📦 Building Backend..."
cd backend
dotnet build -c Release
cd ..

# Build Frontend
echo "📦 Building Frontend..."
cd frontend
npm install
npm run build
cd ..

# Build Worker
echo "📦 Building Worker Image..."
cd worker
docker build -t codelab-worker:latest .
cd ..

echo "✅ All builds completed!"
