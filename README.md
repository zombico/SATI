# SATI - Stateless Audit Trail Inference


LLMs are stateless. REST is stateless. SATI is HTTP middleware that makes LLM conversations work like REST resources:


![Screenshot description](docs/headerscreen.png)
Chat interface on the left. Full observability on the right.

## What SATI enables
- **Stateless reconstruction** - no session store, conversations rebuilt from immutable records
- **Tamper-proof history** - blockchain-style chain hashing for integrity
- **Injection-resilient** - verified conversation history can't be manipulated

Observe, audit, and control every LLM interaction using standard REST API patterns.

**Interoperable LLM backends:** Anthropic and Ollama support


## Quick Start 

**Local (Ollama):**
```bash
ollama pull mistral
cd nodejs && npm install && node server.js
# Or: cd dotnet && dotnet run
```

**Cloud APIs (OpenAI/Anthropic):**
Edit [config.json](config/config.json) with your provider and API key, then run.

Both run on `http://localhost:3000` with identical UI.

## Why SATI

Most LLM integrations are black boxes tightly coupled to one provider. SATI treats LLMs like HTTP APIs.

Instead of managing stateful sessions, it uses six core abstractions:
- Observable Gateway → Intercept all calls
- Stateless Turns → No session state
- Context Injection → RAG, history, instructions
- Prompt Assembly → Dynamic composition
- Cryptographic Audit → Tamper-evident chain
- Reconstructed State → Events, not objects

**The pattern is provider-agnostic.** Whether you're calling localhost or a cloud API, the middleware abstractions stay the same.

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

Full technical deep-dive: [pattern.md](./docs/pattern.md)

## This is a Pattern, Not a Product

SATI demonstrates that LLM middleware can be done simply with HTTP.

**This is intentionally minimal.** Each abstraction is intentionally modular so it can be improved.
- Swap in production-grade RAG (vector databases, reranking)
- Upgrade hashing (HMAC, HSM integration)
- Add policy layers (rate limiting, content filtering)
- Implement auth (OAuth, API keys, mTLS)

Every interaction is HTTP, so standard web patterns just work: OpenTelemetry tracing, API gateways, load balancers, CDN caching.
Fork it. Build with it. Make it yours.

## License
MIT
