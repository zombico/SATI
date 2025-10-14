FROM ollama/ollama:latest

# Install Node.js and utilities (removed nvidia-cuda-toolkit - not needed)
RUN apt-get update && apt-get install -y \
    nodejs npm curl wget \
    && rm -rf /var/lib/apt/lists/*

# The ollama base image already has CUDA runtime

# Set working directory
WORKDIR /app

# Copy package files from Middleware
COPY package*.json ./
RUN npm install

# Copy application files
COPY server.js ./
COPY rag.js ./
COPY client/ ./client/
COPY config/ ./config/
COPY documents/ ./documents/

# Copy start script
COPY start.sh /start.sh
RUN chmod +x /start.sh

# Pre-pull the model to speed up startup (optional but recommended)
# RUN ollama serve & sleep 5 && ollama pull mistral && pkill ollama

# RunPod expects these ports
EXPOSE 3000 11434 8000

# Critical: GPU environment variables for RunPod
ENV NVIDIA_VISIBLE_DEVICES=all
ENV NVIDIA_DRIVER_CAPABILITIES=compute,utility
ENV LD_LIBRARY_PATH=/usr/local/nvidia/lib:/usr/local/nvidia/lib64

# Ollama GPU settings
ENV OLLAMA_HOST=0.0.0.0:11434
ENV OLLAMA_ORIGINS=*

# Default context (can be overridden in RunPod)
ENV CONTEXT_NAME=elections

# Override the ollama entrypoint completely
ENTRYPOINT []
CMD ["/bin/bash", "/start.sh"]