# hn-stories-api

ASP.NET Core Minimal API (.NET 10) that serves the best N stories from Hacker News, sorted by score descending, with in-memory caching, global concurrency limiting, and a Polly resilience pipeline.

---

## How to Run

### Option A – .NET SDK

**Prerequisites:** .NET 10 SDK

```bash
git clone <repo-url>
cd hn-stories-api

dotnet build
dotnet test
cd src/HackerNewsApi.Api
dotnet run
```

After last command you should see such picture:

<img width="3836" height="1991" alt="image" src="https://github.com/user-attachments/assets/4a0e4dc8-ac24-456f-a826-50839dcdab3b" />

- Swagger UI: `https://localhost:{port}/swagger`
- OpenAPI doc: `https://localhost:{port}/openapi/v1.json`

### Option B – Docker (preferred)

**Prerequisites:** Docker / Docker Desktop

```bash
cd hn-stories-api
docker compose up --build
```

- API: `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger`
- OpenAPI doc: `http://localhost:5000/openapi/v1.json`

**Example requests:**
```
GET /api/stories/best?n=10
GET /api/stories/best?n=5&skip=10
```

---

## API Usage

### Endpoint

```
GET /api/stories/best?n={count}&skip={skip}
```

| Parameter | Type | Required | Default | Constraints |
|-----------|------|----------|---------|-------------|
| `n`       | int  | Yes      | –       | 1 ≤ n ≤ 200 |
| `skip`    | int  | No       | 0       | ≥ 0         |

### Success Response – 200 OK

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01+00:00",
    "score": 1757,
    "commentCount": 588
  }
]
```

- Sorted by `score` descending.
- `uri` is `null` for Ask HN posts (no URL).
- `commentCount` is `0` when `descendants` is absent.
- Stories with null score are excluded.

### Error Responses

All errors return `ProblemDetails` JSON (RFC 9457):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110",
  "title": "Bad Request",
  "status": 400,
  "detail": "Parameter 'n' is invalid. Must be between 1 and 200 (MaxStoryCount).",
  "traceId": "00-abc123..."
}
```

| Status | When |
|--------|------|
| 200 | Success – array of stories |
| 400 | `n` missing/invalid/out of range; `skip` negative or non-integer |
| 503 | Upstream HN API unavailable or timeout |
| 500 | Unexpected server error |

---

## How the API Handles High Load Without Overloading Hacker News

```
Client → Our API → ID cache [Hit] → Story cache [Hit] → Return
                          [Miss] → HN /beststories.json
                                  → Story cache [Hit] → Return
                                               [Miss] → Global SemaphoreSlim
                                                      → HN /item/{id}.json (Polly)
                                                      → Cache story → Return
```

1. **Two-tier in-memory caching (`IMemoryCache`)** – Story IDs are cached for 30 seconds; individual story details for 5 minutes (both configurable). When caches are warm, incoming requests require zero outbound HTTP calls.

2. **Global concurrency throttling (`SemaphoreSlim`)** – A Singleton semaphore caps total in-flight outbound calls to HN at 20 concurrent story-detail fetches (configurable), regardless of how many incoming requests arrive simultaneously.

3. **`IHttpClientFactory`** – Manages `HttpClient` lifetimes, connection pooling, and DNS refresh. Prevents socket exhaustion under high load.

4. **Standard resilience pipeline** via `AddStandardResilienceHandler()` – Retry with exponential backoff (3 attempts), circuit breaker (threshold 5, break 30s), and total timeout (30s). `HttpClient.Timeout` is set to infinite – Polly owns all timeout control. All thresholds configurable via `appsettings.json`.

5. **Graceful degradation** – Deleted/dead stories and stories with null score are silently skipped. If `n` exceeds available stories, we return what we have.

6. **`CancellationToken` propagation** – If a client disconnects, all in-flight HTTP calls to HN API are cancelled immediately, freeing resources.

---

## Architecture

Layered structure – three source projects, strict one-way dependencies:

- **`HackerNewsApi.Api`** – Minimal API endpoints, global exception middleware, DI wiring, OpenAPI. `Program.cs` delegates registration to extension methods.
- **`HackerNewsApi.Application`** – Business logic (`StoryService`), interfaces (`IStoryService`, `IHackerNewsClient`), DTOs, `StoryMapper`, custom exceptions. No external dependencies.
- **`HackerNewsApi.Infrastructure`** – `HackerNewsClient` (HTTP, Polly), `CachedHackerNewsClient` (decorator pattern), `HackerNewsApiSettings` (validated at startup with `ValidateOnStart()`).

```
Api → Application ← Infrastructure
```

Test projects:
- **`HackerNewsApi.UnitTests`** – xUnit + Moq + FluentAssertions (33 tests)
- **`HackerNewsApi.IntegrationTests`** – `WebApplicationFactory` + `FakeHackerNewsClient` (21 tests)

---

## Assumptions

1. The `/v0/beststories.json` endpoint returns IDs in a ranked order, but we re-sort by `score` descending as required by the spec.
2. Stories with null/missing `score` are excluded from results.
3. Deleted or dead stories (returning null from HN API) are silently skipped.
4. The `time` field uses `DateTimeOffset`, serialized as ISO 8601 (e.g. `"2019-10-12T13:43:01+00:00"`).
5. The `url` field may be null/empty for Ask HN posts; we return `uri: null` in that case.
6. Cache TTL of 5 minutes (stories) and 30 seconds (IDs) balances freshness with protection of the upstream API.
7. Pagination via `skip` uses offset-based approach on the full sorted result set.
8. Validation is split: model binding handles type errors (`n` missing or non-integer); service layer handles range checks (`n` 1–MaxStoryCount, `skip` ≥ 0).
9. `traceId` is included in all `ProblemDetails` responses for debuggability.
