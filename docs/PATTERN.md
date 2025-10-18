# SATI 
## A REST-inspired Pattern for Verifiable AI Conversations
Reference Implementation for NodeJs and Dotnet C#

## **SATI** stands for 
- **S**tateless  
- **A**udit    
- **T**rail  
- **I**nference 

Utilizes the LLM's stateless nature. Cryptographically verifiable. Conversation history is restorable, and the source of inference. Create transparent, accountable AI interactions.

**No vendor dependencies. No API keys. No black boxes.**

## REST-Inspired Design
SATI applies REST principles to AI conversations:
- **Statelessness**: Each request contains all necessary context
- **Uniform Interface**: Consistent pattern across all interactions  
- **Client-Server**: Clear separation between inference and storage
- **Cacheable**: Responses are immutable and cryptographically signed
- **Layered System**: RAG, history, and instructions compose cleanly

## Philosophy
SATI chatbots are constrained to specific tasks rather than general knowledge, giving them utility with clear boundaries and expectations. Through dynamic prompt generation, SATI narrows the probability field with constraints to reduce unhelpful outputs.

REST is stateless. LLMs are stateless. SATI shows that capturing each interaction and controlling it step-by-step is how web developers can build with AI using tools native to them.

## Core Concept

SATI addresses a fundamental challenge in AI systems: **how do you prove what an AI said, when it said it, and that the conversation history hasn't been altered?**

Traditional chatbots maintain state in memory or databases that can be modified. SATI takes a different approach by treating each conversation turn as an immutable record in a cryptographic chain, similar to blockchain principles but optimized for conversational AI.

## Key Components

### 1. **Stateless Inference**
- Each LLM call is self-contained with all necessary context
- No hidden state between turns
- Full prompt assembly includes: user input, RAG context, conversation history, and instructions

### 2. **Cryptographic Chain**
- **Content Hash**: SHA-256 hash of each turn's complete content (user prompt, LLM response, machine state)
- **Chain Hash**: Links current turn to previous turn, creating an unbreakable chain
- Any modification to historical turns breaks the chain

### 3. **Audit Trail**
- Every conversation turn is permanently recorded
- Complete reproducibility: stored prompts and responses
- Timestamp tracking for temporal verification
- Multi-conversation support with unique identifiers

## Powered by JSON

A key aspect of SATI is that **prompts become first-class citizens**. Programmable behavior is accessible via prompts, allowing you to shape LLM outputs into specific JSON structures based on user input.

This unlocks significant potential: LLMs become structured endpoints that can output the exact JSON needed to drive downstream operations. No parsing unstructured text—just reliable, typed responses.

## How It Works

```
Turn 1: hash(content₁) → chain₁ = hash(content₁ + "0")
Turn 2: hash(content₂) → chain₂ = hash(content₂ + chain₁)
Turn 3: hash(content₃) → chain₃ = hash(content₃ + chain₂)
```

Each turn's chain hash depends on all previous turns. Changing any historical turn invalidates all subsequent hashes.

## Architecture

```
User Input → Context Assembly → LLM Inference → Response + Hash
                ↓
    [RAG Context, History, Instructions]
                ↓
           Database Record
    [Prompt, Response, Hashes, Metadata]
```


## Key Features

- **Conversation persistence**: Multiple conversations tracked independently
- **RAG integration**: Document retrieval with context tracking
- **History management**: Configurable conversation history inclusion
- **Chain verification**: Built-in endpoint to verify conversation integrity
- **Request tracing**: Timestamp and duration tracking for all LLM calls

## Implementation Pattern

1. **Prompt Assembly**: Gather all context (user input, RAG results, history, instructions)
2. **LLM Inference**: Send complete prompt to stateless LLM
3. **Hash Generation**: Create content hash and chain hash linking to previous turn
4. **Database Storage**: Persist turn with all metadata and hashes
5. **Response**: Return LLM response with chain hash proof

## Verification

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

## Benefits

### **Verifiability**
Verify entire conversation chains or specific conversations for tampering

### **Transparency**
Complete audit trail of what context was provided to the LLM

### **Accountability**
Cryptographic proof of AI responses and conversation integrity

### **Reproducibility**
Full prompt reconstruction enables debugging and analysis

### **Compliance**
Meet regulatory requirements for AI system auditability

