const express = require('express');
const axios = require('axios');
const cors = require('cors');
const path = require('path');
const SimpleRAG = require('./rag');
const config = require('./config/config.json');
const fs = require('fs');
const Database = require('better-sqlite3');

const app = express();
const PORT = 3000;

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
        turn INTEGER NOT NULL,
        timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
        user_prompt TEXT NOT NULL,
        full_prompt TEXT,
        llm_response TEXT,
        machine_state TEXT,
        rag_context TEXT
    )
`);

// Prepare insert statement for reuse
const insertTurn = db.prepare(`
    INSERT INTO turns (turn, user_prompt, full_prompt, llm_response, machine_state, rag_context)
    VALUES (?, ?, ?, ?, ?, ?)
`);



app.post('/chat', async (req, res) => {
    try {
        let { prompt, turn } = req.body;
        const result = await assemblePrompt(config, prompt)
        const satiJson = JSON.parse(result)
        satiJson.turn = turn + 1
        insertTurn.run(
            satiJson.turn,
            prompt,
            null,  // Store full_prompt if you want to log it
            result,
            JSON.stringify(satiJson),
            null   // Add RAG context if you're using ragInstance
        );

        console.log(satiJson.answer)
        console.log(satiJson.turn)
        res.json({ response: satiJson }); 
    } catch(e) {
        console.error(e)
        res.status(500).json({ error: 'Internal server error' });
    }
})

// Generic context processor
async function assemblePrompt(contextConfig, userPrompt) {
    console.log(contextConfig)
    const instructionsPath = path.join(__dirname, contextConfig.config.instructions)
    const instructions = fs.readFileSync(instructionsPath, "utf-8");

    // Build full prompt with instructions and protocol
    const fullPrompt = [
        instructions,
        userPrompt
    ].filter(Boolean).join('\n\n'); // filter removes empty strings
    console.log(fullPrompt)
    const response = await axios.post(`${LLAMA_HOST}/api/generate`, {
        model: MODEL_NAME,
        prompt: fullPrompt,
        stream: false
    }, { timeout: 300000 });

    const result = response.data.response;
  console.log(result)
    
    return result;
}

app.get('/history', (req, res) => {
    const history = db.prepare('SELECT * FROM turns ORDER BY id ASC').all();
    res.json(history);
});

app.get('/history/:turn', (req, res) => {
    const turn = db.prepare('SELECT * FROM turns WHERE turn = ?').get(req.params.turn);
    res.json(turn);
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
