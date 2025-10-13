const express = require('express');
const axios = require('axios');
const cors = require('cors');
const path = require('path');
const SimpleRAG = require('./rag');
const config = require('./config/config.json');
const fs = require('fs');
const crypto = require('crypto');
const Database = require('better-sqlite3');

const app = express();
const PORT = 3000;
const promClient = require('prom-client');

axios.interceptors.request.use(request => {
  const timestamp = new Date().toISOString();
  request.meta = request.meta || {};
  request.meta.requestTimestamp = timestamp;
  request.meta.requestStartedAt = Date.now();
  
  console.log(`[LOCAL LLM CALL] ${request.method.toUpperCase()} ${request.baseURL || ''}${request.url} | Timestamp: ${timestamp}`);
  return request;
});

// Response interceptor: Calculate duration and attach metadata
axios.interceptors.response.use(
  response => {
    const endTime = Date.now();
    const duration = endTime - response.config.meta.requestStartedAt;
    
    response.trace = {
      requestTimestamp: response.config.meta.requestTimestamp,
      responseTimestamp: new Date().toISOString(),
      durationMs: duration,
      method: response.config.method.toUpperCase(),
      url: `${response.config.baseURL || ''}${response.config.url}`
    };
    
    return response;
  },
  error => {
    if (error.config && error.config.meta) {
      const endTime = Date.now();
      const duration = endTime - error.config.meta.requestStartedAt;
      
      error.trace = {
        requestTimestamp: error.config.meta.requestTimestamp,
        responseTimestamp: new Date().toISOString(),
        durationMs: duration,
        method: error.config.method.toUpperCase(),
        url: `${error.config.baseURL || ''}${error.config.url}`,
        error: true
      };
    }
    throw error;
  }
);

// Create a Registry to register metrics
const register = new promClient.Registry();

// Collect default metrics (CPU, memory, event loop lag, etc.)
promClient.collectDefaultMetrics({
    register: register,
    prefix: 'node_'
});

// Add custom metrics for your conversation system
const conversationCounter = new promClient.Counter({
    name: 'conversation_turns_total',
    help: 'Total number of conversation turns processed',
    labelNames: ['conversation_id']
});

const responseTime = new promClient.Histogram({
    name: 'llm_response_duration_seconds',
    help: 'Duration of LLM response generation',
    buckets: [0.1, 0.5, 1, 2, 5, 10, 30]
});

register.registerMetric(conversationCounter);
register.registerMetric(responseTime);


app.use(cors());
app.use(express.json());
app.use(express.static(path.join(__dirname, './client')));

const LLAMA_HOST = 'http://localhost:11434';
const MODEL_NAME = 'mistral';

let ragInstance = null;

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

app.post('/chat', async (req, res) => {
    const end = responseTime.startTimer();
    try {
        let { prompt, turn, conversationId } = req.body;
        // Generate conversationId if new conversation
        if (!conversationId) {
            conversationId = crypto.randomUUID();
        }

        const result = await assemblePrompt(config, prompt)
        
        const satiJson = JSON.parse(result.response)
        const trace = result.trace
        satiJson.turn = turn + 1
        // Record metrics
        conversationCounter.inc({ conversation_id: conversationId });
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
            conversationId,        // NEW: First parameter
            satiJson.turn,
            prompt,
            null,
            result.response,
            JSON.stringify(satiJson),
            null,
            contentHash,
            chainHash
        );


        const hashMsg = `Chain: ${chainHash.substring(0, 8)}...`;
        console.log(hashMsg)
        end(); // Record response time
        res.json({ response: satiJson, conversationId, trace, hashMsg });
    } catch (e) {
        end(); // Record failed request time
        console.error(e)
        res.status(500).json({ error: 'Internal server error' });
    }
})

// Generic context processor
async function assemblePrompt(contextConfig, userPrompt) {

    const instructionsPath = path.join(__dirname, contextConfig.config.instructions)
    const instructions = fs.readFileSync(instructionsPath, "utf-8");

    // Build full prompt with instructions and protocol
    const fullPrompt = [
        instructions,
        userPrompt
    ].filter(Boolean).join('\n\n'); // filter removes empty strings
    
    const response = await axios.post(`${LLAMA_HOST}/api/generate`, {
        model: MODEL_NAME,
        prompt: fullPrompt,
        stream: false
    }, { timeout: 300000 });

    const result = {
        response: response.data.response,
        trace: response.trace
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

app.get('/verify{/:conversationId}', (req, res) => {
    const { conversationId } = req.params;
    
    // Build query based on whether we're verifying specific conversation or all
    const query = conversationId 
        ? 'SELECT id, turn, conversation_id, user_prompt, llm_response, machine_state, content_hash, chain_hash FROM turns WHERE conversation_id = ? ORDER BY turn ASC'
        : 'SELECT id, turn, conversation_id, user_prompt, llm_response, machine_state, content_hash, chain_hash FROM turns ORDER BY conversation_id, turn ASC';
    
    const stmt = conversationId ? db.prepare(query) : db.prepare(query);
    const turns = conversationId ? stmt.all(conversationId) : stmt.all();

    if (turns.length === 0) {
        return res.json({ valid: true, totalTurns: 0, message: 'No turns to verify' });
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

    res.json({
        valid: isValid,
        totalTurns: turns.length,
        invalidTurns: invalidCount,
        conversationsVerified: chainMap.size
    });
});

app.get('/history', (req, res) => {
    const history = db.prepare('SELECT * FROM turns ORDER BY id ASC').all();
    res.json(history);
});

app.get('/history/:turn', (req, res) => {
    const turn = db.prepare('SELECT * FROM turns WHERE turn = ?').get(req.params.turn);
    res.json(turn);
});

app.get('/metrics', async (req, res) => {
    res.setHeader('Content-Type', register.contentType);
    res.send(await register.metrics());
});


app.listen(PORT, async () => {
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
