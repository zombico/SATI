FROM ollama/ollama:latest

# Install Node.js and utilities (removed nvidia-cuda-toolkit - not needed)
RUN apt-get update && apt-get install -y \
    nodejs npm curl wget \
    && rm -rf /var/lib/apt/lists/*

# The ollama base image already has CUDA runtime

# Set working directory
WORKDIR /app

# Copy package files from Middleware
COPY nodejs/package*.json ./
RUN npm install

# Copy application files
COPY nodejs/server.js ./
COPY nodejs/rag.js ./
COPY client/ /client/
COPY config/ /config/
COPY documents/ /documents/

# Copy start script
COPY nodejs/start.sh /start.sh
RUN chmod +x /start.sh

EXPOSE 3000 11434 

# Override the ollama entrypoint completely
ENTRYPOINT []
CMD ["/bin/bash", "/start.sh"]