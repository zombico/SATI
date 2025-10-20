# SATI - Stateless Audit Trail Inference
**The HTTP translation layer for LLMs**

LLMs are stateless. REST is stateless. SATI is the bridge.

## What is SATI

SATI is HTTP middleware that makes LLM conversations work like REST resources:
- **Stateless reconstruction** - no session store, conversations rebuilt from immutable records
- **Tamper-proof history** - blockchain-style chain hashing for integrity
- **Vendor-agnostic** - works with OpenAI, Anthropic, Ollama, Azure, or your own models
- **Injection-resilient** - verified conversation history can't be manipulated

Observe, audit, and control every LLM interaction using the same patterns you use for REST APIs.
**Works with any LLM backend:** OpenAI, Anthropic, Azure OpenAI, Ollama, or your own infrastructure. Switch providers without changing your middleware layer.


![Screenshot description](docs/headerscreen.png)

## Quick Start 

**Prerequisites (both implementations):**
- [Ollama](https://ollama.ai) running locally 
- Mistral model: `ollama pull mistral`

### Node.js Implementation
```bash
cd nodejs
npm install
node server.js
```
### .NET Implementation

```bash
ollama pull nomic-embed-text 
cd dotnet
dotnet run
```
Or open the solution with Visual Studio or Rider.

Both implementations run on `http://localhost:3000` with identical debugger UI.

Instead of managing stateful sessions, it uses six core abstractions:
- Observable Gateway → Intercept all calls
- Stateless Turns → No session state
- Context Injection → RAG, history, instructions
- Prompt Assembly → Dynamic composition
- Cryptographic Audit → Tamper-evident chain
- Reconstructed State → Events, not objects

**The pattern is provider-agnostic.** Whether you're calling localhost or a cloud API, the middleware abstractions stay the same.

## Swap LLM Backends

The demo uses Ollama, but SATI's middleware pattern works with any LLM provider.

**Supported backends:**
- **Local models:** Ollama, LM Studio, vLLM, llama.cpp
- **Cloud APIs:** OpenAI, Anthropic Claude, Google Gemini
- **Enterprise:** Azure OpenAI, AWS Bedrock, GCP Vertex AI
- **Custom:** Your own inference infrastructure

## Why SATI

Most LLM integrations are black boxes tightly coupled to one provider. SATI treats LLMs like HTTP APIs.

**Core Benefits:**
- **Provider independence** - Switch from OpenAI to Anthropic to local models by without refactoring
- **Observability as infrastructure** - Monitor LLMs like you monitor REST APIs
- **Compliance-ready audit trails** - Cryptographic chain proves what the AI said (legal/medical/financial)
- **Stateless by design** - Replay any turn with full context, no session state to manage.
- **Prompt injection resilience** - Instructions regenerated per turn, not hijacked

**Who This Is For:**

*Developers prototyping AI features* - Ship in hours using local models, migrate to cloud APIs later without changing middleware.

*Platform teams* - Vendor-neutral telemetry, request/response tracing, and policy enforcement at the HTTP layer.

*Regulated industries* - Tamper-evident audit logs work identically whether you're using Azure, OpenAI or self-hosted models.

## Understanding the Code

**Want to understand how it works?** Paste [server.js](./nodejs/server.js) into Claude and ask:

- "How does this work?"
- "What are the different components?"
- "How is conversation history saved?"
- "How is this different from a framework?"

Full technical deep-dive: [pattern.md](./docs/pattern.md)

## This is a Pattern, Not a Product

SATI demonstrates that LLM middleware doesn't need complex frameworks or vendor SDKs.

The Node.js implementation uses minimal libraries. The .NET version is similar. Both use only:
- HTTP clients
- SQLite
- Standard middleware patterns

**This is intentionally minimal.** Other capable engineers could extend this into:
- Distributed tracing with OpenTelemetry
- Wrap it in Azure Entra ID 
- Policy enforcement layers
- More secure hashing
- Multi-tenant isolation
- Advanced RAG pipelines

## How This Was Built

This pattern emerged through iterative development with Claude (Anthropic) as a coding assistant. The architectural decisions, implementation choices, and pattern validation were human-driven. Claude accelerated code generation and helped refine documentation.

The dual implementation (Node.js and .NET) was deliberately done to prove the abstractions are language-agnostic and not artifacts of a single coding session.

## License
MIT
