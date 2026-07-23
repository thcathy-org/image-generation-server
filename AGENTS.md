# AGENTS.md — image-generation-server

## Scope

ASP.NET Core microservice that generates AI vocabulary images for [funfunspell.com](https://www.funfunspell.com). Accepts phrase generation requests, writes images to Firebase Storage, and tracks a human verify workflow. Includes `angular-admin/` for reviewing pending phrases.

## Commands

```bash
# API server
dotnet build ImageGenerationServer/ImageGenerationServer.csproj
dotnet test ImageGenerationServer.UT/ImageGenerationServer.UT.csproj

# Admin UI (from angular-admin/)
npm install && npm start
```

Health check: `GET /healthz`  
Generate (requires API key): `POST /image/generate/{phrase}` with header `X-API-KEY`

## Related repos

| Repo | Relationship |
|------|--------------|
| `esl-rest` | Caller — queues phrases via `ImageGenerationService` |
| `esl-ionic` | Displays vocab images from Firebase (indirect) |
| ~~`esl-speech-worker`~~ | **Deprecated** — unrelated to this service |

## Cross-repo changes

- **Prompt model** → `ReplicateAiServiceOptions.PromptModel` in `appsettings.json`; see [IMAGE_PROMPT_MODEL_DECISION.md](./IMAGE_PROMPT_MODEL_DECISION.md)
- **Image provider** → `ImageGenerationServiceOptions.ImageProvider` (`localai` default, or `replicate`)
- **API contract / auth** → coordinate with `esl-rest` (`IMAGE_GENERATION_SERVER_HOST`, API key)
- **Firebase bucket/folder** → `FirebaseServiceOptions`; verify client image URLs still resolve

## Do not

- Change prompt model without eval and updating `IMAGE_PROMPT_MODEL_DECISION.md`
- Include Claude attribution in commit messages

## Cursor agents & rules

Committed agents: [`.cursor/agents/`](./.cursor/agents/) — `esl-image-senior-dev` (design), `esl-image-programmer` (implement). They load stack/commands from this file, `.cursor/rules/`, and `CLAUDE.md` (do not hardcode stack in agent prompts). For `angular-admin` UI/UX, use `esl-uiux` (lives in `esl-ionic/.cursor/agents/` / workspace mirror).

Committed rules: [`.cursor/rules/`](./.cursor/rules/). Workspace-level `esl-all/.cursor/` is local-only — not source of truth for cloud agents.

## Deep context

See [CLAUDE.md](./CLAUDE.md) for pipeline architecture, services, config, and verify workflow.
