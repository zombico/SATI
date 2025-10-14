#!/bin/bash
#set -e

echo "=== RunPod Environment Check ==="
echo "GPU Info:"
#nvidia-smi || echo "WARNING: nvidia-smi failed"

echo ""
echo "CUDA Environment:"
env | grep -E 'CUDA|NVIDIA|LD_LIBRARY' || true

echo ""
echo "=== Starting Ollama Server ==="
export OLLAMA_HOST=0.0.0.0:11434
export OLLAMA_ORIGINS=*

# Force GPU usage and optimize performance
export OLLAMA_DEBUG=1
export CUDA_VISIBLE_DEVICES=0
export OLLAMA_LLM_LIBRARY=cuda_v12
export OLLAMA_KEEP_ALIVE=-1  # Keep model in VRAM permanently
export OLLAMA_MAX_LOADED_MODELS=1  # Only cache one model
export OLLAMA_NUM_PARALLEL=4  # Handle multiple concurrent requests

# Ensure CUDA libraries are visible
export LD_LIBRARY_PATH=/usr/local/nvidia/lib:/usr/local/nvidia/lib64:${LD_LIBRARY_PATH}
export PATH=/usr/local/nvidia/bin:${PATH}

# Start Ollama with explicit GPU settings
ollama serve > /tmp/ollama.log 2>&1 &
OLLAMA_PID=$!
echo "Ollama PID: $OLLAMA_PID"

# Wait for Ollama to be ready
echo "Waiting for Ollama to start..."
MAX_RETRIES=60
RETRY_COUNT=0

until curl -sf http://localhost:11434/api/tags > /dev/null 2>&1; do
  RETRY_COUNT=$((RETRY_COUNT + 1))
  if [ $RETRY_COUNT -ge $MAX_RETRIES ]; then
    echo "ERROR: Ollama failed to start after $MAX_RETRIES attempts"
    echo "=== Ollama Logs ==="
    cat /tmp/ollama.log
    exit 1
  fi
  
  if [ $((RETRY_COUNT % 10)) -eq 0 ]; then
    echo "Still waiting... ($RETRY_COUNT/$MAX_RETRIES)"
    echo "Recent logs:"
    tail -5 /tmp/ollama.log 2>/dev/null || true
  fi
  
  sleep 1
done

echo ""
echo "✓ Ollama is ready!"

# Pull the model
echo ""
echo "=== Pulling Mistral Model ==="
ollama rm mistral 2>/dev/null || true
ollama pull mistral

# Pre-load model into GPU memory (keeps it warm)
echo ""
echo "=== Pre-loading Mistral to GPU ==="
curl -s http://localhost:11434/api/generate -d '{
  "model": "mistral",
  "prompt": "Initialize",
  "stream": false
}' > /dev/null

echo "✓ Model loaded and ready in VRAM"

# Verify GPU is being used
echo ""
echo "=== Verifying GPU Usage ==="
sleep 2
#nvidia-smi

echo ""
echo "=== Ollama Performance Info ==="
echo "Looking for eval rate (tokens/sec)..."
grep -i "eval rate" /tmp/ollama.log | tail -1 || echo "No eval rate found yet"

echo ""
echo "=== Starting Node.js Middleware ==="
cd /app

# Get context name from environment or default to 'incometax'
CONTEXT_NAME=${CONTEXT_NAME:-incometax}
echo "Using context: $CONTEXT_NAME"

# Keep the container running and tail logs
node server.js $CONTEXT_NAME &
NODE_PID=$!

echo ""
echo "=== Services Started ==="
echo "Ollama PID: $OLLAMA_PID"
echo "Node PID: $NODE_PID"
echo "Ollama endpoint: http://0.0.0.0:11434"
echo "Middleware endpoint: http://0.0.0.0:3000"
echo ""
echo "✓ Ready for requests - model is warm in VRAM!"

# Keep container alive and show both logs
tail -f /tmp/ollama.log &
wait $NODE_PID