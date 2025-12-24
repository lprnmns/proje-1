# 🐋 WhaleTracker

Balina cüzdan takip ve kopya ticaret sistemi.

## 📁 Proje Yapısı

```
WhaleTracker/
├── src/
│   ├── WhaleTracker.API/          # Web API (Controllers, Program.cs)
│   │   └── Controllers/
│   │       ├── DashboardController.cs   → Anlık durum
│   │       ├── TradesController.cs      → İşlem geçmişi
│   │       └── WhaleController.cs       → Balina takibi
│   │
│   ├── WhaleTracker.Core/         # Modeller ve Interface'ler
│   │   ├── Models/
│   │   │   ├── WhaleStats.cs           → Balina portföyü
│   │   │   ├── UserStats.cs            → Kullanıcı durumu
│   │   │   ├── TransactionEvent.cs     → Balina işlemi
│   │   │   ├── TradeSignal.cs          → AI kararı
│   │   │   └── TradeResult.cs          → İşlem sonucu
│   │   └── Interfaces/
│   │       ├── IZerionService.cs       → Zerion API
│   │       ├── IOkxService.cs          → OKX Futures API
│   │       ├── IDecisionEngine.cs      → AI Karar Motoru
│   │       └── IWhaleTrackerService.cs → Ana Orkestrasyon
│   │
│   ├── WhaleTracker.Data/         # Veritabanı (PostgreSQL)
│   │   ├── Entities/                   → DB tabloları
│   │   ├── Repositories/               → CRUD işlemleri
│   │   └── WhaleTrackerDbContext.cs    → EF Core Context
│   │
│   └── WhaleTracker.Infrastructure/  # Dış API Servisleri
│       └── Services/
│           ├── ZerionService.cs        → ⭐ KOD YAZ
│           ├── OkxService.cs           → ⭐ KOD YAZ
│           ├── DecisionEngine.cs       → ⭐ KOD YAZ
│           └── WhaleTrackerService.cs  → ⭐ KOD YAZ
│
├── docker-compose.yml             # PostgreSQL + pgAdmin
├── Dockerfile                     # API container
└── WhaleTracker.sln               # Solution dosyası
```

## 🚀 Hızlı Başlangıç

### 1. PostgreSQL'i Başlat
```bash
docker-compose up -d
```

### 2. Projeyi Restore Et
```bash
dotnet restore
```

### 3. API'yi Çalıştır
```bash
cd src/WhaleTracker.API
dotnet run
```

### 4. Swagger'a Git
http://localhost:5000

## 📝 Senin Kod Yazacağın Yerler

Tüm `NotImplementedException` olan metodlar senin için hazır bekliyor:

| Dosya | Metod | Açıklama |
|-------|-------|----------|
| `ZerionService.cs` | `GetWalletPortfolioAsync` | Zerion API'den portföy çek |
| `ZerionService.cs` | `GetRecentTransactionsAsync` | Son işlemleri çek |
| `OkxService.cs` | `GetAccountInfoAsync` | OKX hesap bilgisi |
| `OkxService.cs` | `ExecuteTradeAsync` | **ANA METOD** - Pseudo-code mantığı |
| `OkxService.cs` | `PlaceMarketOrderAsync` | Market emri gönder |
| `DecisionEngine.cs` | `AnalyzeAndDecideAsync` | AI'dan karar al |
| `WhaleTrackerService.cs` | `ScanAndProcessAsync` | Ana döngü |
| `TradeRepository.cs` | Tüm metodlar | DB işlemleri |

## 🔧 Yapılandırma

`appsettings.json` dosyasını düzenle:

```json
{
  "Zerion": {
    "ApiKey": "ZERION_API_KEY",
    "WhaleAddress": "0x..."
  },
  "Okx": {
    "ApiKey": "OKX_API_KEY",
    "SecretKey": "OKX_SECRET",
    "Passphrase": "OKX_PASSPHRASE",
    "IsDemo": true
  },
  "OpenAi": {
    "ApiKey": "OPENAI_API_KEY"
  }
}
```

## 🔄 İş Akışı

```
┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│   Zerion    │ ──▶ │  Decision    │ ──▶ │    OKX      │
│   Service   │     │   Engine     │     │   Service   │
│  (Balina)   │     │    (AI)      │     │  (İşlem)    │
└─────────────┘     └──────────────┘     └─────────────┘
       │                   │                    │
       ▼                   ▼                    ▼
┌─────────────────────────────────────────────────────┐
│                   PostgreSQL                         │
│   (TradeLogs, PnlHistory, ProcessedTransactions)    │
└─────────────────────────────────────────────────────┘
```

## 📊 API Endpoints

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| GET | `/api/dashboard/status` | Anlık durum |
| GET | `/api/dashboard/positions` | Açık pozisyonlar |
| GET | `/api/trades` | İşlem geçmişi |
| GET | `/api/whale/portfolio` | Balina portföyü |
| POST | `/api/whale/scan` | Manuel tarama |
