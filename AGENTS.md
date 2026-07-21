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

## Cursor rules

Committed agent hints: [`.cursor/rules/`](./.cursor/rules/) (C# conventions). Workspace-level `esl-all/.cursor/rules/` is local-only — not source of truth for cloud agents.

## Deep context

See [CLAUDE.md](./CLAUDE.md) for pipeline architecture, services, config, and verify workflow.
