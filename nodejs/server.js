const express = require('express');
const cors = require('cors');
const path = require('path');
const rateLimit = require('express-rate-limit');
const SimpleRAG = require('./rag');
const config = require('../config/config.json');
const fs = require('fs');
const crypto = require('crypto');
const Database = require('better-sqlite3');
const { createLLMClient } = require('./llm-client');

const app = express();
const PORT = process.env.PORT || 3000;

app.use(cors());
app.use(express.json());
app.use(express.static(path.join(__dirname, '../client')));

let ragInstance = null;
let llmClient = null;
let cachedInstructions = null;
let cachedRagSummary = null;

// Initialize database
const db = new Database('./conversation.db');

// Create table on startup
db.exec(`
    CREATE TABLE IF NOT EXISTS turns (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        conversation_id TEXT NOT NULL,
        turn INTEGER NOT NULL,
        timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
        user_prompt TEXT NOT NULL,
        full_prompt TEXT,
        llm_response TEXT,
        machine_state TEXT,
        rag_context TEXT,
        content_hash TEXT NOT NULL,
        chain_hash TEXT NOT NULL
    )
`);

// Prepare insert statement for reuse
const insertTurn = db.prepare(`
    INSERT INTO turns (conversation_id, turn, user_prompt, full_prompt, llm_response, machine_state, rag_context, content_hash, chain_hash)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
`);

// Rate limiting
const chatLimiter = rateLimit({
    windowMs: 15 * 60 * 1000, // 15 minutes
    max: 100, // limit each IP to 100 requests per windowMs
    message: 'Too many requests, please try again later'
});

app.post('/chat', chatLimiter, async (req, res) => {
    try {
        let { prompt, turn, conversationId, includeHistory = true } = req.body;
        // Generate conversationId if new conversation
        if (!conversationId) {
            conversationId = crypto.randomUUID();
        }
        // Load conversation history if this is a continuing conversation
        let conversationHistory = null;
        if (conversationId && includeHistory) {
            conversationHistory = getConversationHistory(conversationId);
        }
        console.log(conversationHistory)
        const result = await assemblePrompt(prompt, conversationHistory)
        console.log(result)
        const satiJson = JSON.parse(result.response.trim())
        console.log(satiJson)
        const trace = result.trace
        satiJson.turn = turn + 1
        
        // Generate hashes
        const contentHash = hashTurnContent(
            satiJson.turn,
            prompt,
            result.response,
            JSON.stringify(satiJson)
        );
        const previousChainHash = getLastChainHash(conversationId);
        const chainHash = createChainHash(contentHash, previousChainHash);

        insertTurn.run(
            conversationId,
            satiJson.turn,
            prompt,
            null,
            result.response,
            JSON.stringify(satiJson),
            null,
            contentHash,
            chainHash
        );

        const hashMsg = `Chain: ${chainHash}`;
        res.json({ response: satiJson, conversationId, trace, hashMsg });
    } catch (e) {
        console.error(e)
        res.status(500).json({ error: 'Internal server error' });
    }
})

// Generic context processor
async function assemblePrompt(userPrompt, conversationHistory = null) {
    const instructions = cachedInstructions;

    // Build conversation context if history exists
    let conversationContext = '';
    if (conversationHistory && conversationHistory.length > 0) {
        conversationContext = '\n\nPrevious conversation:\n' + 
            conversationHistory.map(turn => 
                `Turn ${turn.turn}:\nUser: ${turn.user_prompt}\nAssistant: ${turn.llm_response}`
            ).join('\n\n');
    }

    // do rag
    let ragContext = '';
    if (ragInstance && ragInstance.isLoaded) {
        const searchResults = ragInstance.search(userPrompt, 3); // Get top 3 chunks
        if (searchResults) {
            ragContext = '\n\nRelevant information from documents:\n' + searchResults;
        } else {
            const expandedSearch = await ragExpander(userPrompt)
            const extendedResults = ragInstance.search(expandedSearch, 3);
            if (extendedResults) {
                ragContext = '\n\nRelevant information from documents:\n' + extendedResults;
            } else {
                console.log('No results found')
            }
        }
    }

    // rag expander
    async function ragExpander(userPrompt) {
        const ragSummary = cachedRagSummary;
        const expander = `Analyze the user prompt and the RAG Summary, infer what the user is asking. 
            IF IT IS RELEVANT, Rewrite the query using proper terminology from the domain.
            Your response must be a SINGLE JSON object with a field "answer" containing ONE optimized search query.
            Focus on: correcting spelling, using domain-specific terms, and being specific.
            It should be clean and have no other strings. 
            ## DO NOT OUTPUT ANYTHING ELSE
            `
        const result = await llmClient.generate(expander, userPrompt, ragSummary, conversationHistory)
        console.log('ENHANCED QUERY:',result)
        const parsed = JSON.parse(result.response)
        const updatedPrompt = parsed.answer
        return updatedPrompt
    }

    // Build full prompt with instructions and protocol
    const fullPrompt = [
        userPrompt,
        ragContext,
        conversationContext,
        instructions,
    ].filter(Boolean).join('\n\n').trim(); // filter removes empty strings
        
    // Use LLM client instead of direct axios call
    if (config.llm.provider === 'ollama') {
        result = await llmClient.generate(fullPrompt)
    } else {
        result = await llmClient.generate(instructions, userPrompt, ragContext, conversationHistory)
    }
    return result;
}

// Hash the content of this turn
function hashTurnContent(turn, userPrompt, llmResponse, machineState) {
    const content = JSON.stringify({
        turn,
        userPrompt,
        llmResponse,
        machineState
    });
    return crypto.createHash('sha256').update(content).digest('hex');
}

// Create chain hash linking to previous turn
function createChainHash(contentHash, previousChainHash) {
    const combined = contentHash + (previousChainHash || '0');
    return crypto.createHash('sha256').update(combined).digest('hex');
}

// Get the last chain hash from database
function getLastChainHash(conversationId) {
    const lastTurn = db.prepare(
        'SELECT chain_hash FROM turns WHERE conversation_id = ? ORDER BY turn DESC LIMIT 1'
    ).get(conversationId);
    return lastTurn ? lastTurn.chain_hash : null;
}

// Shared verification logic
function verifyConversation(conversationId = null) {
    const query = conversationId 
        ? 'SELECT id, turn, conversation_id, user_prompt, llm_response, machine_state, content_hash, chain_hash FROM turns WHERE conversation_id = ? ORDER BY turn ASC'
        : 'SELECT id, turn, conversation_id, user_prompt, llm_response, machine_state, content_hash, chain_hash FROM turns ORDER BY conversation_id, turn ASC';
    
    const stmt = db.prepare(query);
    const turns = conversationId ? stmt.all(conversationId) : stmt.all();

    if (turns.length === 0) {
        return { valid: true, totalTurns: 0, message: 'No turns to verify' };
    }

    // Track chain per conversation
    const chainMap = new Map();
    let isValid = true;
    let invalidCount = 0;

    for (const turn of turns) {
        const prevHash = chainMap.get(turn.conversation_id) || null;
        
        // Verify content hash
        const expectedContentHash = hashTurnContent(
            turn.turn,
            turn.user_prompt,
            turn.llm_response,
            turn.machine_state
        );
        const contentValid = expectedContentHash === turn.content_hash;

        // Verify chain hash
        const expectedChainHash = createChainHash(turn.content_hash, prevHash);
        const chainValid = expectedChainHash === turn.chain_hash;

        if (!contentValid || !chainValid) {
            isValid = false;
            invalidCount++;
        }

        chainMap.set(turn.conversation_id, turn.chain_hash);
    }

    return {
        valid: isValid,
        totalTurns: turns.length,
        invalidTurns: invalidCount,
        conversationsVerified: chainMap.size
    };
}

// Verify all conversations
app.get('/verify', (req, res) => {
    res.json(verifyConversation());
});

// Verify specific conversation
app.get('/verify/:conversationId', (req, res) => {
    const { conversationId } = req.params;
    res.json(verifyConversation(conversationId));
});

function getConversationHistory(conversationId, maxTurns = null) {
    const query = maxTurns 
        ? 'SELECT turn, user_prompt, llm_response FROM turns WHERE conversation_id = ? ORDER BY turn ASC LIMIT ?'
        : 'SELECT turn, user_prompt, llm_response FROM turns WHERE conversation_id = ? ORDER BY turn ASC';
    
    const stmt = db.prepare(query);
    return maxTurns ? stmt.all(conversationId, maxTurns) : stmt.all(conversationId);
}

app.get('/context', async (req, res) => {
    res.json(config.config)
})

app.get('/metrics', async (req, res) => {
    res.setHeader('Content-Type', 'text/plain');
    res.send('Metrics endpoint - implement as needed');
});

app.get('/health', (req, res) => {
    const health = {
        status: 'ok',
        timestamp: Date.now(),
        rag: ragInstance?.isLoaded || false,
        llm: !!llmClient,
        db: !!db
    };
    res.json(health);
});


app.listen(PORT, async () => {
    // Initialize LLM client from config
    llmClient = createLLMClient(config);
    console.log(`LLM Provider: ${config.llm.provider}`);

    // Cache instruction files at startup to avoid sync reads per request
    const instructionsPath = path.join(__dirname, config.config.instructions);
    const ragSummaryPath = path.join(__dirname, config.config.ragSummary);
    cachedInstructions = fs.readFileSync(instructionsPath, "utf-8");
    cachedRagSummary = fs.readFileSync(ragSummaryPath, "utf-8");
    console.log(`Cached instructions and RAG summary`);

    // Initialize RAG
    const documentsPath = path.join(__dirname, config.config.documentsPath);
    ragInstance = new SimpleRAG(documentsPath);
    await ragInstance.initialize();

    console.log(`Server running on http://localhost:${PORT}`);
    console.log(`Database initialized: conversation.db`);
});

process.on('SIGINT', () => {
    db.close();
    process.exit(0);
});

