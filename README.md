# SATI
## A REST-inspired pattern for AI - For Developers Everywhere

**Observable AI conversation. Fully headless. No API keys. No training. No fine-tuning. Just run it.**

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

That's it. Your chatbot will be running on `http://localhost:3000` for both NodeJs AND .NET

## What You Get

- ✅ **Build faster than ever** - No training, no fine-tuning, no waiting
- ✅ **Harness your local model** - Runs on your machine via Ollama/Mistral
- ✅ **Tamper-proof logs** - Every conversation cryptographically chained
- ✅ **RAG-ready** - Drop documents in, get context-aware responses
- ✅ **Verifiable** - Built-in integrity checking
- ✅ **Sovereign** - No API dependency, no internet required

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
- .NET 8.0 SDK or later
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

## Learn More

- [Architecture & Pattern Details](./docs/pattern.md)
- [NodeJs Implementation](./nodejs)
- [Dotnet Implementation](./dotnet)

## Caveats

- Bona fide intent is necessary to get useful outputs
- Performance is subject to hardware
- Local model capability and inference may vary
- Not fully hardened against Prompt Injection
- Current state shared here is a reference implementation - not hardened for production use

## Support
This repo is to demonstrate the pattern and educate how to use it.
Its sole purpose is to point out the way.
This is NOT a framework that will be maintained.
Do what you will. Fork it, clone it, and customize to your use case.
Make it better. Make it work. 

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
---

