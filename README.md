# SATI
## A REST-inspired AI pattern - For Developers Everywhere

Observable, verifiable AI conversation. Fully headless. Zero API costs. No training. No fine-tuning.

![Screenshot description](docs/headerscreen.png)

## Quick Start - Vanilla JavaScript

```bash
cd nodejs
npm install
node server.js
```
## .NET

```bash
cd dotnet
Open the solution with Visual Studio or Rider and press F5.
```

That's it. Your SATI server will be running on http://localhost:3000 
with the demo UI for both Node.js and .NET  (same port for UI consistency)

## What is SATI
**SATI** (Stateless Audit Trail Inference) is a pattern for building verifiable AI conversations using REST principles. Every conversation turn is cryptographically chained, creating an immutable audit trail.

- [Architecture & Pattern Details](./docs/pattern.md)

## Who SATI Is For

**Web developers building AI features** - You know Express or ASP.NET. You understand REST and databases. AI is now just another resource to provision.

**Compliance-minded teams** - Regulated industries, or anywhere AI accountability matters.

**Rapid prototypers** - Use Node.js/Express to ship AI features in hours, not weeks.

**Enterprise integrations** - Use .NET for production-grade systems with familiar tooling.

**Cost-conscious teams** - Eliminate per-request API charges and unpredictable scaling costs.

**Sovereignty-conscious builders** - No API keys. No vendor lock-in. No internet? No problem. Your infrastructure, your control.


## What You Get

- ✅ **Zero API costs** - No per-request charges, no usage limits, no surprise bills
- ✅ **Build faster than ever** - No training, no fine-tuning, no waiting
- ✅ **Harness your local model** - Runs on your machine via Ollama/Mistral
- ✅ **Demo UI included** - Test and debug your implementation
- ✅ **Tamper-proof logs** - Every conversation cryptographically chained
- ✅ **RAG-ready** - Drop documents in, get context-aware responses
- ✅ **Verifiable** - Built-in integrity checking


## Why SATI?

Most chatbots are black boxes. SATI gives you:
- **Proof of what the AI said** (cryptographic hashes)
- **When it said it** (timestamps)
- **What context it had** (full prompt reconstruction)
- **Unalterable history** (blockchain-style chaining)

Usable in domains where AI accountability matters.

## Prerequisites

**Both versions:**
- [Ollama](https://ollama.ai/) running locally with Mistral model

**Node.js version:**
- Node.js 16+

**.NET version:**
- .NET 9.0 SDK or later
- Ollama with `nomic-embed-text` model for embeddings

## Features

### Cryptographic Chain
Every conversation turn is hashed and linked to the previous turn. Tampering breaks the chain.

### Conversation Persistence
Multiple conversations tracked with unique IDs. Full history reconstruction.

### Document RAG
Drop text files in your documents folder, get contextual answers automatically.

### Verification Endpoint
`GET /verify/:conversationId` - Prove conversation integrity anytime.

## Caveats

- Quality outputs require well-crafted prompts and instructions
- Performance is subject to hardware
- Local model capability and inference may vary
- While resilient, it is not fully hardened against Prompt Injection
- Current state shared here is a reference implementation - not hardened for production use

## Deployment Considerations

**GPU Requirements**: Local LLMs need GPU acceleration or unified memory for acceptable performance.
**Known Working Platforms**:
- ✅ M-series Macs (unified memory)
- ✅ Runpod GPU instances
- ✅ Local machines with NVIDIA GPUs

**Will Be Slow**:
- ⚠️ Standard cloud VMs without GPU
- ⚠️ Docker without GPU passthrough
- ⚠️ CPU-only deployments

## Support

This repo demonstrates the pattern and provides guidance on understanding it.
Its sole purpose is to point out the way.
This is NOT a maintained framework.
Do what you will. Fork it, clone it, and customize to your use case.
Make it better. Make it robust. Ship AI features faster than you thought possible.

## License

MIT


## Technical Stack

### Node.js Implementation
- **Express.js**: API server
- **Axios**: HTTP client with request/response tracing
- **better-sqlite3**: Embedded database for audit trail
- **Local LLM**: Ollama/Mistral for inference
- **Crypto**: Node.js built-in for SHA-256 hashing

### .NET Implementation
- **ASP.NET Core**: Web API
- **Entity Framework Core**: ORM and database management
- **SQLite**: Embedded database for audit trail
- **OllamaSharp**: Local LLM inference
- **System.Security.Cryptography**: SHA-256 hashing
- **Semantic Kernel/Microsoft.SemanticKernel**: LLM orchestration (optional)

### Developed with
M1 Macbook Pro 64GB
