# doc-coauthoring Skill

A structured technical documentation workflow for PRD, RFC, and Design Docs.

## Prerequisites

### Required
- **Antigravity** installed and configured

### Optional (for Cross-Model Verification)
- **Gemini CLI** for enhanced quality verification in Stage 3

## Installing Gemini CLI

```bash
# Install via npm
npm install -g @anthropic-ai/gemini-cli

# Or install via homebrew (macOS)
brew install gemini-cli

# Verify installation
gemini --version
```

### Configuration (Authentication)

**Option A: Google Login (Recommended)**
```bash
# Login with your Google account
gemini auth login
```

**Option B: API Key**
```bash
# Set up API key via environment variable
export GEMINI_API_KEY="your-api-key"

# Or configure via CLI
gemini config set api-key your-api-key
```

## Usage

This skill triggers automatically when you ask to:
- Write a PRD (Product Requirements Document)
- Draft an RFC (Request for Comments)
- Create a Design Doc or Technical Spec
- Write a Proposal

### Example Prompts

```
"Help me write a PRD for a new user authentication feature"
"Draft an RFC for migrating to microservices"
"Create a design doc for the payment gateway integration"
```

## Workflow Overview

```
Stage 1: Context Gathering (Alignment)
    ↓
Stage 2: Drafting (Deep Dive or Fast Draft)
    ↓
Stage 3: Stress Test (Persona Simulation + Optional Cross-Model Verification)
    ↓
Final Review
```

## Cross-Model Verification (Stage 3)

If Gemini CLI is available, the agent can use a second model to verify document quality. This provides independent feedback from a different AI perspective.

### How It Works

1. Agent checks if `gemini` CLI is available
2. If available, sends the document to Gemini for review
3. Parses feedback and integrates into the refinement loop

### Opting Out

If you prefer self-verification only, you can tell the agent:
```
"Skip external verification, use self-review only"
```
