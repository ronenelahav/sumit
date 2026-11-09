# SUMIT Transaction Exporter

A local Blazor Server app that signs in to a financial provider in a real browser window (Playwright),
retrieves the previous calendar month's transactions, normalizes them and lets you download them as JSON.

This is a take-home assignment. **Isracard and Mizrahi Tefahot run against the real websites.**
There is no synthetic demo data. See [Provider status](#provider-status).

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (LTS). Check with `dotnet --version`; it should start with `10.`. Any installed 10.0 patch is enough.
- A browser for the sign-in window. On Windows the app uses the installed Google Chrome, then Microsoft Edge, and Playwright's own Chromium only if neither starts. On macOS and Linux it uses Playwright's Chromium (installed once, see below), then Chrome or Edge.
- A desktop session on the same computer. The sign-in window opens on the machine that runs `dotnet run`. Run it on Windows or macOS, not inside WSL, so that window is visible.

## Run on your computer

Clone the repository, then from its root:

**Windows (PowerShell)**

```powershell
dotnet build
dotnet run --project src\Sumit.TransactionExporter
```

There is no separate browser to install when Chrome or Edge is present.

**macOS or Linux**

```bash
dotnet build
pwsh src/Sumit.TransactionExporter/bin/Debug/net10.0/playwright.ps1 install chromium
# Linux only: add --with-deps so Chromium gets the system libraries it needs.
dotnet run --project src/Sumit.TransactionExporter
```

`pwsh` is PowerShell 7 (`dotnet tool install --global PowerShell` if you do not have it). The install
step is once per machine; run it again only after the Playwright package version changes. It prints a
Chromium download; a prompt that returns with no text means the browser was not installed.

If the sign-in window does not open, this opens one for 15 seconds and prints why each browser was skipped:

```powershell
dotnet run --project src\Sumit.TransactionExporter -- --check-browser
```

The app listens on http://localhost:5287 and opens that page in your browser. It binds to this computer only.

1. **Choose a provider.** Isracard or Mizrahi Tefahot.
2. **Check the period.** It is always the previous calendar month (for example, run on 8 Nov 2026,
   it is 1–31 Oct 2026).
3. **Connect.** A browser window opens on the provider's real sign-in page. Sign in there yourself,
   including any SMS code and, for Isracard, Cloudflare's "verify you are human" check if it appears.
   You have 5 minutes, and **Cancel** closes the window.
4. **Review and download.** The app shows the count, the period and a preview, and **Download JSON**
   saves the file through your browser. Nothing is written on the server.

### Export format

Same schema for every provider: UTF-8, ISO dates, numeric amounts, ordered by date.
Negative amounts are money out, positive amounts are money in.

```json
[
  { "date": "2026-10-05", "amount": -125.50, "description": "Grocery purchase" },
  { "date": "2026-10-10", "amount": 3500.00, "description": "Incoming transfer" }
]
```

## Tests

```bash
dotnet test
```

Ten unit tests, chosen for the main financial and reliability risks rather than for coverage: the
previous-month calculation across a year boundary, date and amount parsing with masking, the card
sign convention, period filtering, a malformed row failing the whole batch, the Isracard and Mizrahi
response mapping, an Isracard response without its transaction list, browser-session disposal after a
failed sign-in, and the JSON schema. All test data is synthetic, and no test signs in to a real provider;
that is checked manually.

## Architecture

One web project with folders as layers, plus one test project. More projects would add ceremony
without a real boundary for an app this size.

```
src/Sumit.TransactionExporter/
  Domain/          FinancialProvider, Transaction, DateRange
  Application/     TransactionExportService (workflow), TransactionNormalizer, SensitiveDataMasker,
                   RawTransaction/RawTransactionFormat, ExtractionException, Abstractions/
  Infrastructure/
    Browser/       Playwright session and factory, manual sign-in helper
    Providers/     IProviderAuthenticator, ITransactionScraper, ProviderRegistry,
                   BrowserTransactionSource, Isracard/, Mizrahi/
    Export/        JsonTransactionExporter
  Components/      Blazor UI; talks only to TransactionExportService
```

The flow for one extraction:

1. The page calls `TransactionExportService.ExtractAsync(provider, period, progress, token)`.
2. `BrowserTransactionSource` (behind `ITransactionSource`) asks `ProviderRegistry` for the provider's
   authenticator and scraper, opens a new browser session, runs the authenticator and then the
   scraper in that session, and disposes the session whatever happens.
3. The scraper returns provider values as `RawTransaction`s plus the provider's `RawTransactionFormat`
   (date formats, separators, sign convention).
4. `TransactionNormalizer` parses, applies the sign convention, cleans and masks descriptions,
   filters to the period and orders by date.
5. `JsonTransactionExporter` writes the file, streamed to the browser download.

### Design decisions

- **Blazor Server rather than a console app.** Sign-in is manual, so the user needs a clear place to
  start, follow progress, cancel, review the result and download it. Blazor Server keeps the whole
  workflow and the Playwright browser in the server process on this computer and pushes progress to
  the page, with no separate API. The cost is that the app is local-only: the sign-in window opens
  where the server runs.
- **Strategy per provider, two strategies each.** Signing in (`IProviderAuthenticator`) and reading
  data (`ITransactionScraper`) change for different reasons: a new sign-in page or verification step
  versus a new data endpoint or response shape. They share only the browser session.
- **Registry instead of a factory framework.** `ProviderRegistry` takes the injected collections and
  matches by `FinancialProvider`. A missing implementation is an error; there is no fallback.
- **Interfaces only where something varies or needs faking:** provider strategies, the transaction
  source, the exporter (it owns the file schema) and the browser session. The normalizer, the
  workflow service and the registry are concrete classes.
- **Provider data endpoints where available.** Isracard and Mizrahi load their own pages from JSON
  endpoints; reading those inside the signed-in browser avoids parsing markup, which changes more
  often. They are internal, undocumented endpoints, so they can change without notice. Mappers are
  separate from scrapers so that a change is fixed and tested in one place.
- **One browser per extraction.** Each session starts its own Playwright driver, browser and
  in-memory context, and is disposed with `await using`. Nothing is shared between users or runs,
  and no cookies or tokens survive. Starting a browser costs about a second per run, which is fine here.
- **Lifetimes.** Services are transient and stateless; `TimeProvider` is a singleton. Per-user
  state (result, running flag, cancellation) lives in the page component, which is scoped to the
  Blazor circuit. Handlers of one circuit never run in parallel, but a click can be handled while
  another handler awaits; `ConnectAsync` therefore sets the running flag before its first `await`,
  and Connect, provider selection and Start over are blocked while a run or a download is in progress.
- **Cancellation.** Playwright for .NET does not take a `CancellationToken`. The sign-in wait (the
  only long wait) uses `Task.WaitAsync(timeout, token)`, and disposing the session ends the
  abandoned Playwright call. Scraping steps have a 30-second timeout and check the token between
  steps. Closing the app's tab ends the Blazor circuit, which also cancels the run.
- **Display names and sign-in URLs are configuration** (`Providers` in `appsettings.json`).
- **One exception type for expected failures.** `ExtractionException` carries a message that is safe
  to show the user and to log. Playwright errors are translated at the boundary, for example "the
  browser window was closed" or "the website did not respond as expected".

### Failure policy

- A malformed row (unparseable date or amount, empty description) **fails the whole extraction** with
  the row number and field, never the value. A partial financial export that looks complete is
  worse than none.
- Rows outside the period are not errors; they are left out and counted in the UI.
- A response without the expected transaction structure fails instead of being read as "no
  transactions": an Isracard response without its transaction list, a Mizrahi response without its
  table, a Mizrahi row that is not marked as transaction or non-transaction, or a partial Mizrahi page.

### Credit cards and billing cycles

Card statements are grouped by billing cycle; the export is by calendar month. Billing dates are
never used as transaction dates.

- **Isracard:** reads the billing months that can contain the period's purchases (the period's
  month and the two after it, never past next month's upcoming charges) and keeps rows dated in the
  period. Isracard gives every row the original purchase date. For installment 2 and later the
  exported date is calculated as `purchase date + (installment number - 1) months`: the month that
  installment is allocated to, not the original purchase date and not necessarily the posting date.
  Without it, every installment read from the later billing months would be counted in the purchase
  month. The same adjustment is made by `fixInstallments` in israeli-bank-scrapers. The description
  includes Isracard's installment note (for example `תשלום 2 מתוך 3`). Amounts are the charged amount.

### Isracard integration

- **Sign-in:** the authenticator opens the configured sign-in page and does not touch the form.
  After sign-in Isracard sends the user to its newer site (`web.isracard.co.il/StatusPage`). Each
  time the window lands on an Isracard page outside the sign-in pages, the app asks Isracard's data
  endpoint whether there is a session (`DashboardMonth`; status `1` = signed in, `-2` = no session).
- **Data:** the scraper requests `CardsTransactionsList` for each billing month by navigating a
  second tab of the same signed-in browser to the endpoint on `digital.isracard.co.il`. The browser
  would block those requests from a `web.isracard.co.il` page. Because it is the user's own browser,
  the requests carry the session and pass Cloudflare; plain HTTP clients and headless browsers do not.
- **Source of the endpoint and field names:** the open-source
  [israeli-bank-scrapers](https://github.com/eshaham/israeli-bank-scrapers) project
  (`base-isracard-amex.ts`). The mapping is isolated in `IsracardTransactionMapper`.
- **Verified against a real account:** the post-sign-in address, and the shape of the signed-in
  `DashboardMonth` and `CardsTransactionsList` responses (field names and value formats only:
  `dd/MM/yyyy` dates, amounts as strings such as `"123.45"`, installment notes such as
  `תשלום 2 מתוך 3`). The synthetic test responses follow that shape. Not verified: what Isracard
  returns for a billing month without any activity (see [Known limitations](#known-limitations)).

### Mizrahi Tefahot integration

- **Sign-in:** after sign-in the bank moves to its online app (`mto.mizrahi-tefahot.co.il/OnlineApp/`).
  The authenticator waits for the app's link to the current-account section, which the sign-in pages
  do not have.
- **Data:** the scraper opens the current-account transactions page the way a user would, captures
  the request the page makes for its table (`/Online/api/SkyOSH/get428Index`, with the page's
  `mizrahixsrftoken` anti-forgery header), and sends it again from the page with the period's dates.
- **Rows:** `MC02PeulaTaaEZ` (date, `yyyy-MM-ddTHH:mm:ss`), `MC02SchumEZ` (signed amount, negative =
  money out, the same convention as the export) and `MC02TnuaTeurEZ` (description). Rows with
  `RecTypeSpecified: false`, such as the balance line, are not transactions. The bank may return a
  few rows from before the requested start date; the normalizer leaves them out.
- **Source and verification:** endpoint and fields follow israeli-bank-scrapers (`mizrahi.ts`) and
  were checked against a real signed-in session (field names and value formats only).
- **Limitation:** reads the account currently selected in the bank's app. Pending (not yet posted)
  transactions are not included.

## Provider status

| Provider | Status | Notes |
|---|---|---|
| Isracard | Live | Real website; manual sign-in, data from Isracard's own JSON endpoint (see above) |
| Mizrahi Tefahot | Live | Real website; manual sign-in, current-account (עובר ושב) transactions (see above) |

## Extending

**Adding a provider** (for example Bank Hapoalim or MAX):

1. Add a `FinancialProvider` value.
2. Add `Infrastructure/Providers/<Name>/` with an `IProviderAuthenticator` (open the sign-in page
   with `ManualSignIn.OpenSignInPageAsync`, wait for a signal that only exists after sign-in with
   `ManualSignIn.WaitForUserAsync`) and an `ITransactionScraper` that declares the provider's
   `RawTransactionFormat`. Keep JSON-to-row mapping in a separate mapper class when the provider
   returns JSON.
3. Add `Providers:<Name>` (`DisplayName`, `LoginUrl`) to `appsettings.json`.
4. Register it with one `AddProvider<TAuthenticator, TScraper>(provider, kind, configuration)` line in
   `InfrastructureServiceCollectionExtensions`.
5. Add a mapper test with a synthetic response.

`TransactionExportService`, `BrowserTransactionSource`, `TransactionNormalizer`, the exporter and the
UI do not change; the Mizrahi integration was added this way. Writing the two classes needs the real
sign-in URL, an account the developer may legitimately use, and the real page structure or endpoints.
A card provider such as MAX reuses the session handling, sign-in wait, normalizer (with
`SignConvention.PositiveIsOutgoing`) and exporter; its billing-month and installment rules stay in its
own scraper and mapper, as Isracard's do, until real duplication appears.

**Several cards or accounts.** Today Isracard exports every card in its response (one `Index<n>` entry
per card) and Mizrahi exports the account currently selected in the bank's app. Choosing cards or
accounts would stay inside each provider: Isracard's dashboard request already lists the cards, and
Mizrahi would have to switch the selected account in its app before each transactions request. Keeping
the source of each row would be an internal field on `RawTransaction`/`Transaction`; the export file
stays unchanged because `JsonTransactionExporter` owns the three-field schema. Choosing *after* sign-in
needs a pause between `AuthenticateAsync` and `ScrapeAsync` in `BrowserTransactionSource`, with the
browser kept open while the user decides; none of this is implemented.

**Other sign-in flows.** An authenticator only returns once the user is signed in, so SMS codes,
approval in a mobile app or a CAPTCHA are all completed by the user in the provider's window while it
waits. The app never handles credentials or codes.

**XML export.** Add an `XmlTransactionExporter : ITransactionExporter`; scraping and normalization stay
unchanged. Some orchestration would change: the service takes a single exporter and builds a `.json`
file name, and the page passes the `application/json` content type and says "Download JSON". Supporting
both formats means choosing the exporter by format and moving the extension and content type onto it.

## Security and privacy

- Credentials are typed only into the provider's own page in the browser window; the app has no
  credential fields.
- Each run uses a fresh, in-memory browser profile that is discarded afterwards. No cookies,
  tokens or storage state are saved.
- No CAPTCHA or MFA bypass: the user completes those steps.
- Transactions stay in memory for the current browser tab and are streamed directly to the
  download. They are not logged or written to disk on the server.
- Logs record the workflow: provider, period, stages, row and transaction counts, duration, and for
  failures the stage, the user-facing `ExtractionException` message and the error type (unexpected
  errors: type and stack trace). They never contain amounts, descriptions, responses, page content or
  Playwright messages. Browser launch paths and launch errors can contain the Windows user name, so
  they are logged at Debug only, which `--check-browser` shows on the console.
- Long numbers in descriptions (8 or more digits, such as card, account, IBAN or phone numbers) are
  masked to their last 4 digits in the preview and the export. A digit-only date in a description is
  masked too; over-masking is the safer mistake. This is a heuristic, not full personal-data removal.
- No tracing, screenshots or HAR files are recorded. `.gitignore` excludes exports and browser
  artifacts. All test data is synthetic.
- Binds to localhost only, with no telemetry and no external services.

## Known limitations

- Isracard and Mizrahi Tefahot are the only live providers. Bank Hapoalim is not integrated yet.
- Both integrations depend on internal data endpoints, which can change without notice. The Isracard
  sign-in window may show a Cloudflare check that the user has to complete.
- Isracard: the shape of a billing month without any activity has not been seen. If it lacks the
  transaction list, that run fails with a clear message instead of exporting an empty month.
- Isracard abroad purchases use Isracard's charged amount. The export has no currency field, and it
  has not been verified that every card charges abroad purchases in shekels (israeli-bank-scrapers
  reads a separate charged currency).
- Only the previous calendar month is offered; there is no custom date range.
- The browser window opens on the machine running the server, so the app is meant for local use only.
- Masking does not recognize names or addresses in descriptions.

## What would change for production use

- More providers, each with recorded synthetic fixtures of its real responses so that site changes
  are caught by tests, and a wider test suite as the code grows.
- A desktop shell or separate local agent for the browser, if the UI ever needs to run elsewhere.
- Alerting when a provider's response shape changes.
- Optional custom date ranges, and billing-cycle export for card providers if users need it.
