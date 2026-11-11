# ADR 0020: Google Gemini API as the AI provider

- Status: Accepted
- Date: 2026-11-11

## Context

The assignment asks for an endpoint that improves task descriptions and allows
"GitHub Models (Free) or any free AI provider". PRD 10 puts the provider behind a
strategy interface.

| Option | Free | Status (checked 2026-11-11) | Rejected because |
| --- | --- | --- | --- |
| GitHub Models | was free | GitHub docs: "As of July 30, 2026, GitHub Models has been fully retired" | No longer available |
| Ollama (local) | yes | available | Setup (model download, hardware, Bangla/Banglish quality) takes too long now (developer) |
| OpenAI API | no | available | Paid; the developer's ChatGPT subscription does not include API use |
| **Google Gemini API, free tier** | yes (Google AI Studio key) | available in Bangladesh | **Chosen** |

## Decision

- `IAiTextImprover` (Application) with one implementation, `GeminiTextImprover`
  (Infrastructure), calling the `generateContent` REST endpoint with a typed
  `HttpClient`; no SDK.
- Model `gemini-3.5-flash-lite` (Stable), configurable.
- The API key lives only in user-secrets or environment variables, is sent in a
  header (never in a URL) and is optional: without it the endpoint returns
  `ai.not_configured` and the rest of the API works.

## Consequences

- Free-tier terms: Google may use submitted text to improve its products and
  human reviewers may read it. Users are told not to include confidential or
  personal information; only title and description are sent.
- Free-tier rate limits are not published as numbers (shown in AI Studio); a
  limit hit returns `ai.unavailable` and the PM can retry.
- Another provider (Ollama, a paid tier) is a new `IAiTextImprover` plus
  configuration, without changes to the use case or the endpoint.
