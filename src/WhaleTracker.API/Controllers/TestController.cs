using Microsoft.AspNetCore.Mvc;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Core.Models;

namespace WhaleTracker.API.Controllers;

/// <summary>
/// Test Controller
/// API baÄŸlantÄ±larÄ±nÄ± test etmek iÃ§in
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IOkxService _okxService;
    private readonly IAIService _aiService;
    private readonly ILogger<TestController> _logger;

    public TestController(
        IOkxService okxService,
        IAIService aiService,
        ILogger<TestController> logger)
    {
        _okxService = okxService;
        _aiService = aiService;
        _logger = logger;
    }

    // ================================================================
    // KAPSAMLI TEST - TÃœM METODLARI TEST ET
    // ================================================================

    /// <summary>
    /// ğŸ§ª KAPSAMLI TEST - TÃ¼m OKX metodlarÄ±nÄ± test et
    /// GET /api/test/comprehensive
    /// </summary>
    [HttpGet("comprehensive")]
    public async Task<IActionResult> ComprehensiveTest()
    {
        var results = new List<object>();
        var startTime = DateTime.Now;

        _logger.LogInformation("ğŸ§ª KAPSAMLI TEST BAÅLIYOR...");

        // ================================================================
        // TEST 1: Hesap Bilgisi
        // ================================================================
        try
        {
            _logger.LogInformation("ğŸ“Š TEST 1: GetAccountInfoAsync");
            var userStats = await _okxService.GetAccountInfoAsync();
            
            results.Add(new
            {
                Test = "1. GetAccountInfoAsync",
                Status = "âœ… BAÅARILI",
                Data = new
                {
                    TotalBalanceUSD = userStats.TotalUsd,
                    DefaultLeverage = userStats.Leverage,
                    OpenPositionsCount = userStats.ActivePositions.Count
                }
            });
        }
        catch (Exception ex)
        {
            results.Add(new { Test = "1. GetAccountInfoAsync", Status = "âŒ HATA", Error = ex.Message });
        }

        // ================================================================
        // TEST 2: TÃ¼m Pozisyonlar
        // ================================================================
        List<Position> allPositions = new();
        try
        {
            _logger.LogInformation("ğŸ“Š TEST 2: GetAllPositionsAsync");
            allPositions = await _okxService.GetAllPositionsAsync();
            
            results.Add(new
            {
                Test = "2. GetAllPositionsAsync",
                Status = "âœ… BAÅARILI",
                PositionCount = allPositions.Count,
                Positions = allPositions.Select(p => new
                {
                    p.Symbol,
                    p.Direction,
                    MarginUSD = Math.Round(p.MarginUsd, 2),
                    p.EntryPrice,
                    p.Size,
                    PnL = Math.Round(p.UnrealizedPnl, 4)
                })
            });
        }
        catch (Exception ex)
        {
            results.Add(new { Test = "2. GetAllPositionsAsync", Status = "âŒ HATA", Error = ex.Message });
        }

        // ================================================================
        // TEST 3: BABY Pozisyonu (Long ve Short ayrÄ± ayrÄ±)
        // ================================================================
        try
        {
            _logger.LogInformation("ğŸ“Š TEST 3: GetPositionAsync(BABY)");
            var babyPosition = await _okxService.GetPositionAsync("BABY");
            
            // PozisyonlarÄ± direction'a gÃ¶re grupla
            var babyPositions = allPositions.Where(p => p.Symbol == "BABY").ToList();
            
            results.Add(new
            {
                Test = "3. GetPositionAsync(BABY)",
                Status = babyPosition != null ? "âœ… BAÅARILI" : "âš ï¸ POZÄ°SYON YOK",
                FirstPosition = babyPosition != null ? new
                {
                    babyPosition.Symbol,
                    babyPosition.Direction,
                    MarginUSD = Math.Round(babyPosition.MarginUsd, 2),
                    babyPosition.Size,
                    PnL = Math.Round(babyPosition.UnrealizedPnl, 4)
                } : null,
                AllBABYPositions = babyPositions.Select(p => new
                {
                    p.Direction,
                    MarginUSD = Math.Round(p.MarginUsd, 2),
                    p.Size,
                    PnL = Math.Round(p.UnrealizedPnl, 4)
                })
            });
        }
        catch (Exception ex)
        {
            results.Add(new { Test = "3. GetPositionAsync(BABY)", Status = "âŒ HATA", Error = ex.Message });
        }

        // ================================================================
        // TEST 4: KaldÄ±raÃ§ Ayarlama (DOGE iÃ§in test - kÃ¼Ã§Ã¼k coin)
        // ================================================================
        try
        {
            _logger.LogInformation("ğŸ“Š TEST 4: SetLeverageAsync(DOGE, 5)");
            var leverageResult = await _okxService.SetLeverageAsync("DOGE", 5);
            
            results.Add(new
            {
                Test = "4. SetLeverageAsync(DOGE, 5x)",
                Status = leverageResult ? "âœ… BAÅARILI" : "âš ï¸ UYARI",
                Message = leverageResult ? "DOGE kaldÄ±racÄ± 5x olarak ayarlandÄ±" : "KaldÄ±raÃ§ ayarlanamadÄ±"
            });
        }
        catch (Exception ex)
        {
            results.Add(new { Test = "4. SetLeverageAsync", Status = "âŒ HATA", Error = ex.Message });
        }

        // ================================================================
        // TEST 5: Pozisyon Ã–zeti (Long vs Short analizi)
        // ================================================================
        try
        {
            _logger.LogInformation("ğŸ“Š TEST 5: Pozisyon Analizi");
            
            var longPositions = allPositions.Where(p => p.Direction == "Long").ToList();
            var shortPositions = allPositions.Where(p => p.Direction == "Short").ToList();
            
            results.Add(new
            {
                Test = "5. Pozisyon Analizi",
                Status = "âœ… BAÅARILI",
                Summary = new
                {
                    TotalPositions = allPositions.Count,
                    LongCount = longPositions.Count,
                    ShortCount = shortPositions.Count,
                    TotalLongMargin = Math.Round(longPositions.Sum(p => p.MarginUsd), 2),
                    TotalShortMargin = Math.Round(shortPositions.Sum(p => p.MarginUsd), 2),
                    TotalLongPnL = Math.Round(longPositions.Sum(p => p.UnrealizedPnl), 4),
                    TotalShortPnL = Math.Round(shortPositions.Sum(p => p.UnrealizedPnl), 4)
                },
                LongPositions = longPositions.Select(p => $"{p.Symbol}: {p.MarginUsd:F2} USD, PnL: {p.UnrealizedPnl:F4}"),
                ShortPositions = shortPositions.Select(p => $"{p.Symbol}: {p.MarginUsd:F2} USD, PnL: {p.UnrealizedPnl:F4}")
            });
        }
        catch (Exception ex)
        {
            results.Add(new { Test = "5. Pozisyon Analizi", Status = "âŒ HATA", Error = ex.Message });
        }

        var totalTime = (DateTime.Now - startTime).TotalMilliseconds;

        return Ok(new
        {
            Title = "ğŸ‹ WhaleTracker KapsamlÄ± Test SonuÃ§larÄ±",
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            TotalDurationMs = Math.Round(totalTime, 0),
            TestCount = results.Count,
            Results = results
        });
    }

    // ================================================================
    // DEBUG: KONTRAT HESAPLAMA TESTÄ°
    // ================================================================

    /// <summary>
    /// ğŸ”§ DEBUG: USDT -> Kontrat dÃ¶nÃ¼ÅŸÃ¼mÃ¼nÃ¼ test et
    /// GET /api/test/debug/contracts?symbol=DOGE&usdt=2&leverage=3
    /// </summary>
    [HttpGet("debug/contracts")]
    public async Task<IActionResult> DebugContracts(
        [FromQuery] string symbol = "DOGE",
        [FromQuery] decimal usdt = 2,
        [FromQuery] int leverage = 3)
    {
        _logger.LogInformation("ğŸ”§ DEBUG: Kontrat hesaplama - {Symbol} {USDT} USDT {Leverage}x", 
            symbol, usdt, leverage);

        try
        {
            var (contracts, ctVal, price, notional, minSz, lotSz) = await _okxService.ConvertToContractsDebugAsync(symbol, usdt, leverage);
            
            // 1 TAM kontratÄ±n USD deÄŸeri
            var oneFullContractUsd = ctVal * price;
            // Minimum kontratÄ±n USD deÄŸeri (0.01 kontrat gibi)
            var minContractUsd = minSz * oneFullContractUsd;
            // Minimum margin gerekli (minSz kontrat iÃ§in)
            var minMarginRequired = minContractUsd / leverage;
            // AÃ§Ä±lacak kontratÄ±n USD deÄŸeri
            var positionValueUsd = contracts * oneFullContractUsd;
            // GerÃ§ek margin
            var actualMarginUsd = positionValueUsd / leverage;
            // AÃ§Ä±lacak coin miktarÄ±
            var coinAmount = contracts * ctVal;
            
            // Durum belirleme
            string status;
            var marginDiff = Math.Abs(actualMarginUsd - usdt);
            if (marginDiff <= usdt * 0.5m)
                status = "âœ… UYGUN - Margin doÄŸru hesaplandÄ±";
            else if (usdt >= minMarginRequired / 2)
                status = $"âš ï¸ UYARI - Minimum {minSz} kontrat aÃ§Ä±lacak ({Math.Round(actualMarginUsd, 4)} USDT margin)";
            else
                status = $"âŒ REDDEDÄ°LECEK - Minimum {Math.Round(minMarginRequired, 4)} USDT margin gerekli";

            return Ok(new
            {
                Title = "ğŸ”§ Kontrat Hesaplama Debug (minSz/lotSz ile)",
                Input = new
                {
                    Symbol = symbol,
                    RequestedMarginUSDT = usdt,
                    Leverage = leverage
                },
                ContractInfo = new
                {
                    CtVal = ctVal,
                    CtVal_Aciklama = $"1 tam kontrat = {ctVal} {symbol}",
                    MinSz = minSz,
                    MinSz_Aciklama = $"Minimum emir = {minSz} kontrat = {minSz * ctVal} {symbol}",
                    LotSz = lotSz,
                    LotSz_Aciklama = $"ArtÄ±ÅŸ miktarÄ± = {lotSz} kontrat",
                    CurrentPrice = price,
                    OneFullContract_USD = Math.Round(oneFullContractUsd, 2),
                    MinContract_USD = Math.Round(minContractUsd, 4),
                    MinMarginRequired = Math.Round(minMarginRequired, 4)
                },
                Calculation = new
                {
                    Step1 = $"{usdt} USDT * {leverage}x = {notional} USDT notional",
                    Step2 = $"1 tam kontrat = {ctVal} coin * ${price} = ${Math.Round(oneFullContractUsd, 2)}",
                    Step3 = $"{notional} / {Math.Round(oneFullContractUsd, 2)} = {Math.Round(notional / oneFullContractUsd, 6)} (ham)",
                    Step4 = $"lotSz ({lotSz}) ile yuvarla -> {contracts} kontrat"
                },
                Result = new
                {
                    Contracts = contracts,
                    CoinAmount = coinAmount,
                    CoinAmount_Aciklama = $"{contracts} kontrat * {ctVal} = {coinAmount} {symbol}",
                    PositionValueUSD = Math.Round(positionValueUsd, 4),
                    ActualMarginUSD = Math.Round(actualMarginUsd, 4),
                    RequestedMarginUSD = usdt,
                    Difference = Math.Round(actualMarginUsd - usdt, 4)
                },
                Status = status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Debug hatasÄ±!");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    // ================================================================
    // ğŸ—ï¸ YENÄ° MÄ°MARÄ° - ORDER CALCULATION
    // ================================================================

    /// <summary>
    /// ğŸ¯ ORDER CALCULATION - AI sinyali iÃ§in tam hesaplama
    /// GET /api/test/calculate-order?symbol=ETH&usdt=20&leverage=5&action=long
    /// 
    /// Bu endpoint iÅŸlem yapmadan Ã¶nce tÃ¼m hesaplamalarÄ± yapar:
    /// - Minimum kontrat kontrolÃ¼
    /// - lotSz'ye gÃ¶re yuvarlama
    /// - GerÃ§ek margin hesabÄ±
    /// - TÃ¼m validasyonlar
    /// </summary>
    [HttpGet("calculate-order")]
    public async Task<IActionResult> CalculateOrder(
        [FromQuery] string symbol = "ETH",
        [FromQuery] decimal usdt = 20,
        [FromQuery] int leverage = 5,
        [FromQuery] string action = "long")
    {
        _logger.LogInformation("ğŸ¯ ORDER CALCULATION: {Symbol} {USDT} USDT {Leverage}x {Action}", 
            symbol, usdt, leverage, action);

        try
        {
            var calculation = await _okxService.CalculateOrderAsync(symbol, usdt, leverage, action);

            // Status emoji belirleme
            var statusEmoji = calculation.ValidationStatus switch
            {
                OrderValidationStatus.Valid => "âœ…",
                OrderValidationStatus.ValidWithWarning => "âš ï¸",
                OrderValidationStatus.InsufficientMargin => "ğŸ’°",
                OrderValidationStatus.LeverageTooHigh => "ğŸ“Š",
                OrderValidationStatus.InstrumentNotFound => "ğŸ”",
                OrderValidationStatus.PriceUnavailable => "ğŸ’µ",
                OrderValidationStatus.Error => "âŒ",
                _ => "â“"
            };

            return Ok(new
            {
                Title = "ğŸ—ï¸ Order Calculation (Demir Mimari)",
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                
                Request = new
                {
                    calculation.Symbol,
                    calculation.RequestedMarginUSDT,
                    calculation.Leverage,
                    calculation.Action
                },
                
                Validation = new
                {
                    IsValid = calculation.IsValid,
                    Status = $"{statusEmoji} {calculation.ValidationStatus}",
                    Message = calculation.ValidationMessage,
                    Warnings = calculation.Warnings
                },
                
                InstrumentInfo = calculation.Instrument != null ? new
                {
                    calculation.Instrument.InstId,
                    calculation.Instrument.CtVal,
                    CtVal_Aciklama = $"1 tam kontrat = {calculation.Instrument.CtVal} {symbol}",
                    calculation.Instrument.MinSz,
                    MinSz_Aciklama = $"Minimum = {calculation.Instrument.MinSz} kontrat = {calculation.Instrument.MinCoinAmount} {symbol}",
                    calculation.Instrument.LotSz,
                    LotSz_Aciklama = $"ArtÄ±ÅŸ = {calculation.Instrument.LotSz} kontrat",
                    calculation.Instrument.MaxLeverage,
                    calculation.Instrument.LastPrice,
                    OneFullContractUSD = calculation.Instrument.OneFullContractUsd,
                    MinContractUSD = calculation.Instrument.MinContractUsd,
                    MinMarginForLeverage = calculation.Instrument.GetMinMarginForLeverage(leverage)
                } : null,
                
                Calculation = new
                {
                    calculation.Contracts,
                    calculation.CoinAmount,
                    CoinAmount_Aciklama = $"{calculation.Contracts} kontrat Ã— {calculation.Instrument?.CtVal} = {calculation.CoinAmount} {symbol}",
                    PositionValueUSD = calculation.PositionValueUSD,
                    ActualMarginUSD = calculation.ActualMarginUSD,
                    MarginDifference = calculation.MarginDifference,
                    MarginDifferencePercent = usdt > 0 ? Math.Round((calculation.MarginDifference / usdt) * 100, 2) : 0
                },
                
                Summary = calculation.IsValid 
                    ? $"âœ… Ä°ÅLEM YAPILABÄ°LÄ°R: {calculation.Contracts} kontrat ({calculation.CoinAmount} {symbol}), margin: {calculation.ActualMarginUSD:F4} USDT"
                    : $"âŒ Ä°ÅLEM YAPILAMAZ: {calculation.ValidationMessage}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Calculate order hatasÄ±!");
            return StatusCode(500, new { Error = ex.Message, Stack = ex.StackTrace });
        }
    }

    /// <summary>
    /// ğŸ” INSTRUMENT INFO - Coin bilgisini al
    /// GET /api/test/instrument?symbol=DOGE
    /// </summary>
    [HttpGet("instrument")]
    public async Task<IActionResult> GetInstrumentInfo([FromQuery] string symbol = "DOGE")
    {
        _logger.LogInformation("ğŸ” Instrument bilgisi: {Symbol}", symbol);

        try
        {
            var info = await _okxService.GetInstrumentInfoAsync(symbol);

            if (info == null)
            {
                return NotFound(new { Error = $"{symbol} iÃ§in instrument bulunamadÄ±" });
            }

            return Ok(new
            {
                Title = $"ğŸ” {symbol} Instrument Bilgisi",
                InstId = info.InstId,
                Symbol = info.Symbol,
                
                ContractSpec = new
                {
                    CtVal = info.CtVal,
                    CtVal_Aciklama = $"1 tam kontrat = {info.CtVal} {symbol}",
                    MinSz = info.MinSz,
                    MinSz_Aciklama = $"Minimum emir = {info.MinSz} kontrat = {info.MinCoinAmount} {symbol}",
                    LotSz = info.LotSz,
                    LotSz_Aciklama = $"Lot artÄ±ÅŸÄ± = {info.LotSz} kontrat",
                    TickSz = info.TickSz,
                    MaxLeverage = info.MaxLeverage
                },
                
                Price = new
                {
                    LastPrice = info.LastPrice,
                    PriceUpdatedAt = info.PriceUpdatedAt,
                    InfoUpdatedAt = info.InfoUpdatedAt
                },
                
                CalculatedValues = new
                {
                    OneFullContractUSD = info.OneFullContractUsd,
                    MinContractUSD = info.MinContractUsd,
                    MinMargin_5x = info.GetMinMarginForLeverage(5),
                    MinMargin_10x = info.GetMinMarginForLeverage(10),
                    MinMargin_20x = info.GetMinMarginForLeverage(20)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Instrument bilgisi hatasÄ±!");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// ğŸ§ª MULTI-COIN TEST - Birden fazla coin iÃ§in hesaplama
    /// GET /api/test/multi-calculate?usdt=10&leverage=5
    /// </summary>
    [HttpGet("multi-calculate")]
    public async Task<IActionResult> MultiCoinCalculate(
        [FromQuery] decimal usdt = 10,
        [FromQuery] int leverage = 5)
    {
        var symbols = new[] { "BTC", "ETH", "SOL", "DOGE", "XRP", "AVAX", "LINK", "PEPE" };
        var results = new List<object>();

        _logger.LogInformation("ğŸ§ª Multi-coin hesaplama: {USDT} USDT, {Leverage}x", usdt, leverage);

        foreach (var symbol in symbols)
        {
            try
            {
                var calc = await _okxService.CalculateOrderAsync(symbol, usdt, leverage, "long");
                
                var statusEmoji = calc.ValidationStatus switch
                {
                    OrderValidationStatus.Valid => "âœ…",
                    OrderValidationStatus.ValidWithWarning => "âš ï¸",
                    _ => "âŒ"
                };

                results.Add(new
                {
                    Symbol = symbol,
                    Status = $"{statusEmoji} {calc.ValidationStatus}",
                    IsValid = calc.IsValid,
                    Contracts = calc.Contracts,
                    CoinAmount = calc.CoinAmount,
                    ActualMarginUSD = Math.Round(calc.ActualMarginUSD, 4),
                    MarginDiff = Math.Round(calc.MarginDifference, 4),
                    Price = calc.Instrument?.LastPrice ?? 0,
                    MinSz = calc.Instrument?.MinSz ?? 0,
                    Warning = calc.Warnings.FirstOrDefault()
                });
            }
            catch (Exception ex)
            {
                results.Add(new
                {
                    Symbol = symbol,
                    Status = "âŒ HATA",
                    IsValid = false,
                    Error = ex.Message
                });
            }
        }

        return Ok(new
        {
            Title = "ğŸ§ª Multi-Coin Hesaplama Testi",
            RequestedMarginUSDT = usdt,
            Leverage = leverage,
            Results = results,
            Summary = new
            {
                Total = results.Count,
                Valid = results.Count(r => ((dynamic)r).IsValid == true),
                Invalid = results.Count(r => ((dynamic)r).IsValid == false)
            }
        });
    }

    // ================================================================
    // LIVE TRADE TESTLERI (DÄ°KKATLÄ° KULLAN!)
    // ================================================================

    /// <summary>
    /// ğŸ”¥ LIVE TEST: KÃ¼Ã§Ã¼k bir LONG pozisyon aÃ§
    /// POST /api/test/live/open-long?symbol=DOGE&usdt=1&leverage=2
    /// </summary>
    [HttpPost("live/open-long")]
    public async Task<IActionResult> LiveTestOpenLong(
        [FromQuery] string symbol = "DOGE",
        [FromQuery] decimal usdt = 1,
        [FromQuery] int leverage = 2)
    {
        _logger.LogWarning("ğŸ”¥ LIVE TEST: LONG POZÄ°SYON AÃ‡ILIYOR - {Symbol} {USDT} USDT {Leverage}x", 
            symbol, usdt, leverage);

        try
        {
            var signal = new TradeSignal
            {
                Symbol = symbol,
                Action = TradeAction.OPEN_LONG,
                MarginAmountUSDT = usdt,
                Leverage = leverage,
                Reason = "Live Test - Open Long"
            };

            var result = await _okxService.ExecuteTradeAsync(signal);

            return Ok(new
            {
                Test = "LIVE: Open Long",
                Signal = new { symbol, usdt, leverage, action = "OPEN_LONG" },
                Result = new
                {
                    result.Success,
                    result.OrderId,
                    result.Symbol,
                    result.Side,
                    result.Size,
                    result.ErrorMessage
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Live test hatasÄ±!");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// ğŸ”¥ LIVE TEST: KÃ¼Ã§Ã¼k bir SHORT pozisyon aÃ§
    /// POST /api/test/live/open-short?symbol=DOGE&usdt=1&leverage=2
    /// </summary>
    [HttpPost("live/open-short")]
    public async Task<IActionResult> LiveTestOpenShort(
        [FromQuery] string symbol = "DOGE",
        [FromQuery] decimal usdt = 1,
        [FromQuery] int leverage = 2)
    {
        _logger.LogWarning("ğŸ”¥ LIVE TEST: SHORT POZÄ°SYON AÃ‡ILIYOR - {Symbol} {USDT} USDT {Leverage}x", 
            symbol, usdt, leverage);

        try
        {
            var signal = new TradeSignal
            {
                Symbol = symbol,
                Action = TradeAction.OPEN_SHORT,
                MarginAmountUSDT = usdt,
                Leverage = leverage,
                Reason = "Live Test - Open Short"
            };

            var result = await _okxService.ExecuteTradeAsync(signal);

            return Ok(new
            {
                Test = "LIVE: Open Short",
                Signal = new { symbol, usdt, leverage, action = "OPEN_SHORT" },
                Result = new
                {
                    result.Success,
                    result.OrderId,
                    result.Symbol,
                    result.Side,
                    result.Size,
                    result.ErrorMessage
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Live test hatasÄ±!");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// ğŸ”¥ LIVE TEST: LONG pozisyonu kapat
    /// POST /api/test/live/close-long?symbol=DOGE&usdt=1
    /// usdt = kapatÄ±lacak miktar (dust threshold'a gÃ¶re tam/kÄ±smi kapanÄ±ÅŸ)
    /// </summary>
    [HttpPost("live/close-long")]
    public async Task<IActionResult> LiveTestCloseLong(
        [FromQuery] string symbol = "DOGE",
        [FromQuery] decimal usdt = 100) // YÃ¼ksek deÄŸer = tam kapanÄ±ÅŸ
    {
        _logger.LogWarning("ğŸ”¥ LIVE TEST: LONG POZÄ°SYON KAPATILIYOR - {Symbol} {USDT} USDT", symbol, usdt);

        try
        {
            var signal = new TradeSignal
            {
                Symbol = symbol,
                Action = TradeAction.CLOSE_LONG,
                MarginAmountUSDT = usdt,
                Leverage = 1,
                Reason = "Live Test - Close Long"
            };

            var result = await _okxService.ExecuteTradeAsync(signal);

            return Ok(new
            {
                Test = "LIVE: Close Long",
                Signal = new { symbol, usdt, action = "CLOSE_LONG" },
                Result = new
                {
                    result.Success,
                    result.OrderId,
                    result.Symbol,
                    result.Side,
                    result.ErrorMessage
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Live test hatasÄ±!");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// ğŸ”¥ LIVE TEST: SHORT pozisyonu kapat
    /// POST /api/test/live/close-short?symbol=BABY&usdt=100
    /// </summary>
    [HttpPost("live/close-short")]
    public async Task<IActionResult> LiveTestCloseShort(
        [FromQuery] string symbol = "BABY",
        [FromQuery] decimal usdt = 100)
    {
        _logger.LogWarning("ğŸ”¥ LIVE TEST: SHORT POZÄ°SYON KAPATILIYOR - {Symbol} {USDT} USDT", symbol, usdt);

        try
        {
            var signal = new TradeSignal
            {
                Symbol = symbol,
                Action = TradeAction.CLOSE_SHORT,
                MarginAmountUSDT = usdt,
                Leverage = 1,
                Reason = "Live Test - Close Short"
            };

            var result = await _okxService.ExecuteTradeAsync(signal);

            return Ok(new
            {
                Test = "LIVE: Close Short",
                Signal = new { symbol, usdt, action = "CLOSE_SHORT" },
                Result = new
                {
                    result.Success,
                    result.OrderId,
                    result.Symbol,
                    result.Side,
                    result.ErrorMessage
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Live test hatasÄ±!");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// ğŸ”¥ LIVE TEST: Market emri direkt gÃ¶nder
    /// POST /api/test/live/place-order?symbol=DOGE&side=buy&posSide=long&size=10
    /// </summary>
    [HttpPost("live/place-order")]
    public async Task<IActionResult> LiveTestPlaceOrder(
        [FromQuery] string symbol = "DOGE",
        [FromQuery] string side = "buy",
        [FromQuery] string posSide = "long",
        [FromQuery] decimal size = 10,
        [FromQuery] bool reduceOnly = false)
    {
        _logger.LogWarning("ğŸ”¥ LIVE TEST: MARKET EMRÄ° - {Symbol} {Side} {PosSide} {Size} kontrat", 
            symbol, side, posSide, size);

        try
        {
            var result = await _okxService.PlaceMarketOrderAsync(symbol, side, posSide, size, reduceOnly);

            return Ok(new
            {
                Test = "LIVE: Place Market Order",
                Order = new { symbol, side, posSide, size, reduceOnly },
                Result = new
                {
                    result.Success,
                    result.OrderId,
                    result.Symbol,
                    result.Side,
                    result.Size,
                    result.ErrorMessage
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Live test hatasÄ±!");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    // ================================================================
    // MEVCUT TEST METODLARI
    // ================================================================

    /// <summary>
    /// OKX hesap bilgisini test et
    /// GET /api/test/okx-account
    /// </summary>
    [HttpGet("okx-account")]
    public async Task<IActionResult> TestOkxAccount()
    {
        try
        {
            _logger.LogInformation("OKX hesap testi baÅŸlatÄ±lÄ±yor...");
            
            var userStats = await _okxService.GetAccountInfoAsync();

            return Ok(new
            {
                Success = true,
                Message = "OKX baÄŸlantÄ±sÄ± baÅŸarÄ±lÄ±!",
                Data = new
                {
                    TotalBalanceUSD = userStats.TotalUsd,
                    DefaultLeverage = userStats.Leverage,
                    OpenPositionsCount = userStats.ActivePositions.Count,
                    OpenPositions = userStats.ActivePositions.Select(p => new
                    {
                        p.Symbol,
                        p.Direction,
                        MarginUSD = p.MarginUsd,
                        p.EntryPrice,
                        p.Size,
                        UnrealizedPnL = p.UnrealizedPnl
                    })
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OKX test hatasÄ±!");
            return StatusCode(500, new
            {
                Success = false,
                Error = ex.Message,
                Details = ex.InnerException?.Message
            });
        }
    }

    /// <summary>
    /// Belirli bir coin iÃ§in pozisyon kontrol et
    /// GET /api/test/okx-position/ETH
    /// </summary>
    [HttpGet("okx-position/{symbol}")]
    public async Task<IActionResult> TestOkxPosition(string symbol)
    {
        try
        {
            _logger.LogInformation("OKX pozisyon testi: {Symbol}", symbol);
            
            var position = await _okxService.GetPositionAsync(symbol);

            if (position == null)
            {
                return Ok(new
                {
                    Success = true,
                    Message = $"{symbol} iÃ§in aÃ§Ä±k pozisyon YOK",
                    HasPosition = false
                });
            }

            return Ok(new
            {
                Success = true,
                Message = $"{symbol} pozisyonu bulundu",
                HasPosition = true,
                Position = new
                {
                    position.Symbol,
                    position.Direction,
                    MarginUSD = position.MarginUsd,
                    position.EntryPrice,
                    position.Size,
                    UnrealizedPnL = position.UnrealizedPnl
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OKX pozisyon test hatasÄ±: {Symbol}", symbol);
            return StatusCode(500, new
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// TÃ¼m aÃ§Ä±k pozisyonlarÄ± listele
    /// GET /api/test/okx-positions
    /// </summary>
    [HttpGet("okx-positions")]
    public async Task<IActionResult> TestOkxAllPositions()
    {
        try
        {
            _logger.LogInformation("TÃ¼m OKX pozisyonlarÄ± Ã§ekiliyor...");
            
            var positions = await _okxService.GetAllPositionsAsync();

            return Ok(new
            {
                Success = true,
                TotalPositions = positions.Count,
                Positions = positions.Select(p => new
                {
                    p.Symbol,
                    p.Direction,
                    MarginUSD = p.MarginUsd,
                    p.EntryPrice,
                    p.Size,
                    UnrealizedPnL = p.UnrealizedPnl
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OKX pozisyonlar test hatasÄ±!");
            return StatusCode(500, new
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    // ================================================================
    // ğŸ¯ FULL LIVE TEST - TÃœM Ä°ÅLEMLERÄ° SIRALI YAP
    // ================================================================

    /// <summary>
    /// ğŸš€ FULL LIVE TEST
    /// SÄ±rayla: DOGE Long â†’ DOGE Short â†’ ETH Long â†’ PozisyonlarÄ± GÃ¶ster â†’ Hepsini Kapat
    /// POST /api/test/live/full-cycle
    /// </summary>
    [HttpPost("live/full-cycle")]
    public async Task<IActionResult> FullLiveCycleTest()
    {
        var testResults = new List<object>();
        var startTime = DateTime.Now;

        _logger.LogInformation("ğŸš€ FULL LIVE CYCLE TEST BAÅLIYOR...");

        // ================================================================
        // AÅAMA 0: BaÅŸlangÄ±Ã§ Durumu
        // ================================================================
        decimal startBalance = 0;
        try
        {
            var accountInfo = await _okxService.GetAccountInfoAsync();
            startBalance = accountInfo.TotalUsd;
            
            testResults.Add(new
            {
                Step = "0ï¸âƒ£ BAÅLANGIÃ‡ DURUMU",
                Status = "âœ…",
                Balance = $"${startBalance:F2}",
                OpenPositions = accountInfo.ActivePositions.Count
            });
        }
        catch (Exception ex)
        {
            testResults.Add(new { Step = "0ï¸âƒ£ BAÅLANGIÃ‡ DURUMU", Status = "âŒ", Error = ex.Message });
        }

        await Task.Delay(500); // Rate limit iÃ§in bekle

        // ================================================================
        // AÅAMA 1: DOGE LONG AÃ‡ (2 USDT, 3x)
        // ================================================================
        try
        {
            _logger.LogInformation("1ï¸âƒ£ DOGE LONG aÃ§Ä±lÄ±yor...");
            
            // Ã–nce hesapla
            var calculation = await _okxService.CalculateOrderAsync("DOGE", 2, 3, "LONG");
            
            // KaldÄ±raÃ§ ayarla
            await _okxService.SetLeverageAsync("DOGE", 3);
            
            // Emir gÃ¶nder
            var result = await _okxService.PlaceMarketOrderAsync("DOGE", "buy", "long", calculation.Contracts);
            
            testResults.Add(new
            {
                Step = "1ï¸âƒ£ DOGE LONG AÃ‡",
                Status = result.Success ? "âœ… BAÅARILI" : "âŒ BAÅARISIZ",
                OrderId = result.OrderId,
                Contracts = calculation.Contracts,
                CoinAmount = $"{calculation.CoinAmount} DOGE",
                Margin = $"{calculation.ActualMarginUSD:F2} USDT",
                Leverage = "3x",
                Error = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            testResults.Add(new { Step = "1ï¸âƒ£ DOGE LONG AÃ‡", Status = "âŒ HATA", Error = ex.Message });
        }

        await Task.Delay(1000); // Ä°ÅŸlem yerleÅŸmesi iÃ§in bekle

        // ================================================================
        // AÅAMA 2: DOGE SHORT AÃ‡ (2 USDT, 3x) - HEDGE TEST
        // ================================================================
        try
        {
            _logger.LogInformation("2ï¸âƒ£ DOGE SHORT aÃ§Ä±lÄ±yor (HEDGE)...");
            
            var calculation = await _okxService.CalculateOrderAsync("DOGE", 2, 3, "SHORT");
            
            var result = await _okxService.PlaceMarketOrderAsync("DOGE", "sell", "short", calculation.Contracts);
            
            testResults.Add(new
            {
                Step = "2ï¸âƒ£ DOGE SHORT AÃ‡ (HEDGE)",
                Status = result.Success ? "âœ… BAÅARILI" : "âŒ BAÅARISIZ",
                OrderId = result.OrderId,
                Contracts = calculation.Contracts,
                CoinAmount = $"{calculation.CoinAmount} DOGE",
                Margin = $"{calculation.ActualMarginUSD:F2} USDT",
                Leverage = "3x",
                Error = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            testResults.Add(new { Step = "2ï¸âƒ£ DOGE SHORT AÃ‡", Status = "âŒ HATA", Error = ex.Message });
        }

        await Task.Delay(1000);

        // ================================================================
        // AÅAMA 3: ETH LONG AÃ‡ (5 USDT, 5x)
        // ================================================================
        try
        {
            _logger.LogInformation("3ï¸âƒ£ ETH LONG aÃ§Ä±lÄ±yor...");
            
            var calculation = await _okxService.CalculateOrderAsync("ETH", 5, 5, "LONG");
            
            await _okxService.SetLeverageAsync("ETH", 5);
            
            var result = await _okxService.PlaceMarketOrderAsync("ETH", "buy", "long", calculation.Contracts);
            
            testResults.Add(new
            {
                Step = "3ï¸âƒ£ ETH LONG AÃ‡",
                Status = result.Success ? "âœ… BAÅARILI" : "âŒ BAÅARISIZ",
                OrderId = result.OrderId,
                Contracts = calculation.Contracts,
                CoinAmount = $"{calculation.CoinAmount:F4} ETH",
                Margin = $"{calculation.ActualMarginUSD:F2} USDT",
                Leverage = "5x",
                Error = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            testResults.Add(new { Step = "3ï¸âƒ£ ETH LONG AÃ‡", Status = "âŒ HATA", Error = ex.Message });
        }

        await Task.Delay(1000);

        // ================================================================
        // AÅAMA 4: TÃœM POZÄ°SYONLARI GÃ–STER
        // ================================================================
        List<Position> allPositions = new();
        try
        {
            _logger.LogInformation("4ï¸âƒ£ Pozisyonlar listeleniyor...");
            
            allPositions = await _okxService.GetAllPositionsAsync();
            
            testResults.Add(new
            {
                Step = "4ï¸âƒ£ AÃ‡IK POZÄ°SYONLAR",
                Status = "âœ…",
                TotalPositions = allPositions.Count,
                Positions = allPositions.Select(p => new
                {
                    p.Symbol,
                    p.Direction,
                    Margin = $"${p.MarginUsd:F2}",
                    Size = p.Size,
                    EntryPrice = $"${p.EntryPrice:F4}",
                    PnL = $"${p.UnrealizedPnl:F4}"
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            testResults.Add(new { Step = "4ï¸âƒ£ POZÄ°SYONLAR", Status = "âŒ HATA", Error = ex.Message });
        }

        await Task.Delay(1000);

        // ================================================================
        // AÅAMA 5: DOGE LONG KAPAT
        // ================================================================
        try
        {
            _logger.LogInformation("5ï¸âƒ£ DOGE LONG kapatÄ±lÄ±yor...");
            
            var result = await _okxService.ClosePositionAsync("DOGE", "long");
            
            testResults.Add(new
            {
                Step = "5ï¸âƒ£ DOGE LONG KAPAT",
                Status = result.Success ? "âœ… KAPATILDI" : "âŒ BAÅARISIZ",
                Error = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            testResults.Add(new { Step = "5ï¸âƒ£ DOGE LONG KAPAT", Status = "âŒ HATA", Error = ex.Message });
        }

        await Task.Delay(500);

        // ================================================================
        // AÅAMA 6: DOGE SHORT KAPAT
        // ================================================================
        try
        {
            _logger.LogInformation("6ï¸âƒ£ DOGE SHORT kapatÄ±lÄ±yor...");
            
            var result = await _okxService.ClosePositionAsync("DOGE", "short");
            
            testResults.Add(new
            {
                Step = "6ï¸âƒ£ DOGE SHORT KAPAT",
                Status = result.Success ? "âœ… KAPATILDI" : "âŒ BAÅARISIZ",
                Error = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            testResults.Add(new { Step = "6ï¸âƒ£ DOGE SHORT KAPAT", Status = "âŒ HATA", Error = ex.Message });
        }

        await Task.Delay(500);

        // ================================================================
        // AÅAMA 7: ETH LONG KAPAT
        // ================================================================
        try
        {
            _logger.LogInformation("7ï¸âƒ£ ETH LONG kapatÄ±lÄ±yor...");
            
            var result = await _okxService.ClosePositionAsync("ETH", "long");
            
            testResults.Add(new
            {
                Step = "7ï¸âƒ£ ETH LONG KAPAT",
                Status = result.Success ? "âœ… KAPATILDI" : "âŒ BAÅARISIZ",
                Error = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            testResults.Add(new { Step = "7ï¸âƒ£ ETH LONG KAPAT", Status = "âŒ HATA", Error = ex.Message });
        }

        await Task.Delay(500);

        // ================================================================
        // AÅAMA 8: FÄ°NAL DURUM
        // ================================================================
        decimal endBalance = 0;
        int remainingPositions = 0;
        try
        {
            var finalAccount = await _okxService.GetAccountInfoAsync();
            endBalance = finalAccount.TotalUsd;
            remainingPositions = finalAccount.ActivePositions.Count;
            
            var pnl = endBalance - startBalance;
            
            testResults.Add(new
            {
                Step = "8ï¸âƒ£ FÄ°NAL DURUM",
                Status = "âœ…",
                StartBalance = $"${startBalance:F2}",
                EndBalance = $"${endBalance:F2}",
                TotalPnL = $"${pnl:+0.00;-0.00}",
                RemainingPositions = remainingPositions
            });
        }
        catch (Exception ex)
        {
            testResults.Add(new { Step = "8ï¸âƒ£ FÄ°NAL DURUM", Status = "âŒ HATA", Error = ex.Message });
        }

        var totalTime = (DateTime.Now - startTime).TotalSeconds;

        return Ok(new
        {
            TestName = "ğŸš€ FULL LIVE CYCLE TEST",
            TotalSteps = testResults.Count,
            TotalTimeSeconds = Math.Round(totalTime, 1),
            Results = testResults,
            Summary = new
            {
                StartBalance = $"${startBalance:F2}",
                EndBalance = $"${endBalance:F2}",
                PnL = $"${(endBalance - startBalance):+0.00;-0.00}",
                RemainingPositions = remainingPositions
            }
        });
    }

    // ================================================================
    // ğŸ¤– AI TEST ENDPOINT'LERÄ°
    // ================================================================

    /// <summary>
    /// ğŸ”Œ AI BaÄŸlantÄ± Testi
    /// GET /api/test/ai/connection
    /// </summary>
    [HttpGet("ai/connection")]
    public async Task<IActionResult> TestAIConnection()
    {
        _logger.LogInformation("ğŸ”Œ AI BaÄŸlantÄ± testi baÅŸlatÄ±lÄ±yor...");

        try
        {
            var (success, message) = await _aiService.TestConnectionAsync();

            return Ok(new
            {
                Test = "AI Connection Test",
                Status = success ? "âœ… BAÅARILI" : "âŒ HATA",
                Message = message,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI baÄŸlantÄ± testi hatasÄ±");
            return Ok(new
            {
                Test = "AI Connection Test",
                Status = "âŒ HATA",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// ğŸ’¬ AI'a Basit Soru Sor
    /// GET /api/test/ai/ask?q=merhaba
    /// </summary>
    [HttpGet("ai/ask")]
    public async Task<IActionResult> AskAI([FromQuery] string q = "Merhaba, kripto piyasasÄ± hakkÄ±nda ne dÃ¼ÅŸÃ¼nÃ¼yorsun?")
    {
        _logger.LogInformation("ğŸ’¬ AI'a soru soruluyor: {Question}", q);

        try
        {
            var startTime = DateTime.Now;
            var response = await _aiService.AskAsync(q);
            var duration = (DateTime.Now - startTime).TotalMilliseconds;

            return Ok(new
            {
                Test = "AI Ask",
                Status = "âœ… BAÅARILI",
                Question = q,
                Response = response,
                DurationMs = Math.Round(duration, 0),
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI soru hatasÄ±");
            return Ok(new
            {
                Test = "AI Ask",
                Status = "âŒ HATA",
                Question = q,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// ğŸ‹ Sahte Balina Hareketi ile AI Karar Testi
    /// POST /api/test/ai/analyze
    /// SimÃ¼le edilmiÅŸ bir balina hareketi gÃ¶nderir ve AI'Ä±n kararÄ±nÄ± dÃ¶ndÃ¼rÃ¼r
    /// </summary>
    [HttpPost("ai/analyze")]
    public async Task<IActionResult> TestAIAnalysis([FromBody] AITestRequest? request = null)
    {
        _logger.LogInformation("ğŸ‹ AI Analiz testi baÅŸlatÄ±lÄ±yor...");

        try
        {
            // 1. Mevcut bakiye ve pozisyonlarÄ± al
            var userStats = await _okxService.GetAccountInfoAsync();
            var positions = await _okxService.GetAllPositionsAsync();

            // 2. Test iÃ§in sahte balina hareketi oluÅŸtur (ya da request'ten al)
            var movement = request?.Movement ?? new WhaleMovement
            {
                Type = "BUY",
                Symbol = "ETH",
                Amount = 0.5m,
                ValueUSDT = 2000m,
                Price = 4000m,
                TxHash = "0x_test_" + Guid.NewGuid().ToString()[..8],
                Timestamp = DateTime.UtcNow,
                WhalePositionAfter = 10.5m
            };

            // 3. AI Context oluÅŸtur
            var context = new AIContext
            {
                OurBalanceUSDT = userStats.TotalUsd,
                WhaleBalanceUSDT = request?.WhaleBalance ?? 500000m, // 500K varsayÄ±lan
                NewMovement = movement,
                OurPositions = positions.Select(p => new OurPosition
                {
                    Symbol = p.Symbol.Replace("-USDT-SWAP", ""),
                    Direction = p.Direction,
                    MarginUSDT = p.MarginUsd,
                    Leverage = p.Size > 0 && p.MarginUsd > 0 ? (int)Math.Ceiling(p.Size * p.EntryPrice / p.MarginUsd) : 3,
                    EntryPrice = p.EntryPrice,
                    UnrealizedPnL = p.UnrealizedPnl
                }).ToList()
            };

            _logger.LogInformation("ğŸ“Š AI Context: Balance=${Balance}, Positions={Count}",
                context.OurBalanceUSDT, context.OurPositions.Count);

            // 4. AI'a gÃ¶nder
            var startTime = DateTime.Now;
            var decision = await _aiService.AnalyzeMovementAsync(context);
            var duration = (DateTime.Now - startTime).TotalMilliseconds;

            return Ok(new
            {
                Test = "AI Analysis",
                Status = decision.ParseSuccess ? "âœ… BAÅARILI" : "âš ï¸ PARSE HATASI",
                Input = new
                {
                    OurBalance = $"${context.OurBalanceUSDT:F2}",
                    WhaleBalance = $"${context.WhaleBalanceUSDT:F2}",
                    Movement = new
                    {
                        movement.Type,
                        movement.Symbol,
                        Value = $"${movement.ValueUSDT:F2}",
                        movement.Amount,
                        Price = $"${movement.Price:F2}"
                    },
                    OurPositionsCount = context.OurPositions.Count
                },
                Decision = new
                {
                    decision.Action,
                    decision.Symbol,
                    AmountUSDT = $"${decision.AmountUSDT:F2}",
                    decision.Leverage,
                    Confidence = $"{decision.ConfidenceScore}%",
                    decision.Reasoning,
                    ShouldTrade = decision.ShouldTrade
                },
                RawResponse = decision.RawResponse,
                ParseInfo = new
                {
                    decision.ParseSuccess,
                    decision.ParseError
                },
                DurationMs = Math.Round(duration, 0),
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Analiz testi hatasÄ±");
            return Ok(new
            {
                Test = "AI Analysis",
                Status = "âŒ HATA",
                Error = ex.Message,
                StackTrace = ex.StackTrace
            });
        }
    }

    /// <summary>
    /// ğŸ”„ FULL AI â†’ OKX TEST (SimÃ¼lasyon)
    /// POST /api/test/ai/full-cycle
    /// AI karar verir, ShouldTrade=true ise OKX'e gerÃ§ek iÅŸlem gÃ¶nderir
    /// âš ï¸ DÄ°KKAT: GERÃ‡EK Ä°ÅLEM AÃ‡AR!
    /// </summary>
    [HttpPost("ai/full-cycle")]
    public async Task<IActionResult> AIFullCycleTest([FromBody] AITestRequest? request = null)
    {
        var testResults = new List<object>();
        var startTime = DateTime.Now;

        _logger.LogInformation("ğŸš€ AI FULL CYCLE TEST BAÅLIYOR...");
        _logger.LogWarning("âš ï¸ DÄ°KKAT: Bu test GERÃ‡EK Ä°ÅLEM yapabilir!");

        try
        {
            // Step 1: Mevcut durum
            var userStats = await _okxService.GetAccountInfoAsync();
            var positions = await _okxService.GetAllPositionsAsync();
            var startBalance = userStats.TotalUsd;

            testResults.Add(new
            {
                Step = "1ï¸âƒ£ BAÅLANGIÃ‡ DURUMU",
                Balance = $"${startBalance:F2}",
                OpenPositions = positions.Count
            });

            // Step 2: Sahte balina hareketi
            var movement = request?.Movement ?? new WhaleMovement
            {
                Type = "BUY",
                Symbol = "DOGE",
                Amount = 1000m,
                ValueUSDT = 300m,
                Price = 0.30m,
                TxHash = "0x_ai_test_" + Guid.NewGuid().ToString()[..8],
                Timestamp = DateTime.UtcNow,
                WhalePositionAfter = 50000m
            };

            testResults.Add(new
            {
                Step = "2ï¸âƒ£ BALÄ°NA HAREKETÄ°",
                Type = movement.Type,
                Symbol = movement.Symbol,
                Value = $"${movement.ValueUSDT:F2}",
                TxHash = movement.TxHash
            });

            // Step 3: AI Context
            var context = new AIContext
            {
                OurBalanceUSDT = userStats.TotalUsd,
                WhaleBalanceUSDT = request?.WhaleBalance ?? 500000m,
                NewMovement = movement,
                OurPositions = positions.Select(p => new OurPosition
                {
                    Symbol = p.Symbol.Replace("-USDT-SWAP", ""),
                    Direction = p.Direction,
                    MarginUSDT = p.MarginUsd,
                    Leverage = p.Size > 0 && p.MarginUsd > 0 ? (int)Math.Ceiling(p.Size * p.EntryPrice / p.MarginUsd) : 3,
                    EntryPrice = p.EntryPrice,
                    UnrealizedPnL = p.UnrealizedPnl
                }).ToList()
            };

            // Step 4: AI Karar
            var decision = await _aiService.AnalyzeMovementAsync(context);

            testResults.Add(new
            {
                Step = "3ï¸âƒ£ AI KARARI",
                Action = decision.Action,
                Symbol = decision.Symbol,
                Amount = $"${decision.AmountUSDT:F2}",
                Leverage = decision.Leverage,
                Confidence = $"{decision.ConfidenceScore}%",
                Reasoning = decision.Reasoning,
                ShouldTrade = decision.ShouldTrade
            });

            // Step 5: Ä°ÅŸlem Yap (eÄŸer AI onayladÄ±ysa)
            if (decision.ShouldTrade)
            {
                if (decision.Action == "LONG")
                {
                    _logger.LogInformation("ğŸ“ˆ LONG pozisyon aÃ§Ä±lÄ±yor: {Symbol} ${Amount}",
                        decision.Symbol, decision.AmountUSDT);

                    // TradeSignal oluÅŸtur ve ExecuteTradeAsync kullan
                    var signal = new TradeSignal
                    {
                        Symbol = decision.Symbol,
                        Action = "OPEN_LONG",
                        Decision = "TRADE",
                        MarginAmountUSDT = decision.AmountUSDT,
                        Leverage = decision.Leverage,
                        Reason = "AI Test"
                    };
                    
                    var tradeResult = await _okxService.ExecuteTradeAsync(signal);

                    testResults.Add(new
                    {
                        Step = "4ï¸âƒ£ Ä°ÅLEM SONUCU",
                        Status = tradeResult.Success ? "âœ… BAÅARILI" : "âŒ HATA",
                        OrderId = tradeResult.OrderId,
                        Error = tradeResult.ErrorMessage
                    });
                }
                else if (decision.Action == "CLOSE_LONG" || decision.Action == "SHORT")
                {
                    // SHORT = Mevcut LONG pozisyonu kapat
                    _logger.LogInformation("ğŸ“‰ SHORT sinyali: {Symbol} pozisyonu kapatÄ±lÄ±yor", decision.Symbol);

                    // Mevcut pozisyonu bul
                    var instId = $"{decision.Symbol}-USDT-SWAP";
                    var existingPosition = positions.FirstOrDefault(p => 
                        p.Symbol == instId && p.Direction == "long");

                    if (existingPosition != null)
                    {
                        var closeResult = await _okxService.ClosePositionAsync(
                            decision.Symbol, "long");

                        testResults.Add(new
                        {
                            Step = "4ï¸âƒ£ POZÄ°SYON KAPATMA",
                            Status = closeResult.Success ? "âœ… BAÅARILI" : "âŒ HATA",
                            OrderId = closeResult.OrderId,
                            ClosedPosition = $"{existingPosition.Symbol} ${existingPosition.MarginUsd:F2}",
                            Error = closeResult.ErrorMessage
                        });
                    }
                    else
                    {
                        testResults.Add(new
                        {
                            Step = "4ï¸âƒ£ POZÄ°SYON KAPATMA",
                            Status = "âš ï¸ POZÄ°SYON BULUNAMADI",
                            Message = $"{instId} iÃ§in aÃ§Ä±k LONG pozisyon yok"
                        });
                    }
                }
                else
                {
                    testResults.Add(new
                    {
                        Step = "4ï¸âƒ£ Ä°ÅLEM",
                        Status = "â­ï¸ ATLANDIÄ",
                        Reason = $"Action: {decision.Action}"
                    });
                }
            }
            else
            {
                testResults.Add(new
                {
                    Step = "4ï¸âƒ£ Ä°ÅLEM",
                    Status = "â­ï¸ AI ONAYLAMADI",
                    Reason = decision.Reasoning
                });
            }

            // Step 6: Final durum
            await Task.Delay(1000);
            var finalStats = await _okxService.GetAccountInfoAsync();
            var finalPositions = await _okxService.GetAllPositionsAsync();

            testResults.Add(new
            {
                Step = "5ï¸âƒ£ FÄ°NAL DURUM",
                StartBalance = $"${startBalance:F2}",
                EndBalance = $"${finalStats.TotalUsd:F2}",
                PnL = $"${(finalStats.TotalUsd - startBalance):+0.00;-0.00}",
                OpenPositions = finalPositions.Count
            });

            var totalTime = (DateTime.Now - startTime).TotalSeconds;

            return Ok(new
            {
                TestName = "ğŸ¤– AI FULL CYCLE TEST",
                TotalSteps = testResults.Count,
                TotalTimeSeconds = Math.Round(totalTime, 1),
                Results = testResults,
                AIDecision = new
                {
                    decision.Action,
                    decision.Symbol,
                    AmountUSDT = decision.AmountUSDT,
                    decision.Leverage,
                    decision.ConfidenceScore,
                    decision.Reasoning,
                    decision.ShouldTrade
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Full Cycle test hatasÄ±");
            testResults.Add(new
            {
                Step = "âŒ HATA",
                Error = ex.Message,
                StackTrace = ex.StackTrace
            });

            return Ok(new
            {
                TestName = "ğŸ¤– AI FULL CYCLE TEST",
                Status = "âŒ HATA",
                Results = testResults
            });
        }
    }

    // ================================================================
    // ğŸ‹ WHALE LIVE CYCLE TEST - TAM SENARYO
    // ================================================================

    /// <summary>
    /// ğŸ‹ WHALE LIVE CYCLE TEST
    /// Mock whale verisi ile gerÃ§ek AI + OKX iÅŸlem testi
    /// 
    /// SENARYO:
    /// 1. BaÅŸlangÄ±Ã§ durumu kontrol (pozisyon yok)
    /// 2. Whale ETH alÄ±yor ($400) â†’ AI analiz â†’ OKX LONG aÃ§
    /// 3. Whale yarÄ±sÄ±nÄ± satÄ±yor ($200) â†’ AI analiz â†’ OKX kÄ±smi kapat
    /// 4. Whale kalanÄ± satÄ±yor ($200) â†’ AI analiz â†’ OKX tam kapat
    /// 
    /// GET /api/test/whale-live-cycle
    /// </summary>
    [HttpGet("whale-live-cycle")]
    public async Task<IActionResult> WhaleLiveCycleTest()
    {
        var testResults = new List<object>();
        var startTime = DateTime.UtcNow;

        const string symbol = "ETH";
        const decimal whaleTotalBalance = 100_000m;
        const decimal whaleBuyUsd = 4000m;

        _logger.LogWarning("LIVE WHALE CYCLE TEST starting. This will place REAL orders.");

        InstrumentInfo? instrument = null;
        try
        {
            instrument = await _okxService.GetInstrumentInfoAsync(symbol);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Instrument lookup failed for {Symbol}", symbol);
        }

        var referencePrice = instrument?.LastPrice ?? 3000m;
        if (referencePrice <= 0)
        {
            referencePrice = 3000m;
        }

        async Task<object> ProcessStageAsync(string stageName, string movementType, decimal valueUsdt, decimal amount)
        {
            var userStats = await _okxService.GetAccountInfoAsync();

            var movement = new WhaleMovement
            {
                Type = movementType,
                Symbol = symbol,
                Amount = amount,
                ValueUSDT = valueUsdt,
                Price = referencePrice,
                TxHash = $"0x_mock_{Guid.NewGuid().ToString()[..8]}",
                Timestamp = DateTime.UtcNow
            };

            var context = new AIContext
            {
                OurBalanceUSDT = userStats.TotalUsd,
                WhaleBalanceUSDT = whaleTotalBalance,
                NewMovement = movement,
                OurPositions = userStats.ActivePositions.Select(p => new OurPosition
                {
                    Symbol = p.Symbol,
                    Direction = p.Direction,
                    MarginUSDT = p.MarginUsd,
                    Leverage = p.MarginUsd > 0 && p.EntryPrice > 0
                        ? (int)Math.Ceiling((p.Size * p.EntryPrice) / p.MarginUsd)
                        : 3,
                    EntryPrice = p.EntryPrice,
                    UnrealizedPnL = p.UnrealizedPnl
                }).ToList()
            };

            _logger.LogInformation(
                "Stage {Stage} - AI context: OurBalance={Balance:F2} WhaleBalance={WhaleBalance:F2} Positions={Positions} Movement={Type} {Symbol} ${Value:F2}",
                stageName,
                context.OurBalanceUSDT,
                context.WhaleBalanceUSDT,
                context.OurPositions.Count,
                movement.Type,
                movement.Symbol,
                movement.ValueUSDT);

            var decision = await _aiService.AnalyzeMovementAsync(context);
            _logger.LogInformation(
                "Stage {Stage} - AI decision: Action={Action} Symbol={Symbol} AmountUSDT={Amount:F4} ShouldTrade={ShouldTrade}",
                stageName,
                decision.Action,
                decision.Symbol,
                decision.AmountUSDT,
                decision.ShouldTrade);

            TradeSignal? signal = null;
            TradeResult? tradeResult = null;

            if (decision.ShouldTrade)
            {
                var mappedAction = decision.Action.ToUpperInvariant() switch
                {
                    "LONG" => TradeAction.OPEN_LONG,
                    "CLOSE_LONG" => TradeAction.CLOSE_LONG,
                    "SHORT" => TradeAction.CLOSE_LONG,
                    _ => TradeAction.IGNORE
                };

                if (mappedAction != TradeAction.IGNORE)
                {
                    signal = new TradeSignal
                    {
                        Decision = "TRADE",
                        Reason = decision.Reasoning,
                        Symbol = decision.Symbol,
                        Action = mappedAction,
                        Leverage = decision.Leverage,
                        MarginAmountUSDT = decision.AmountUSDT,
                        TradeConfidence = decision.ConfidenceScore,
                        SourceTxHash = movement.TxHash
                    };

                    _logger.LogInformation(
                        "Stage {Stage} - Sending to OKX: {Action} {Symbol} ${Amount:F4} {Leverage}x",
                        stageName,
                        signal.Action,
                        signal.Symbol,
                        signal.MarginAmountUSDT,
                        signal.Leverage);

                    tradeResult = await _okxService.ExecuteTradeAsync(signal);
                    _logger.LogInformation(
                        "Stage {Stage} - OKX result: Success={Success} OrderId={OrderId} Error={Error}",
                        stageName,
                        tradeResult.Success,
                        tradeResult.OrderId,
                        tradeResult.ErrorMessage ?? "none");
                }
            }

            return new
            {
                Stage = stageName,
                Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                Context = new
                {
                    context.OurBalanceUSDT,
                    context.WhaleBalanceUSDT,
                    Positions = context.OurPositions.Select(p => new
                    {
                        p.Symbol,
                        p.Direction,
                        p.MarginUSDT,
                        p.EntryPrice,
                        p.UnrealizedPnL
                    }),
                    Movement = new
                    {
                        movement.Type,
                        movement.Symbol,
                        movement.Amount,
                        movement.ValueUSDT,
                        movement.Price,
                        movement.TxHash
                    }
                },
                AIDecision = new
                {
                    decision.Action,
                    decision.Symbol,
                    decision.AmountUSDT,
                    decision.Leverage,
                    decision.ConfidenceScore,
                    decision.Reasoning,
                    decision.ShouldTrade,
                    decision.ParseSuccess,
                    decision.ParseError,
                    decision.RawResponse
                },
                Signal = signal == null
                    ? null
                    : new
                    {
                        signal.Action,
                        signal.Symbol,
                        signal.MarginAmountUSDT,
                        signal.Leverage
                    },
                OkxResult = tradeResult == null
                    ? null
                    : new
                    {
                        tradeResult.Success,
                        tradeResult.OrderId,
                        tradeResult.Symbol,
                        tradeResult.Side,
                        tradeResult.Size,
                        tradeResult.ErrorMessage
                    }
            };
        }

        try
        {
            var accountInfo = await _okxService.GetAccountInfoAsync();
            testResults.Add(new
            {
                Stage = "0 - START",
                Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                BalanceUSD = Math.Round(accountInfo.TotalUsd, 2),
                OpenPositions = accountInfo.ActivePositions.Count,
                WhaleBalanceUSD = whaleTotalBalance,
                PlannedBuyUSD = whaleBuyUsd,
                ReferencePrice = referencePrice
            });
        }
        catch (Exception ex)
        {
            testResults.Add(new { Stage = "0 - START", Error = ex.Message });
        }

        var whaleBuyAmount = referencePrice > 0 ? whaleBuyUsd / referencePrice : 0m;
        var whaleSellHalfUsd = whaleBuyUsd / 2m;
        var whaleSellHalfAmount = referencePrice > 0 ? whaleSellHalfUsd / referencePrice : 0m;

        testResults.Add(await ProcessStageAsync("1 - WHALE BUY", "BUY", whaleBuyUsd, whaleBuyAmount));
        await Task.Delay(1000);

        testResults.Add(await ProcessStageAsync("2 - WHALE SELL HALF", "SELL", whaleSellHalfUsd, whaleSellHalfAmount));
        await Task.Delay(1000);

        testResults.Add(await ProcessStageAsync("3 - WHALE SELL REST", "SELL", whaleSellHalfUsd, whaleSellHalfAmount));
        await Task.Delay(1000);

        try
        {
            var finalAccount = await _okxService.GetAccountInfoAsync();
            testResults.Add(new
            {
                Stage = "4 - FINAL",
                Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                BalanceUSD = Math.Round(finalAccount.TotalUsd, 2),
                OpenPositions = finalAccount.ActivePositions.Count
            });
        }
        catch (Exception ex)
        {
            testResults.Add(new { Stage = "4 - FINAL", Error = ex.Message });
        }

        var totalTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

        return Ok(new
        {
            Title = "WHALE LIVE CYCLE TEST",
            Status = "COMPLETED",
            TotalDurationMs = Math.Round(totalTimeMs, 0),
            Results = testResults
        });
    }
}
public class AITestRequest
{
    public WhaleMovement? Movement { get; set; }
    public decimal? WhaleBalance { get; set; }
}
