# Technical design

Design notes for the main abstractions and the reasons behind them, kept up to date with the code.
The README describes behaviour, setup and the folder layout.

## Solution structure

One Blazor Web App (Interactive Server) plus one xUnit test project. Layers are folders, not
projects: the app is small, runs locally, and separate assemblies would add ceremony without
adding a boundary anyone needs to deploy or version independently.

## Main abstractions

| Abstraction | Layer | Why it exists |
|---|---|---|
| `ITransactionSource` | Application | Lets the workflow fetch raw transactions without knowing about Playwright. |
| `ITransactionExporter` | Application | Export format is a natural extension point; JSON is the only implementation. |
| `IProviderAuthenticator` | Infrastructure | Strategy: one implementation per provider. |
| `ITransactionScraper` | Infrastructure | Strategy: one implementation per provider, separate from authentication. |
| `IBrowserSession` / `IBrowserSessionFactory` | Infrastructure | Owns the Playwright lifetime; faked to test resource disposal. |

Concrete classes without interfaces: `TransactionExportService`, `TransactionNormalizer`,
`ProviderRegistry`. They have a single implementation and are easy to test directly.

## Provider selection and resolution

- `FinancialProvider` enum identifies providers; no strings.
- Each provider registers its authenticator and scraper in DI, plus a `ProviderInfo`
  (display name from configuration, `ProviderKind` declared in code).
- `ProviderRegistry` receives `IEnumerable<IProviderAuthenticator>` and
  `IEnumerable<ITransactionScraper>`, matches them by `Provider`, and throws if one is missing.
  No fallback, no service locator.

## Session ownership

- `BrowserTransactionSource` creates one `IBrowserSession` per extraction (`await using`),
  passes it to the authenticator and then the scraper, and disposes it in all outcomes.
- Each session is a fresh Playwright instance, browser and non-persistent context, so no
  cookies or storage survive the run, and two users or tabs never share a browser.
- Playwright for .NET does not accept `CancellationToken`. The long wait (manual sign-in) uses
  `Task.WaitAsync(timeout, token)`; disposing the session ends the abandoned Playwright call.
  Scraping steps use short Playwright timeouts and check the token between steps.

## Raw to normalized

1. The scraper returns `RawTransaction` strings exactly as the provider supplies them (both current
   providers return JSON), optionally with a debit or credit hint for providers that use separate
   columns. It also declares the provider's `RawTransactionFormat` (date formats, number
   separators, sign convention).
2. `TransactionNormalizer` parses dates and amounts, applies direction, cleans and masks the
   description, filters to the requested period, and orders by date (stable).
3. Sign convention in the output: negative = money out, positive = money in.
4. Failure policy: any malformed row fails the whole extraction with a message naming the row
   number and field (never its value), and a provider response without the expected transaction
   structure fails in its mapper. A partial financial export is worse than none.

## Dependency injection

- `AddApplication()` and `AddInfrastructure(configuration)` extension methods; `Program.cs`
  stays short.
- Stateless services are transient. `TimeProvider.System` is a singleton. Nothing holds a
  browser across requests.
- Per-user state (current result, running flag, cancellation source) lives in the page
  component, which is scoped to the Blazor circuit.

## Providers

The shipped providers are Isracard and Mizrahi Tefahot, both against the real websites. Sign-in
URLs and display names come from configuration. There is no synthetic demo site.

## Avoided on purpose

Separate projects per layer, MediatR/CQRS, repositories, a database, a REST API, a generic
provider factory, background job frameworks, a custom exception hierarchy (one
`ExtractionException` carries a user-safe message).
