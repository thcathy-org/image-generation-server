# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## About

ASP.NET Core 7 microservice for AI vocabulary image generation on [funfunspell.com](https://www.funfunspell.com). Prompts via Replicate LLM; pixels via LocalAI Flux (default) or Replicate Flux; storage in Google Firebase.

## Commands

```bash
# Build and test
dotnet build ImageGenerationServer/ImageGenerationServer.csproj
dotnet test ImageGenerationServer.UT/ImageGenerationServer.UT.csproj

# Run API (from repo root)
dotnet run --project ImageGenerationServer/ImageGenerationServer.csproj
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
  Controllers/                  # ImageController
  Services/                     # Generation, Replicate, LocalAI, Firebase, image remove
  Middleware/                   # ApiKeyMiddleware
ImageGenerationServer.UT/       # Unit tests
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
   - Uploads JSON `{ images: [base64...] }` to Firebase. New images are available without a review step. This service does not read or write `isVerify`, and it does not bulk-rewrite existing Firebase files to strip that old field.
3. **Remove** — `POST /image/remove` with `{ phrase, imageIndex }` drops that index when other images remain, and deletes the phrase file when that index is the only image. Success is returned only after the Firebase write or delete succeeds. A blank phrase or negative index returns 400. A missing file, missing index, or unreadable JSON returns 404. A Firebase failure returns 500. There is no database and no admin UI.

Firebase path: `{first-two-chars}/{slug}.json` (see `StringExtension.GetImageFilePath`).

### Key services

| Service | Responsibility |
|---------|----------------|
| `ReplicateAiService` | LLM prompts + optional Replicate Flux image generation |
| `LocalAiImageService` | LocalAI Flux image generation (default pixel provider) |
| `FirebaseService` | Upload/download/delete JSON objects in GCS bucket |
| `ImageRemovalService` | `POST /image/remove` drops one image, or deletes the phrase file when that index is the only image |
| `ImageGenerationService` | Background queue processor. Uploads new images |

### API endpoints

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/image/generate/{phrase}` | Enqueue phrase for generation |
| POST | `/image/remove` | Drop `imageIndex`, or delete the phrase file when it is the only image |
| GET | `/healthz` | Health check |

`/image/*` requires the **`X-API-KEY`** header (see `ApiKeyMiddleware`). `/healthz` does not.

### Configuration (`appsettings.json`)

| Section | Key settings |
|---------|--------------|
| `ImageGenerationServiceOptions` | `ImageProvider`: `localai` (default) or `replicate` |
| `ReplicateAiServiceOptions` | `PromptModel`, `PromptMaxTokens`, `PromptTemperature`, `ImageModel`, `Token` |
| `LocalAiImageServiceOptions` | `BaseUrl`, `ApiKey`, `ImageModel`, `Size`, `RequestTimeoutSeconds` |
| `FirebaseServiceOptions` | `BucketName`, `FirebaseBaseFolder` |
| `ApiKeyMiddlewareOptions` | `ApiKeys`, `ApiKeyHeaderName` |

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

### Testing patterns

Unit tests mock HTTP clients and Firebase. `ImageGenerationService.GenerateImagesAsync` is `internal` and tested directly. Use `MockHttp` for Replicate/LocalAI HTTP calls.
