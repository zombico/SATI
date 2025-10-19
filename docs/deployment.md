# Deployment Considerations

### For Cloud APIs (OpenAI/Anthropic/Azure)

**No special requirements.** Standard HTTP client with your API key.

SATI's observability and audit layer work identically regardless of backend.

### For Local Models (Ollama/LM Studio/vLLM)
**GPU Requirements**: Local LLMs need GPU acceleration or unified memory for acceptable performance.
**Known Working Platforms**:
- ✅ M-series Macs (unified memory)
- ✅ Runpod GPU instances
- ✅ Local machines with NVIDIA GPUs

**Will Be Slow**:
- ⚠️ Standard cloud VMs without GPU
- ⚠️ Docker without GPU passthrough
- ⚠️ CPU-only deployments

### Built with
Macbook M1 64GB Unified memory - fast enough for Mistral, but still too slow for Llama.

### Caveats
I was only able to validate and deploy after many attempts and through trial and error. Getting the CUDA settings to work on Docker was non-trivial. Finding the appropriate template/hardware combo on Runpod equally so.


