FROM python:3.11-slim

# Set working directory
WORKDIR /app

# Install dependencies
RUN pip install --no-cache-dir numpy pandas requests

# Copy executor
COPY executor.py /app/

# Set resource limits (enforced by Docker at container level)
# Memory: 512MB, CPU: 0.5 cores (set via docker run)

# Run bash to keep container alive
CMD ["/bin/bash"]
