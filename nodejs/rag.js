// rag.js
const fs = require('fs').promises;
const fsSync = require('fs');
const path = require('path');
const pdfParse = require('pdf-parse');

class SimpleRAG {
  constructor(documentsPath = '../documents') {
    this.documentsPath = path.resolve(documentsPath); // Use absolute path
    this.documentChunks = [];
    this.isLoaded = false;
    this.loadedFiles = new Set();
  }

  // Initialize and load all documents
  async initialize() {
    if (this.isLoaded) {
      console.log('📚 RAG already initialized');
      return;
    }
    
    try {
      console.log(`📚 Initializing RAG system from: ${this.documentsPath}`);
      
      // Check if documents directory exists
      if (!fsSync.existsSync(this.documentsPath)) {
        throw new Error(`Documents directory does not exist: ${this.documentsPath}`);
      }
      
      // Clear existing chunks
      this.documentChunks = [];
      this.loadedFiles.clear();
      
      const files = await fs.readdir(this.documentsPath);
      const pdfFiles = files.filter(file => file.toLowerCase().endsWith('.pdf'));
      
      if (pdfFiles.length === 0) {
        console.log('📄 No PDF files found in documents directory');
        this.isLoaded = true;
        return;
      }
      
      console.log(`📄 Found ${pdfFiles.length} PDF files: ${pdfFiles.join(', ')}`);
      
      const loadPromises = pdfFiles.map(file => this.loadDocument(file));
      const results = await Promise.allSettled(loadPromises);
      
      // Log results
      let successCount = 0;
      let failCount = 0;
      
      results.forEach((result, index) => {
        if (result.status === 'fulfilled') {
          successCount++;
        } else {
          failCount++;
          console.error(`❌ Failed to load ${pdfFiles[index]}: ${result.reason.message}`);
        }
      });
      
      console.log(`✅ RAG initialized: ${successCount} successful, ${failCount} failed`);
      console.log(`📊 Total chunks: ${this.documentChunks.length}`);
      
      this.isLoaded = true;
      
    } catch (error) {
      console.error('❌ RAG initialization failed:', error.message);
      this.isLoaded = false;
      throw error;
    }
  }

  // Load a single document (now async)
  async loadDocument(filename) {
    try {
      const filePath = path.join(this.documentsPath, filename);
      
      console.log(`🔄 Processing ${filename}...`);
      
      // Check if file exists
      const stats = await fs.stat(filePath);
      if (!stats.isFile()) {
        throw new Error(`${filename} is not a file`);
      }
      
      // Check file size (warn if very large)
      const fileSizeMB = stats.size / (1024 * 1024);
      if (fileSizeMB > 50) {
        console.log(`⚠️ Large file detected: ${filename} (${fileSizeMB.toFixed(2)}MB)`);
      }
      
      const dataBuffer = await fs.readFile(filePath);
      const pdfData = await pdfParse(dataBuffer);
      
      if (!pdfData.text || pdfData.text.trim().length === 0) {
        throw new Error(`No text extracted from ${filename}`);
      }
      
      // Split into chunks
      const chunks = this.splitIntoChunks(pdfData.text, 500);
      
      if (chunks.length === 0) {
        console.log(`⚠️ No usable chunks created from ${filename}`);
        return;
      }
      
      // Add chunks to collection
      chunks.forEach((chunk, index) => {
        this.documentChunks.push({
          id: `${filename}-${index}`,
          filename: filename,
          content: chunk.trim(),
          chunkIndex: index,
          wordCount: chunk.split(/\s+/).length,
          createdAt: new Date().toISOString()
        });
      });
      
      this.loadedFiles.add(filename);
      console.log(`  ✓ Added ${chunks.length} chunks from ${filename} (${pdfData.text.length} chars)`);
      
    } catch (error) {
      console.error(`❌ Error loading ${filename}:`, error.message);
      throw error; // Re-throw to be handled by caller
    }
  }

  // Improved text chunking with better sentence handling
  splitIntoChunks(text, maxChunkSize = 500, overlap = 50) {
    const chunks = [];
    
    // Clean up text first
    const cleanText = text
      .replace(/\s+/g, ' ') // Normalize whitespace
      .replace(/[^\w\s.,!?;:()"'-]/g, ' ') // Remove unusual characters
      .trim();
    
    if (cleanText.length < 50) {
      return []; // Skip very short texts
    }
    
    // Split into sentences using multiple delimiters
    const sentences = cleanText.split(/[.!?]+/)
      .map(s => s.trim())
      .filter(s => s.length > 10); // Filter out very short fragments
    
    let currentChunk = '';
    
    for (let i = 0; i < sentences.length; i++) {
      const sentence = sentences[i];
      const potentialChunk = currentChunk + (currentChunk ? '. ' : '') + sentence;
      
      if (potentialChunk.length > maxChunkSize && currentChunk.length > 0) {
        // Add current chunk and start new one
        chunks.push(currentChunk.trim() + '.');
        
        // Start new chunk with overlap if possible
        const words = currentChunk.split(/\s+/);
        const overlapWords = words.slice(-Math.min(overlap / 5, words.length / 2));
        currentChunk = overlapWords.join(' ') + (overlapWords.length > 0 ? '. ' : '') + sentence;
      } else {
        currentChunk = potentialChunk;
      }
    }
    
    // Add final chunk if it has content
    if (currentChunk.trim()) {
      chunks.push(currentChunk.trim() + (currentChunk.endsWith('.') ? '' : '.'));
    }
    
    // Filter chunks by minimum length and quality
    return chunks.filter(chunk => {
      const wordCount = chunk.split(/\s+/).length;
      return wordCount >= 5 && chunk.length >= 30; // Minimum quality thresholds
    });
  }

  // Enhanced search with better scoring
  search(query, maxResults = 3, minScore = 1) {
    if (!this.isLoaded || this.documentChunks.length === 0) {
      console.log('📭 RAG not loaded or no documents available');
      return '';
    }
    
    const cleanQuery = query.trim();
    if (cleanQuery.length < 3) {
      console.log('🔍 Query too short for RAG search');
      return '';
    }
    
    console.log(`🔍 RAG search: "${cleanQuery}" (${this.documentChunks.length} chunks)`);
    
    // Enhanced keyword extraction
    const stopWords = new Set([
      'the', 'is', 'at', 'which', 'on', 'and', 'a', 'to', 'are', 'as', 
      'was', 'with', 'for', 'of', 'in', 'by', 'an', 'be', 'or', 'that',
      'this', 'will', 'you', 'have', 'it', 'not', 'can', 'from', 'they',
      'we', 'been', 'has', 'had', 'do', 'would', 'could', 'should'
    ]);
    
    const queryWords = cleanQuery.toLowerCase()
      .replace(/[^\w\s]/g, ' ') // Remove punctuation
      .split(/\s+/)
      .filter(word => word.length > 2 && !stopWords.has(word))
      .slice(0, 10); // Limit to prevent over-matching
    
    if (queryWords.length === 0) {
      console.log('🤷 No meaningful keywords extracted from query');
      return '';
    }
    
    console.log(`🔑 Keywords: [${queryWords.join(', ')}]`);
    
    const scoredChunks = [];
    
    // Score each chunk with multiple factors
    this.documentChunks.forEach(chunk => {
      let score = 0;
      const chunkText = chunk.content.toLowerCase();
      const chunkWords = chunkText.split(/\s+/);
      
      queryWords.forEach(word => {
        // Exact word matches (highest score)
        const exactMatches = (chunkText.match(new RegExp(`\\b${this.escapeRegex(word)}\\b`, 'g')) || []).length;
        score += exactMatches * 3;
        
        // Partial matches (lower score)
        const partialMatches = (chunkText.match(new RegExp(this.escapeRegex(word), 'g')) || []).length - exactMatches;
        score += partialMatches * 1;
        
        // Bonus for word density in shorter chunks
        if (exactMatches > 0 && chunkWords.length < 100) {
          score += 0.5;
        }
      });
      
      // Penalty for very long chunks (prefer more focused content)
      if (chunkWords.length > 200) {
        score *= 0.8;
      }
      
      if (score >= minScore) {
        scoredChunks.push({ 
          ...chunk, 
          score: Math.round(score * 100) / 100,
          matchedWords: queryWords.filter(word => 
            chunkText.includes(word.toLowerCase())
          )
        });
      }
    });
    
    // Sort by score and get top results
    scoredChunks.sort((a, b) => b.score - a.score);
    const topChunks = scoredChunks.slice(0, maxResults);
    
    if (topChunks.length > 0) {
      console.log(`📋 Found ${topChunks.length} relevant chunks:`);
      topChunks.forEach((chunk, i) => {
        console.log(`   ${i + 1}. ${chunk.filename} (score: ${chunk.score}, words: [${chunk.matchedWords.join(', ')}])`);
      });
      
      // Format results for LLM
      const formattedResults = topChunks.map((chunk, index) => {
        const prefix = topChunks.length > 1 ? `[${index + 1}] ` : '';
        return `${prefix}[From ${chunk.filename}]:\n${chunk.content}`;
      }).join('\n\n---\n\n');
      
      return formattedResults;
    }
    
    console.log('🤷 No relevant chunks found above minimum score threshold');
    return '';
  }

  // Helper method to escape regex special characters
  escapeRegex(string) {
    return string.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  // Get comprehensive statistics
  getStats() {
    const fileStats = {};
    let totalWords = 0;
    
    this.documentChunks.forEach(chunk => {
      fileStats[chunk.filename] = (fileStats[chunk.filename] || 0) + 1;
      totalWords += chunk.wordCount || 0;
    });
    
    return {
      isLoaded: this.isLoaded,
      documentsPath: this.documentsPath,
      totalChunks: this.documentChunks.length,
      totalWords: totalWords,
      averageWordsPerChunk: this.documentChunks.length > 0 ? Math.round(totalWords / this.documentChunks.length) : 0,
      loadedFiles: Array.from(this.loadedFiles),
      chunksPerFile: fileStats,
      memoryUsage: this.estimateMemoryUsage()
    };
  }

  // Estimate memory usage
  estimateMemoryUsage() {
    let totalSize = 0;
    this.documentChunks.forEach(chunk => {
      totalSize += JSON.stringify(chunk).length * 2; // Rough estimate (UTF-16)
    });
    return {
      estimatedBytes: totalSize,
      estimatedMB: Math.round(totalSize / (1024 * 1024) * 100) / 100
    };
  }

  // Add a new document at runtime
  async addDocument(filename) {
    try {
      await this.loadDocument(filename);
      console.log(`📝 Successfully added document: ${filename}`);
      return true;
    } catch (error) {
      console.error(`❌ Failed to add document ${filename}:`, error.message);
      return false;
    }
  }

  // Remove a document's chunks
  removeDocument(filename) {
    const initialCount = this.documentChunks.length;
    this.documentChunks = this.documentChunks.filter(chunk => chunk.filename !== filename);
    this.loadedFiles.delete(filename);
    
    const removedCount = initialCount - this.documentChunks.length;
    if (removedCount > 0) {
      console.log(`🗑️ Removed ${removedCount} chunks from ${filename}`);
      return true;
    } else {
      console.log(`⚠️ No chunks found for ${filename}`);
      return false;
    }
  }

  // Clear all loaded documents
  clear() {
    const count = this.documentChunks.length;
    this.documentChunks = [];
    this.loadedFiles.clear();
    this.isLoaded = false;
    console.log(`🧹 Cleared ${count} chunks from RAG system`);
  }

  // Search for chunks from a specific document
  searchInDocument(query, filename, maxResults = 3) {
    const documentChunks = this.documentChunks.filter(chunk => 
      chunk.filename === filename
    );
    
    if (documentChunks.length === 0) {
      console.log(`📭 No chunks found for document: ${filename}`);
      return '';
    }
    
    // Temporarily filter to single document and search
    const originalChunks = this.documentChunks;
    this.documentChunks = documentChunks;
    
    const result = this.search(query, maxResults);
    
    // Restore original chunks
    this.documentChunks = originalChunks;
    
    return result;
  }

  // Get chunks for debugging/inspection
  getChunks(filename = null, limit = 10) {
    let chunks = this.documentChunks;
    
    if (filename) {
      chunks = chunks.filter(chunk => chunk.filename === filename);
    }
    
    return chunks.slice(0, limit).map(chunk => ({
      id: chunk.id,
      filename: chunk.filename,
      preview: chunk.content.substring(0, 100) + '...',
      wordCount: chunk.wordCount,
      chunkIndex: chunk.chunkIndex
    }));
  }
}

module.exports = SimpleRAG;