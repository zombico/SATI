# SATI - Stateless Audit Trail Inference
## Explanation of the HTTP-Native LLM Middleware Pattern

REST is stateless. LLMs are stateless. SATI shows that capturing each interaction and controlling it step-by-step is how web developers can build with AI using tools native to them.

## Core Concept

SATI addresses a fundamental challenge in AI systems: **how do you prove what an AI said, when it said it, and that the conversation history hasn't been altered?**

Traditional chatbots maintain state in memory or databases that can be modified. SATI takes a different approach by treating each conversation turn as an immutable record in a cryptographic chain, similar to blockchain principles but optimized for conversational AI.

## Generate JSON with Prompts

A key aspect of SATI is that **prompts become first-class citizens**. Programmable behavior is accessible via prompts, allowing you to shape LLM outputs into specific JSON structures based on user input.

This unlocks significant potential: LLMs become structured endpoints that can output the exact JSON needed to drive downstream operations. No parsing unstructured text—just reliable, typed responses.


# SATI: 6 Core Abstractions

## Request Flow (Vertical)
```
                    ┌──────────────┐
                    │     User     │
                    │   Request    │
                    └──────┬───────┘
                           ↓
            ┌──────────────────────────────┐
            │   ① Observable LLM Gateway   │
            │                              │
            │   • Intercept all calls      │
            │   • Add timing & tracing     │
            │   • Log metadata             │
            └──────────────┬───────────────┘
                           ↓
            ┌──────────────────────────────┐
            │   ② Stateless Turn Mgmt      │
            │                              │
            │   • Self-contained turn      │
            │   • No session state         │
            │   • Everything reconstructed │
            └──────────────┬───────────────┘
                           ↓
    ┌──────────┐  ┌──────────────────────────────┐
    │   RAG    │→ │   ③ Context Injection        │
    │   Docs   │  │                              │
    └──────────┘  │   • User input               │
                  │   • RAG knowledge            │
    ┌──────────┐  │   • Conversation history     │
    │Instructions│→│   • System instructions      │
    │  (fresh) │  │                              │
    └──────────┘  └──────────────┬───────────────┘
                           ↓
            ┌──────────────────────────────┐
            │   ④ Prompt Assembly          │
            │                              │
            │   • Compose all sources      │
            │   • User + RAG + History     │
            │   • + Instructions           │
            │   • Generate full prompt     │
            └──────────────┬───────────────┘
                           ↓
                    ┌──────────┐
                    │   LLM    │
                    │ (Local)  │
                    └─────┬────┘
                          ↓
            ┌──────────────────────────────┐
            │   ⑤ Cryptographic Audit      │
            │                              │
            │   • Hash turn content        │
            │   • Chain to previous hash   │
            │   • Tamper detection         │
            └──────────────┬───────────────┘
                           ↓
            ┌──────────────────────────────┐
            │   ⑥ Reconstructed State      │
            │                              │
            │   • Store in SQLite          │
            │   • Immutable turns          │
            │   • State = view over events │
            └──────────────┬───────────────┘
                           ↓
                    ┌──────────────┐
                    │   Response   │
                    │  + Metadata  │
                    └──────┬───────┘
                           │
                           └────────┐
                                    ↓
        History reconstructed ──────┘
        (loops back to ③)
```

## The 6 Abstractions Explained

### ① Observable LLM Gateway
**What:** Intercepts all LLM communication  
**Why:** Provides visibility into every interaction  
**How:** Axios interceptors add timing, tracing, and metadata to requests/responses

### ② Stateless Turn Management
**What:** Each interaction is self-contained  
**Why:** No session state means predictable, reproducible behavior  
**How:** Turn carries everything needed; state reconstructed from storage

### ③ Context Injection Points
**What:** Multiple sources feed into the prompt  
**Why:** Separates concerns—user input, knowledge, history, behavior  
**How:** RAG documents, instructions, and history injected independently

### ④ Prompt Assembly Pipeline
**What:** Dynamic composition from all sources  
**Why:** Instructions regenerated per turn = resilient to hijacking  
**How:** Concatenates user + RAG + history + instructions into full prompt

### ⑤ Cryptographic Audit Trail
**What:** Tamper-evident chain of conversation turns  
**Why:** Proves what was said, when, and in what order  
**How:** SHA-256 content hash + chain hash linking to previous turn

### ⑥ Conversation as Reconstructed State
**What:** State exists as ordered turns in database  
**Why:** Immutable events > mutable objects  
**How:** Query database to reconstruct conversation at any point


## How They Work Together

1. **Request enters** through Observable Gateway (①)
2. **Turn created** with no session state (②)
3. **Context gathered** from RAG, history, instructions (③)
4. **Prompt assembled** dynamically (④)
5. **LLM responds** (external)
6. **Response hashed** and chained (⑤)
7. **Turn persisted** to database (⑥)
8. **History loops back** for next turn (⑥ → ③)

Each abstraction solves one problem. Together, they create observable, auditable, stateless LLM systems using standard web patterns.

## Verification

Most chatbots are black boxes. with SATI:
- **Proof of what the AI said** (cryptographic hashes)
- **When it said it** (timestamps)
- **What context it had** (full prompt reconstruction)
- **Unalterable history** (blockchain-style chaining)

```
Turn 1: hash(content₁) → chain₁ = hash(content₁ + "0")
Turn 2: hash(content₂) → chain₂ = hash(content₂ + chain₁)
Turn 3: hash(content₃) → chain₃ = hash(content₃ + chain₂)
```

Each turn's chain hash depends on all previous turns. Changing any historical turn invalidates all subsequent hashes.

The system provides a `/verify` endpoint that:
- Recalculates content hashes for all turns
- Validates chain hash linkage
- Reports any integrity violations
- Works per-conversation or across all conversations

## Model Tuning Considerations
This demo was prepared using Mistral. You can switch out the model, but performance will vary.

- **Model Behavior**: Like how browsers have different engines and require specialized handling behavior, LLM models have inherent quirks that need to be observed and optimized for.
- **Sequencing the full prompt combination**: the sequence used in the implementation (Query > RAG > Conversation History > Instructions) is deliberate and tuned for Mistral. Other LLMs might have different  interpretation of this sequence.
- **JSON generation**: All the combination of factors above all influence how effective the JSON is created. Experimentation will be required.


## Hijacking and Prompt Injection
- **Resilient to drift** - Instructions regenerated each turn  
- **Resilient to hijacking** - Defenses reinforced dynamically per request

**The Tradeoff** - too many constraints make it difficult for LLM's to operate well. Balancing the performance is non-trivial.


## Caveats

- Quality outputs require well-crafted prompts and instructions
- Performance is subject to hardware
- Local model capability and inference may vary
- While resilient, it is not fully hardened against Prompt Injection
- Current state shared here is a reference implementation - not hardened for production use


## Support
This is not a maintained framework.

The repo was created to demonstrate the pattern 
and guide those who would like to understanding and use it.
Its sole purpose is to point out the way to build with AI in a web-first way.

It is not definitive. Each component demonstrated can be modularized and optimized in a way that experts know how.

Would love to see forks of an optimized .NET implementation, or in other languages.