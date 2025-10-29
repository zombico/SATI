const axios = require('axios');
const { json } = require('express');

/**
 * Resolves environment variable references in config values
 * Replaces ${ENV_VAR} with process.env.ENV_VAR
 */
function resolveEnvVars(obj) {
    if (typeof obj === 'string') {
        const match = obj.match(/^\$\{(.+)\}$/);
        if (match) {
            const envVar = match[1];
            const value = process.env[envVar];
            if (!value) {
                throw new Error(`Environment variable ${envVar} is not set`);
            }
            return value;
        }
        return obj;
    }

    if (Array.isArray(obj)) {
        return obj.map(resolveEnvVars);
    }

    if (obj && typeof obj === 'object') {
        const resolved = {};
        for (const [key, value] of Object.entries(obj)) {
            resolved[key] = resolveEnvVars(value);
        }
        return resolved;
    }

    return obj;
}

/**
 * Base class for LLM providers
 */
class LLMAdapter {
    constructor(config) {
        this.config = resolveEnvVars(config);
        this.setupInterceptors();
    }

    setupInterceptors() {
        axios.interceptors.request.use(request => {
            const timestamp = new Date().toISOString();
            request.meta = request.meta || {};
            request.meta.requestTimestamp = timestamp;
            request.meta.requestStartedAt = Date.now();
            console.log(`[LLM CALL ${this.config.model}] ${request.method.toUpperCase()} ${request.baseURL || ''}${request.url} | Timestamp: ${timestamp}`);
            return request;
        });

        axios.interceptors.response.use(
            response => {
                const endTime = Date.now();
                const duration = endTime - response.config.meta.requestStartedAt;

                response.trace = {
                    requestTimestamp: response.config.meta.requestTimestamp,
                    responseTimestamp: new Date().toISOString(),
                    durationMs: duration,
                    method: response.config.method.toUpperCase(),
                    model: this.config.model,
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
    }

    async generate(prompt) {
        throw new Error('generate() must be implemented by adapter');
    }
}

/**
 * Ollama adapter
 */
class OllamaAdapter extends LLMAdapter {
    async generate(prompt) {
        const url = `${this.config.host}${this.config.endpoint}`;

        const response = await axios.post(url, {
            model: this.config.model,
            prompt: prompt,
            format: this.config.format || 'json',
            stream: this.config.stream || false
        }, {
            timeout: this.config.timeout || 300000
        });

        return {
            response: response.data.response,
            trace: response.trace
        };
    }
}

/**
 * OpenAI adapter
 */
class OpenAIAdapter extends LLMAdapter {
    async generate(prompt) {
        const url = `${this.config.baseURL}${this.config.endpoint}`;
        const jsonify = JSON.stringify(prompt)
        const response = await axios.post(url, {
            model: this.config.model,
            input: jsonify
        }, {
            timeout: this.config.timeout || 300000,
            headers: {
                'Authorization': `Bearer ${this.config.apiKey}`,
                'OpenAI-Organization': this.config.organization,
                'Content-Type': 'application/json'
            }
        });

        const content = response?.data?.output[0]?.content[0]?.text
        return {
            response: content,
            trace: response.trace
        };
    }
}

/**
 * Anthropic adapter
 */
class AnthropicAdapter extends LLMAdapter {
    async generate(instructions, userPrompt, ragContext, conversationContext) {
        console.log(ragContext.length)

        const url = `${this.config.baseURL}${this.config.endpoint}`;
        
        const systemInstructions = {
            type: "text",
            text: instructions,
            cache_control: { type: "ephemeral" }
        }
        const ragInstructions = {
            type: "text",
            text: ragContext.length > 1 ? ragContext : "No documents found",
            cache_control: { type: "ephemeral" }
        }
        const currentUserPrompt = {
            role: 'user',
            content: userPrompt
        }

        const response = await axios.post(url, {
            model: this.config.model,
            max_tokens: this.config.maxTokens || 4096,
            system: [
                systemInstructions,
                ragInstructions
            ],
            messages: [
                currentUserPrompt
            ]
        }, {
            timeout: this.config.timeout || 300000,
            headers: {
                'x-api-key': this.config.apiKey,
                'anthropic-version': '2023-06-01',
                'Content-Type': 'application/json'
            }
        });

        const content = response.data.content[0]
        const raw = content.text;
        const jsonString = raw.replace(/```json|```/g, '').trim();
        let parsed;
        try {
            parsed = JSON.parse(jsonString);
            console.log(parsed);
        } catch (e) {
            console.error("Invalid JSON:", e);
        }
        return {
            response: jsonString,
            trace: response.trace
        };
    }
}

/**
 * Factory function to create appropriate adapter
 */
function createLLMClient(config) {
    const providerName = config.llm.provider;
    const providerConfig = config.llm[providerName];

    if (!providerConfig) {
        throw new Error(`Provider configuration for '${providerName}' not found in config`);
    }

    switch (providerName) {
        case 'ollama':
            return new OllamaAdapter(providerConfig);
        case 'openai':
            return new OpenAIAdapter(providerConfig);
        case 'anthropic':
            return new AnthropicAdapter(providerConfig);
        default:
            throw new Error(`Unsupported LLM provider: ${providerName}`);
    }
}

module.exports = { createLLMClient };