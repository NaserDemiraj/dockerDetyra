FROM python:3.11-slim

# Set working directory
WORKDIR /app

# Install Python dependencies
RUN pip install --no-cache-dir numpy pandas requests

# Install Mono for C# compilation and execution
RUN apt-get update && \
    apt-get install -y mono-mcs mono-runtime && \
    rm -rf /var/lib/apt/lists/*

# Copy executor
COPY executor.py /app/

# Set resource limits (enforced by Docker at container level)
# Memory: 512MB, CPU: 0.5 cores (set via docker run)

# Run bash to keep container alive
CMD ["/bin/bash"]
