# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## About

ASP.NET Core 7 microservice for AI vocabulary image generation on [funfunspell.com](https://www.funfunspell.com). Prompts via Replicate LLM; pixels via LocalAI Flux (default) or Replicate Flux; storage in Google Firebase. SQLite tracks phrases pending human verification.

## Commands

```bash
# Build and test
dotnet build ImageGenerationServer/ImageGenerationServer.csproj
dotnet test ImageGenerationServer.UT/ImageGenerationServer.UT.csproj

# Run API (from repo root)
dotnet run --project ImageGenerationServer/ImageGenerationServer.csproj

# Admin UI
cd angular-admin && npm install && npm start   # http://localhost:4200
```

Tests use MSTest + Moq + MockHttp in `ImageGenerationServer.UT/`.

## Git

Do not include `Co-Authored-By: Claude` or any Claude attribution in commit messages.

## Related repos

| Repo | Role |
|------|------|
| `esl-rest` | Spring Boot API — queues image generation requests |
| `esl-ionic` | Angular/Ionic client — displays vocab images |
| `image-generation-server` | This repo |

TTS lives in `esl-rest` (LocalAI Kokoro + Azure). `esl-speech-worker` is deprecated.

Agent entry point: [AGENTS.md](./AGENTS.md).

## Architecture

### Solution layout

```
ImageGenerationServer/          # ASP.NET Core API + background worker
  Controllers/                  # ImageController, VerifyController
  Services/                     # Generation, Replicate, LocalAI, Firebase, Verify
  DB/                           # EF Core + SQLite (pending verify phrases)
  Middleware/                   # ApiKeyMiddleware
ImageGenerationServer.UT/       # Unit tests
angular-admin/                  # Angular 16 admin UI; pending-verify keep/remove workflow is retired
ansible/                        # K8s deploy
```

### Image generation pipeline

1. **`POST /image/generate/{phrase}`** — `ImageController` enqueues phrase on an unbounded `Channel<string>`.
2. **`ImageGenerationService`** (hosted `BackgroundService`) reads the channel:
   - Skips if Firebase object already exists at `{folder}/{slug}.json` (does not rewrite it)
   - Calls **`ReplicateAiService.GenerateImagePrompts(phrase)`** — always Replicate LLM (`openai/gpt-5-mini`); returns exactly 3 prompts
   - For each prompt, generates image via provider selected by **`ImageGenerationServiceOptions.ImageProvider`**:
     - `localai` (default) → **`LocalAiImageService`** → LocalAI `flux.2-klein-4b`
     - `replicate` → **`ReplicateAiService.GenerateImageFromPrompt`**
   - Uploads JSON `{ images: [base64...], isVerify: false }` to Firebase
   - Does **not** insert `PendingVerifyPhrases`. New images are available without a pending-verify / Submit Verified step. Clients ignore `isVerify`; this service does not bulk-rewrite existing Firebase JSON to flip it.
3. **Remove APIs** — `POST /verify/verified` with `removeImageIndex` drops only those indexes and leaves the other images. `POST /verify/remove` deletes the phrase file. `GET /verify/pending` and `GET /verify/total` remain but are not a show-gate. The angular admin keep/remove page is retired. This service has no report queue.

Firebase path: `{first-two-chars}/{slug}.json` (see `StringExtension.GetImageFilePath`).

### Key services

| Service | Responsibility |
|---------|----------------|
| `ReplicateAiService` | LLM prompts + optional Replicate Flux image generation |
| `LocalAiImageService` | LocalAI Flux image generation (default pixel provider) |
| `FirebaseService` | Upload/download/delete JSON objects in GCS bucket |
| `VerifyService` | Index-remove on `POST /verify/verified` — drops listed indexes, rewrites that one phrase file |
| `ImageGenerationService` | Background queue processor. Uploads new images; does not enqueue pending verify |

### API endpoints

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/image/generate/{phrase}` | Enqueue phrase for generation |
| GET | `/verify/total` | Count leftover pending-verify rows (not required for images to show) |
| GET | `/verify/pending?max=20` | List leftover pending phrases (not a show-gate) |
| POST | `/verify/verified` | Remove images by `removeImageIndex`; other images stay |
| POST | `/verify/remove` | Delete the phrase file from Firebase |
| GET | `/healthz` | Health check (includes SQLite) |

All routes except `/healthz` require **`X-API-KEY`** header (see `ApiKeyMiddleware`).

### Configuration (`appsettings.json`)

| Section | Key settings |
|---------|--------------|
| `ImageGenerationServiceOptions` | `ImageProvider`: `localai` (default) or `replicate` |
| `ReplicateAiServiceOptions` | `PromptModel`, `PromptMaxTokens`, `PromptTemperature`, `ImageModel`, `Token` |
| `LocalAiImageServiceOptions` | `BaseUrl`, `ApiKey`, `ImageModel`, `Size`, `RequestTimeoutSeconds` |
| `FirebaseServiceOptions` | `BucketName`, `FirebaseBaseFolder` |
| `ApiKeyMiddlewareOptions` | `ApiKeys`, `ApiKeyHeaderName` |
| `ConnectionStrings:LocalDatabase` | SQLite path for pending verify DB |

Environment overrides use `__` separator (e.g. `ReplicateAiServiceOptions__Token`).

Required env vars (see README):

```shell
ReplicateAiServiceOptions__Token=
GoogleApplicationCredentials=
ApiKeyMiddlewareOptions__ApiKeys=
LocalAiImageServiceOptions__BaseUrl=
LocalAiImageServiceOptions__ApiKey=
```

### Model decisions

Prompt model choice is documented in [IMAGE_PROMPT_MODEL_DECISION.md](./IMAGE_PROMPT_MODEL_DECISION.md):

- **Prompts:** Replicate `openai/gpt-5-mini`
- **Pixels (default):** LocalAI `flux.2-klein-4b`
- **Pixels (fallback):** Replicate `black-forest-labs/flux-2-klein-4b` when `ImageProvider=replicate`

Do not change prompt model without eval and updating the decision doc.

### Deploy

- Docker + Kubernetes via `ansible/`
- GitHub Actions: `.github/workflows/build-deploy.yml`
- CORS allows `localhost:4200` and `localhost:44351` for admin UI dev

### Testing patterns

Unit tests mock HTTP clients and Firebase. `ImageGenerationService.GenerateImagesAsync` is `internal` and tested directly. Use `MockHttp` for Replicate/LocalAI HTTP calls.
