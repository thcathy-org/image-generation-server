# Image prompt model decision

**Date:** 2026-07-18  
**Chosen:** Replicate `openai/gpt-5-mini`

## Tested

| Model | Outcome |
|-------|---------|
| LocalAI `qwen3-4b` | Rejected — unsafe `knife`, text on `because` |
| `gpt-5-nano` | OK; weaker on abstract words |
| `gpt-5-mini` | **Best** — keep |
| `qwen3-235b` | No gain; slower; text on `bank` |

Eval data: workspace `scripts/image-prompt-eval/results/2026-07-18-all-models.csv` (if available locally)

## Config

```json
"PromptModel": "openai/gpt-5-mini",
"PromptMaxTokens": 512,
"PromptTemperature": 0.3
```

Image pixels → LocalAI `flux.2-klein-4b` (default; `ImageGenerationServiceOptions.ImageProvider=localai`).
