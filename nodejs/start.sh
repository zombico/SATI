#!/bin/bash
set -e

echo "Starting Ollama server..."
ollama serve &
OLLAMA_PID=$!

# Wait for Ollama to be ready
echo "Waiting for Ollama..."
until curl -sf http://localhost:11434/api/tags > /dev/null 2>&1; do
  sleep 1
done
echo "✓ Ollama ready"

# Pull model
echo "Pulling mistral model..."
ollama pull mistral
echo "✓ Model ready"

# Start Node.js server
echo "Starting Node.js server..."
cd /app
CONTEXT_NAME=${CONTEXT_NAME:-elections}
node server.js $CONTEXT_NAME &
NODE_PID=$!

echo ""
echo "✓ Services running:"
echo "  Ollama: http://0.0.0.0:11434"
echo "  Server: http://0.0.0.0:3000"
echo ""

# Keep container alive
wait $NODE_PID
