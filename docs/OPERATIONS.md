# WhaleTracker Operations

This document describes how to run and operate the current WhaleTracker AI trader stack.

## Runtime Services

- API: ASP.NET Core, default local URL `http://localhost:5090`
- Database: PostgreSQL from `docker-compose.yml`, default port `5432`
- Admin UI: `/admin.html`
- Swagger in development: `/swagger`

## Required Environment

The API loads `.env` files from the project directory and parents. Do not commit `.env`.

Core keys:

- `POSTGRES_CONNECTION`
- `GROQ_API_KEY`
- `GROQ_MODEL`
- `ALCHEMY_API_KEY`
- `ETHERSCAN_API_KEY`
- `OKX_API_KEY`
- `OKX_SECRET_KEY`
- `OKX_PASSPHRASE`
- `OKX_IS_DEMO`
- `ZERION_API_KEY`

Optional Telegram notifications:

- `TELEGRAM_BOT_TOKEN`
- `TELEGRAM_CHAT_ID`
- `TELEGRAM_ENABLED`

## Admin Workflows

1. Login at `/login.html`.
2. Check Provider Health.
3. Add tracked wallets manually or promote insider candidates from historical scans.
4. Run historical scans from the Historical Insider Scan panel.
5. Use Runtime Control:
   - `Enable Auto` starts periodic tracked-wallet scanning.
   - `Disable` stops automatic scanning.
   - `Scan Now` runs one immediate scan without waiting for the polling interval.
6. Watch OKX Account, Open Positions, Recent Executions, AI Bias, and AI Memory Events.

## Historical Insider Detection

The current scanner uses Etherscan logs over selected Uniswap V3 pools:

- WETH/USDC
- WETH/USDT
- WBTC/USDC
- WBTC/USDT
- LINK/USDC

Detection logic:

- Looks for wallets selling risk assets into stables before the manipulation window.
- Looks for the same wallets buying risk assets back during the dip/recovery window.
- Estimates matched round-trip profit.
- Scores timing, size, and profit.
- Candidates can be promoted into tracked wallets.

## AI Memory

AI decisions update a persistent aggregate bias state:

- Stable/no-directional moves do not move bias.
- Moves under 5 percent of wallet value are treated as noise.
- Large LONG decisions increase bullish bias.
- CLOSE/SELL decisions decrease bias.
- Recent decision memory is injected into future Groq prompts.

## Live Trading Notes

OKX execution is controlled by:

- Runtime Control `autoTradingEnabled`
- Manual event processing
- Manual scan trigger

When enabled, the pipeline is:

tracked wallet event -> AI decision -> bias memory -> OKX execution -> trade log -> optional Telegram notification

No hidden dry-run layer is currently added in this implementation. If OKX keys are live and the AI emits a tradeable signal, the system can place an OKX order.

## Known Provider Status

- Groq: working with `llama-3.3-70b-versatile`.
- Alchemy: working on Ethereum mainnet.
- OKX: live account read works.
- Zerion: code is implemented, but the latest observed API response was payment/API access failure. The app falls back to Alchemy wallet activity where possible.

## Verification Commands

```bash
dotnet build --no-restore
dotnet test tests/WhaleTracker.Tests/WhaleTracker.Tests.csproj --no-restore
docker compose up -d postgres
dotnet run --project src/WhaleTracker.API/WhaleTracker.API.csproj --urls http://localhost:5090
```
