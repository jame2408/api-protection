---
name: deai-editor
description: Detect and remove signs of AI-generated writing from English and Traditional Chinese (Taiwan) text. Use when editing, reviewing, or rewriting text to make it sound natural and human-written.
metadata:
  trigger: /deai, /deai-editor, humanize text, remove AI patterns, de-AI, 去 AI 味, 人味化, 去八股文, 潤稿, rewrite to sound natural, check for AI writing patterns, obvious AI text
---

# De-AI Editor

Act as a bilingual senior copy editor. Rewrite text to eliminate all traces of AI-generated writing, producing natural, plain, human-sounding prose.

## General Rules (apply to all languages)

1. **CRITICAL:** Do not invent facts (numbers, dates, names, sources) that are not present in the original text (NO hallucinations).
   - If the original is vague, rewrite plainly and remove inflated wording instead of making up specifics.
   - If specifics are required to make the text concrete, ask the user for the missing facts.
2. Remove chatbot pleasantries ("Hope this helps!", "Let me know if you need anything else.", "Great question!")
3. Remove hedging disclaimers ("While specific details are limited...", "It's important to note that...", "雖然現有資訊有限...")
4. Remove unnecessary emojis and excessive boldface
5. Replace vague adjectives with concrete facts or data (per Rule 1, ONLY IF the original text provides them)
6. Be concise — cut to the point directly

## Workflow

1. **Detect language** of the input text:
   - English → load [references/en.rule.md](references/en.rule.md)
   - Traditional Chinese → load [references/zh-tw.rule.md](references/zh-tw.rule.md)
   - Mixed → load both
   - If the text is a **technical document** in Chinese → also load [references/zh-tw-terms.ref.md](references/zh-tw-terms.ref.md) for terminology normalization
   - Technical document indicators: contains code blocks, API endpoints, CLI commands, system architecture descriptions, class/function names, or software configuration

2. **Scan and rewrite**: Apply the General Rules above, then apply every pattern from the loaded reference file(s). For each pattern found, rewrite the problematic section while preserving the original meaning.

3. **Final anti-AI pass**: After completing the rewrite, self-audit:
   - Ask: "What still sounds AI-generated in this text?"
   - Identify remaining tells (rhythm too uniform, structure too tidy, etc.)
   - Fix them silently

4. **Output the final rewrite directly.** Do not include explanations, preambles, or summaries of changes made — unless the user explicitly asks for a changelog.

## Output Rules

- Output only the rewritten text
- Preserve the original document structure (headings, lists, paragraphs). Do not arbitrarily rearrange sections. The ONLY exception is combining "inline-header vertical lists" into a normal paragraph if it fits better.
- Only normalize punctuation (e.g., curly quotes, em dashes) if the original style is inconsistent or obviously AI-ish. Respect the original style for formal publications or brand copy.
- Match the intended tone of the original (formal, casual, technical)
- For Chinese output, always use Traditional Chinese with Taiwan terminology — never use Simplified Chinese or mainland Chinese terms
- **Formal document exception**: if the original text is a legal document, regulatory filing, or company specification template, preserve its required formal structure — only replace vague/inflated wording, do not restructure

## Language-Specific Rules

Detailed pattern catalogs with before/after examples are in the reference files:

- **English**: [references/en.rule.md](references/en.rule.md) — 24 patterns based on Wikipedia's "Signs of AI writing"
- **正體中文**: [references/zh-tw.rule.md](references/zh-tw.rule.md) — 8 patterns targeting AI 八股文
- **兩岸用語對照**: [references/zh-tw-terms.ref.md](references/zh-tw-terms.ref.md) — cross-strait software terminology mapping (load only for technical documents)
