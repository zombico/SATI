const express = require('express');
const axios = require('axios');
const cors = require('cors');
const path = require('path');
const SimpleRAG = require('./rag');
const config = require('./config/config.json');
const fs = require('fs');

const app = express();
const PORT = 3000;

app.use(cors());
app.use(express.json());
app.use(express.static(path.join(__dirname, '../client')));

const LLAMA_HOST = 'http://localhost:11434';
const MODEL_NAME = 'mistral';

let ragInstance = null;

app.listen(PORT, async () => {
    const documentsPath = path.join(__dirname, config.config.documentsPath);
    ragInstance = new SimpleRAG(documentsPath);
    await ragInstance.initialize();
    console.log(`Server running on http://localhost:${PORT}`);
});

app.post('/chat', async (req, res) => {
    try {
        let { prompt, turn } = req.body;
        const result = await assemblePrompt(config, prompt)
        const satiJson = JSON.parse(result)
        satiJson.turn = turn + 1
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

app.listen(PORT, () => {
    console.log(`Server running on http://localhost:${PORT}`);
});