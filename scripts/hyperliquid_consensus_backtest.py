#!/usr/bin/env python3
"""Replay Hyperliquid consensus signals and simulate OKX-style execution.

This is an offline research script. It does not place orders.

The replay uses historical Hyperliquid position_events.csv files to rebuild what
the 40 tracked traders were carrying at each time step. It then runs the same
side-aware allocation/consensus idea used by the live service and converts coin
direction scores into target OKX notional exposure for a grid of multipliers.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import time
from collections import defaultdict
from dataclasses import dataclass, field
from datetime import datetime, timedelta, timezone
from decimal import Decimal, InvalidOperation
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


INFO_URL = "https://api.hyperliquid.xyz/info"
DEFAULT_RUN_DIR = Path("data/reports/hyperliquid_profiles/hl_full_001_011_30d")
DEFAULT_SCOREBOARD = DEFAULT_RUN_DIR / "historical_scoreboard" / "historical_scoreboard.csv"
DEFAULT_POSITIONS = DEFAULT_RUN_DIR / "okx_coin_report" / "okx_closed_positions.csv"
DEFAULT_OKX_SYMBOLS = DEFAULT_RUN_DIR / "okx_coin_report" / "okx_usdt_swap_symbols.json"
DEFAULT_CACHE_DIR = Path("data/cache/hyperliquid_candles")
DEFAULT_OUT_ROOT = Path("data/reports/consensus_backtests")

SYMBOL_ALIASES = {
    "kBONK": "BONK",
    "kFLOKI": "FLOKI",
    "kNEIRO": "NEIRO",
    "kPEPE": "PEPE",
    "kSHIB": "SHIB",
}


@dataclass(frozen=True)
class GeneralProfile:
    address: str
    quality: float
    confidence: float
    account_value: float
    historical_quality_score: float
    historical_confidence_score: float


@dataclass(frozen=True)
class SkillProfile:
    closed_positions: int
    wins: int
    losses: int
    net_pnl: float
    gross_profit: float
    gross_loss: float
    profit_factor: float
    avg_alloc_pct: float
    median_alloc_pct: float
    p75_alloc_pct: float
    p90_alloc_pct: float
    max_alloc_pct: float
    skill: float
    sample_confidence: float


@dataclass
class PositionEvent:
    time: datetime
    address: str
    coin: str
    price: float
    end_position: float
    account_value: float


@dataclass
class ActiveSourcePosition:
    address: str
    coin: str
    qty: float
    side: str
    last_event_price: float
    account_value: float
    current_price: float
    current_notional: float
    current_alloc_pct: float


@dataclass
class ExposureRow:
    address: str
    coin: str
    side: str
    weighted_signal: float
    current_notional: float
    current_alloc_pct: float
    coin_skill: float
    sample_confidence: float
    allocation_conviction: float
    normalized_exposure: float
    risk_adjustment: float


@dataclass
class ConsensusSnapshot:
    coin: str
    direction_score: float
    quality_score: float
    long_power: float
    short_power: float
    conflict_ratio: float
    participation: float
    action: str
    contributor_count: int


@dataclass
class SimPosition:
    qty: float = 0.0
    avg_entry: float = 0.0

    def signed_notional(self, price: float) -> float:
        return self.qty * price

    def unrealized(self, price: float) -> float:
        if abs(self.qty) <= 1e-12 or self.avg_entry <= 0:
            return 0.0
        return abs(self.qty) * (price - self.avg_entry) * sign(self.qty)


@dataclass
class SimTrade:
    time: datetime
    coin: str
    old_notional: float
    target_notional: float
    delta_notional: float
    price: float
    realized_pnl: float
    cost: float
    equity_after: float


@dataclass
class SimResult:
    requested_days: int
    simulated_days: float
    multiplier: float
    initial_equity: float
    final_equity: float
    pnl_usd: float
    pnl_pct: float
    max_drawdown_pct: float
    trades: list[SimTrade] = field(default_factory=list)
    equity_curve: list[dict[str, Any]] = field(default_factory=list)
    coin_pnl: dict[str, float] = field(default_factory=lambda: defaultdict(float))
    coin_turnover: dict[str, float] = field(default_factory=lambda: defaultdict(float))
    fees_and_slippage: float = 0.0
    below_min_target_count: int = 0
    missing_price_count: int = 0
    consensus_points: int = 0


def dec(value: Any) -> Decimal:
    try:
        return Decimal(str(value if value is not None and value != "" else "0"))
    except (InvalidOperation, ValueError):
        return Decimal("0")


def num(value: Any) -> float:
    return float(dec(value))


def clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


def sign(value: float) -> int:
    if value > 0:
        return 1
    if value < 0:
        return -1
    return 0


def parse_time(value: str) -> datetime:
    text = str(value or "").strip()
    if not text:
        return datetime.fromtimestamp(0, tz=timezone.utc)
    if text.endswith("Z"):
        text = text[:-1] + "+00:00"
    dt = datetime.fromisoformat(text)
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt.astimezone(timezone.utc)


def to_ms(dt: datetime) -> int:
    return int(dt.timestamp() * 1000)


def normalize_coin(value: str) -> str:
    raw = str(value or "").strip()
    if ":" in raw:
        raw = raw.split(":", 1)[1]
    return SYMBOL_ALIASES.get(raw, raw.upper())


def percentile(values: list[float], q: float) -> float:
    cleaned = sorted(v for v in values if math.isfinite(v))
    if not cleaned:
        return 0.0
    if len(cleaned) == 1:
        return cleaned[0]
    pos = (len(cleaned) - 1) * q
    lo = math.floor(pos)
    hi = math.ceil(pos)
    if lo == hi:
        return cleaned[lo]
    return cleaned[lo] + (cleaned[hi] - cleaned[lo]) * (pos - lo)


def average(values: list[float]) -> float:
    return sum(values) / len(values) if values else 0.0


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def write_csv(path: Path, rows: list[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if not rows:
        path.write_text("", encoding="utf-8")
        return
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0].keys()))
        writer.writeheader()
        writer.writerows(rows)


def load_okx_symbols(path: Path) -> set[str]:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    values = payload.get("symbols", []) if isinstance(payload, dict) else payload
    return {normalize_coin(str(x)) for x in values}


def load_scoreboard(path: Path, top_traders: int) -> tuple[list[str], dict[str, GeneralProfile], list[dict[str, str]]]:
    rows = read_csv(path)
    eligible = [r for r in rows if str(r.get("watchlist_eligible", "")).lower() == "yes"]
    selected = eligible[:top_traders] if len(eligible) >= top_traders else rows[:top_traders]
    addresses = [str(r["address"]).strip().lower() for r in selected]
    profiles: dict[str, GeneralProfile] = {}
    for row in selected:
        address = str(row["address"]).strip().lower()
        quality_score = num(row.get("historical_quality_score"))
        confidence_score = num(row.get("confidence_score"))
        profiles[address] = GeneralProfile(
            address=address,
            quality=clamp(quality_score / 100.0, 0.0, 1.0),
            confidence=clamp(confidence_score / 100.0, 0.0, 1.0),
            account_value=max(num(row.get("account_value_usd")), 30_000.0),
            historical_quality_score=quality_score,
            historical_confidence_score=confidence_score,
        )
    return addresses, profiles, selected


def is_okx_row(row: dict[str, str], okx_symbols: set[str]) -> bool:
    coin = normalize_coin(row.get("coin") or row.get("source_coin") or "")
    if coin not in okx_symbols:
        return False
    okx = str(row.get("okx_tradable") or "").lower()
    copyable = str(row.get("copyable_major") or "").lower()
    return okx in {"yes", "true", "1"} or copyable in {"yes", "true", "1"} or bool(coin)


def load_closed_positions(path: Path, addresses: set[str], okx_symbols: set[str]) -> list[dict[str, str]]:
    rows = []
    for row in read_csv(path):
        address = str(row.get("address") or "").strip().lower()
        if address not in addresses:
            continue
        if not is_okx_row(row, okx_symbols):
            continue
        side = str(row.get("side") or "").upper()
        if side not in {"LONG", "SHORT"}:
            continue
        row = dict(row)
        row["address"] = address
        row["coin"] = normalize_coin(row.get("coin") or row.get("source_coin") or "")
        rows.append(row)
    return rows


def build_skill_profile(rows: list[dict[str, str]], general: GeneralProfile, side_profile: bool) -> SkillProfile:
    pnls = [num(r.get("net_pnl_usd")) for r in rows]
    allocs = [num(r.get("max_fill_balance_pct")) for r in rows if num(r.get("max_fill_balance_pct")) >= 0]
    gross_profit = sum(x for x in pnls if x > 0)
    gross_loss = sum(x for x in pnls if x < 0)
    net_pnl = sum(pnls)
    closed = len(rows)
    wins = sum(1 for x in pnls if x > 0)
    losses = sum(1 for x in pnls if x < 0)
    profit_factor = 999.0 if gross_loss == 0 and gross_profit > 0 else (gross_profit / abs(gross_loss) if gross_loss else 0.0)
    one_trade = 1.0 if gross_profit <= 0 else clamp(max([x for x in pnls if x > 0] or [0.0]) / gross_profit, 0.0, 1.0)
    account_value = max(general.account_value, 30_000.0)
    pnl_score = clamp((net_pnl / account_value + 0.10) / 0.25, 0.0, 1.0)
    sample_divisor = 8.0 if side_profile else 10.0
    sample_score = clamp(closed / sample_divisor, 0.0, 1.0)
    win_rate = wins / closed * 100.0 if closed else 0.0
    winrate_quality = clamp(win_rate / 100.0, 0.0, 1.0) * sample_score
    pf_score = clamp((min(profit_factor, 10.0) - 1.0) / 4.0, 0.0, 1.0)
    consistency = 1.0 - one_trade
    if side_profile:
        raw_skill = 0.32 * pnl_score + 0.22 * winrate_quality + 0.18 * sample_score + 0.18 * pf_score + 0.10 * consistency
        sample_confidence = clamp(sample_score * 0.75 + general.confidence * 0.25, 0.0, 1.0)
    else:
        raw_skill = 0.30 * pnl_score + 0.25 * winrate_quality + 0.20 * sample_score + 0.15 * pf_score + 0.10 * consistency
        sample_confidence = clamp(sample_score * 0.70 + general.confidence * 0.30, 0.0, 1.0)
    return SkillProfile(
        closed_positions=closed,
        wins=wins,
        losses=losses,
        net_pnl=net_pnl,
        gross_profit=gross_profit,
        gross_loss=gross_loss,
        profit_factor=profit_factor,
        avg_alloc_pct=average(allocs),
        median_alloc_pct=percentile(allocs, 0.50),
        p75_alloc_pct=percentile(allocs, 0.75),
        p90_alloc_pct=percentile(allocs, 0.90),
        max_alloc_pct=max(allocs or [0.0]),
        skill=clamp(raw_skill, 0.0, 1.0),
        sample_confidence=sample_confidence,
    )


def build_profiles(
    closed_positions: list[dict[str, str]],
    general_profiles: dict[str, GeneralProfile],
) -> tuple[dict[tuple[str, str], SkillProfile], dict[tuple[str, str, str], SkillProfile]]:
    by_coin: dict[tuple[str, str], list[dict[str, str]]] = defaultdict(list)
    by_side: dict[tuple[str, str, str], list[dict[str, str]]] = defaultdict(list)
    for row in closed_positions:
        address = row["address"]
        coin = row["coin"]
        side = str(row.get("side") or "").upper()
        by_coin[(address, coin)].append(row)
        by_side[(address, coin, side)].append(row)

    coin_profiles = {
        key: build_skill_profile(rows, general_profiles[key[0]], side_profile=False)
        for key, rows in by_coin.items()
        if key[0] in general_profiles
    }
    side_profiles = {
        key: build_skill_profile(rows, general_profiles[key[0]], side_profile=True)
        for key, rows in by_side.items()
        if key[0] in general_profiles
    }
    return coin_profiles, side_profiles


def choose_event_file(run_dir: Path, reports_root: Path, address: str) -> Path | None:
    direct = run_dir / address / "position_events.csv"
    if direct.exists():
        return direct
    candidates = list(reports_root.glob(f"**/{address}/position_events.csv"))
    if not candidates:
        return None

    def row_count(path: Path) -> int:
        try:
            return max(sum(1 for _ in path.open("r", encoding="utf-8-sig")) - 1, 0)
        except OSError:
            return 0

    return max(candidates, key=row_count)


def load_position_events(
    run_dir: Path,
    addresses: list[str],
    okx_symbols: set[str],
    reports_root: Path,
) -> tuple[list[PositionEvent], dict[str, str]]:
    events: list[PositionEvent] = []
    sources: dict[str, str] = {}
    for address in addresses:
        path = choose_event_file(run_dir, reports_root, address)
        if path is None:
            continue
        sources[address] = str(path)
        for row in read_csv(path):
            coin = normalize_coin(row.get("coin") or "")
            if coin not in okx_symbols:
                continue
            t = parse_time(row.get("time") or "")
            price = num(row.get("price"))
            if price <= 0:
                continue
            account_value = num(row.get("account_value_usd"))
            events.append(
                PositionEvent(
                    time=t,
                    address=address,
                    coin=coin,
                    price=price,
                    end_position=num(row.get("end_position")),
                    account_value=account_value,
                )
            )
    events.sort(key=lambda x: x.time)
    return events, sources


def post_info(payload: dict[str, Any], max_retries: int = 6) -> Any:
    body = json.dumps(payload).encode("utf-8")
    for attempt in range(max_retries + 1):
        request = Request(INFO_URL, data=body, method="POST", headers={"Content-Type": "application/json"})
        try:
            with urlopen(request, timeout=30) as response:
                return json.loads(response.read().decode("utf-8"))
        except HTTPError as exc:
            if exc.code == 429 and attempt < max_retries:
                wait = min(2**attempt, 20)
                print(f"Hyperliquid 429 for {payload.get('type')}; retry in {wait}s", flush=True)
                time.sleep(wait)
                continue
            detail = exc.read().decode("utf-8", errors="replace")
            raise RuntimeError(f"Hyperliquid HTTP {exc.code}: {detail}") from exc
        except (URLError, TimeoutError) as exc:
            if attempt < max_retries:
                wait = min(2**attempt, 20)
                print(f"Hyperliquid network error; retry in {wait}s: {exc}", flush=True)
                time.sleep(wait)
                continue
            raise RuntimeError(f"Hyperliquid network error: {exc}") from exc
    raise RuntimeError("Hyperliquid request failed after retries")


def candle_cache_path(cache_dir: Path, coin: str, interval: str, start: datetime, end: datetime) -> Path:
    return cache_dir / f"{coin}_{interval}_{to_ms(start)}_{to_ms(end)}.json"


def load_candles(
    coins: list[str],
    start: datetime,
    end: datetime,
    interval: str,
    cache_dir: Path,
    fetch: bool,
) -> dict[str, list[tuple[datetime, float]]]:
    result: dict[str, list[tuple[datetime, float]]] = {}
    cache_dir.mkdir(parents=True, exist_ok=True)
    for idx, coin in enumerate(coins, start=1):
        path = candle_cache_path(cache_dir, coin, interval, start, end)
        payload: Any = None
        if path.exists():
            try:
                payload = json.loads(path.read_text(encoding="utf-8"))
            except json.JSONDecodeError:
                payload = None
        if payload is None and fetch:
            body = {
                "type": "candleSnapshot",
                "req": {
                    "coin": coin,
                    "interval": interval,
                    "startTime": to_ms(start),
                    "endTime": to_ms(end),
                },
            }
            try:
                payload = post_info(body)
                path.write_text(json.dumps(payload), encoding="utf-8")
                time.sleep(0.04)
            except RuntimeError as exc:
                print(f"candle fetch failed for {coin}: {exc}", flush=True)
                payload = []
        rows: list[tuple[datetime, float]] = []
        if isinstance(payload, list):
            for item in payload:
                if not isinstance(item, dict):
                    continue
                close = num(item.get("c"))
                ts = int(item.get("T") or item.get("t") or 0)
                if close > 0 and ts > 0:
                    rows.append((datetime.fromtimestamp(ts / 1000, tz=timezone.utc), close))
        rows.sort(key=lambda x: x[0])
        result[coin] = rows
        if fetch and idx % 25 == 0:
            print(f"candles {idx}/{len(coins)}", flush=True)
    return result


def price_at(
    coin: str,
    t: datetime,
    candles: dict[str, list[tuple[datetime, float]]],
    event_prices: dict[str, list[tuple[datetime, float]]],
) -> float | None:
    for source in (candles, event_prices):
        rows = source.get(coin) or []
        if not rows:
            continue
        lo, hi = 0, len(rows) - 1
        best = -1
        while lo <= hi:
            mid = (lo + hi) // 2
            if rows[mid][0] <= t:
                best = mid
                lo = mid + 1
            else:
                hi = mid - 1
        if best >= 0:
            return rows[best][1]
    return None


def saturation_pct(coin: str) -> float:
    if coin in {"BTC", "ETH"}:
        return 30.0
    if coin == "SOL":
        return 25.0
    if coin in {"HYPE", "XRP", "SUI", "AVAX", "LINK", "DOGE", "BNB"}:
        return 20.0
    return 12.0


def allocation_conviction(current_alloc_pct: float, side_profile: SkillProfile | None, coin_profile: SkillProfile | None) -> float:
    if side_profile is None or side_profile.p90_alloc_pct <= 0:
        if coin_profile is None or coin_profile.p90_alloc_pct <= 0:
            return clamp(current_alloc_pct / 20.0, 0.15, 0.70)
        if current_alloc_pct >= coin_profile.p90_alloc_pct:
            return 1.0
        if current_alloc_pct >= coin_profile.p75_alloc_pct:
            return 0.80
        if current_alloc_pct >= coin_profile.median_alloc_pct:
            return 0.65
        return 0.50

    median = max(side_profile.median_alloc_pct, side_profile.avg_alloc_pct)
    if side_profile.p90_alloc_pct > 0 and current_alloc_pct > side_profile.p90_alloc_pct * 2.0:
        return 0.80
    if current_alloc_pct >= side_profile.p90_alloc_pct:
        return 1.0
    if current_alloc_pct >= side_profile.p75_alloc_pct:
        return 0.85
    if median > 0 and current_alloc_pct >= median:
        return 0.70
    if median > 0 and current_alloc_pct >= median * 0.5:
        return 0.45
    return 0.20


def normalized_exposure(
    current_alloc_pct: float,
    side_profile: SkillProfile | None,
    coin_profile: SkillProfile | None,
    coin: str,
) -> float:
    if side_profile is not None and side_profile.p90_alloc_pct > 0:
        saturation = max(side_profile.p90_alloc_pct, side_profile.median_alloc_pct * 2.0, 1.0)
        return clamp(current_alloc_pct / saturation, 0.0, 1.0)
    if coin_profile is not None and coin_profile.p90_alloc_pct > 0:
        saturation = max(coin_profile.p90_alloc_pct, coin_profile.median_alloc_pct * 2.0, 1.0)
        return clamp(current_alloc_pct / saturation, 0.0, 1.0)
    return clamp(current_alloc_pct / saturation_pct(coin), 0.0, 1.0)


def risk_adjustment(active_notional: float, account_value: float) -> float:
    if account_value <= 0:
        return 0.5
    gross_pct = active_notional / account_value * 100.0
    if gross_pct <= 150.0:
        return 1.0
    if gross_pct <= 300.0:
        return 0.85
    if gross_pct <= 600.0:
        return 0.65
    return 0.45


def quality_score(rows: list[ExposureRow], conflict: float) -> float:
    total = sum(abs(x.weighted_signal) for x in rows)
    if total <= 0:
        return 0.0

    def weighted(selector) -> float:
        return sum(abs(x.weighted_signal) * selector(x) for x in rows) / total

    quality = 100.0 * (
        0.35 * weighted(lambda x: x.coin_skill)
        + 0.25 * weighted(lambda x: x.sample_confidence)
        + 0.15 * (1.0 - conflict)
        + 0.15 * 1.0
        + 0.10 * 1.0
    )
    return clamp(quality, 0.0, 100.0)


def decide_action(direction_score: float, quality: float, conflict: float, participation: float) -> str:
    if abs(direction_score) < 25.0:
        return "WATCH"
    if quality < 45.0:
        return "WATCH"
    if conflict > 0.45:
        return "WATCH"
    if participation < 0.08:
        return "WATCH"
    return "OPEN_LONG" if direction_score > 0 else "OPEN_SHORT"


def build_consensus(
    active: dict[tuple[str, str], ActiveSourcePosition],
    general_profiles: dict[str, GeneralProfile],
    coin_profiles: dict[tuple[str, str], SkillProfile],
    side_profiles: dict[tuple[str, str, str], SkillProfile],
) -> dict[str, ConsensusSnapshot]:
    active_by_trader: dict[str, float] = defaultdict(float)
    for position in active.values():
        active_by_trader[position.address] += position.current_notional

    exposures: list[ExposureRow] = []
    for position in active.values():
        general = general_profiles.get(position.address)
        if general is None or position.account_value <= 0 or position.current_notional <= 0:
            continue
        coin_profile = coin_profiles.get((position.address, position.coin))
        side_profile = side_profiles.get((position.address, position.coin, position.side))
        current_alloc_pct = position.current_notional / position.account_value * 100.0
        norm = normalized_exposure(current_alloc_pct, side_profile, coin_profile, position.coin)
        conviction = allocation_conviction(current_alloc_pct, side_profile, coin_profile)
        coin_skill = (
            side_profile.skill
            if side_profile is not None
            else coin_profile.skill
            if coin_profile is not None
            else clamp(general.quality * 0.55, 0.05, 0.35)
        )
        sample_confidence = (
            side_profile.sample_confidence
            if side_profile is not None
            else coin_profile.sample_confidence
            if coin_profile is not None
            else 0.2
        )
        risk = risk_adjustment(active_by_trader[position.address], position.account_value)
        direction = 1.0 if position.side == "LONG" else -1.0
        weighted_signal = direction * norm * general.quality * coin_skill * sample_confidence * conviction * risk
        exposures.append(
            ExposureRow(
                address=position.address,
                coin=position.coin,
                side=position.side,
                weighted_signal=weighted_signal,
                current_notional=position.current_notional,
                current_alloc_pct=current_alloc_pct,
                coin_skill=coin_skill,
                sample_confidence=sample_confidence,
                allocation_conviction=conviction,
                normalized_exposure=norm,
                risk_adjustment=risk,
            )
        )

    total_power = sum(abs(x.weighted_signal) for x in exposures)
    snapshots: dict[str, ConsensusSnapshot] = {}
    if total_power <= 0:
        return snapshots

    by_coin: dict[str, list[ExposureRow]] = defaultdict(list)
    for row in exposures:
        by_coin[row.coin].append(row)

    for coin, rows in by_coin.items():
        long_power = sum(x.weighted_signal for x in rows if x.weighted_signal > 0)
        short_power = sum(abs(x.weighted_signal) for x in rows if x.weighted_signal < 0)
        gross = long_power + short_power
        if gross <= 0:
            continue
        net_signal = (long_power - short_power) / gross
        participation = gross / total_power
        conflict = min(long_power, short_power) / max(long_power, short_power) if max(long_power, short_power) > 0 else 0.0
        direction_score = 100.0 * net_signal * math.sqrt(participation) * (1.0 - conflict)
        quality = quality_score(rows, conflict)
        action = decide_action(direction_score, quality, conflict, participation)
        snapshots[coin] = ConsensusSnapshot(
            coin=coin,
            direction_score=direction_score,
            quality_score=quality,
            long_power=long_power,
            short_power=short_power,
            conflict_ratio=conflict,
            participation=participation,
            action=action,
            contributor_count=len(rows),
        )
    return snapshots


def mark_active_positions(
    active: dict[tuple[str, str], ActiveSourcePosition],
    t: datetime,
    candles: dict[str, list[tuple[datetime, float]]],
    event_prices: dict[str, list[tuple[datetime, float]]],
) -> int:
    missing = 0
    for key, position in list(active.items()):
        price = price_at(position.coin, t, candles, event_prices) or position.last_event_price
        if price <= 0:
            missing += 1
            continue
        position.current_price = price
        position.current_notional = abs(position.qty) * price
        position.current_alloc_pct = position.current_notional / position.account_value * 100.0 if position.account_value > 0 else 0.0
        active[key] = position
    return missing


def position_unrealized(
    positions: dict[str, SimPosition],
    t: datetime,
    candles: dict[str, list[tuple[datetime, float]]],
    event_prices: dict[str, list[tuple[datetime, float]]],
) -> float:
    total = 0.0
    for coin, position in positions.items():
        price = price_at(coin, t, candles, event_prices)
        if price is None:
            continue
        total += position.unrealized(price)
    return total


def equity_now(
    cash: float,
    positions: dict[str, SimPosition],
    t: datetime,
    candles: dict[str, list[tuple[datetime, float]]],
    event_prices: dict[str, list[tuple[datetime, float]]],
) -> float:
    return cash + position_unrealized(positions, t, candles, event_prices)


def rebalance_position(
    cash: float,
    positions: dict[str, SimPosition],
    coin: str,
    target_notional: float,
    price: float,
    cost_bps: float,
) -> tuple[float, float, float, float, float]:
    position = positions.setdefault(coin, SimPosition())
    old_notional = position.signed_notional(price)
    target_qty = target_notional / price if price > 0 else 0.0
    delta_qty = target_qty - position.qty
    delta_notional = delta_qty * price
    turnover = abs(delta_notional)
    cost = turnover * cost_bps / 10_000.0
    realized = 0.0

    old_qty = position.qty
    if abs(old_qty) <= 1e-12:
        position.qty = target_qty
        position.avg_entry = price if abs(target_qty) > 1e-12 else 0.0
        cash -= cost
        return cash, realized, cost, old_notional, delta_notional

    old_sign = sign(old_qty)
    new_sign = sign(target_qty)

    if new_sign == 0:
        realized = abs(old_qty) * (price - position.avg_entry) * old_sign
        position.qty = 0.0
        position.avg_entry = 0.0
        cash += realized - cost
        return cash, realized, cost, old_notional, delta_notional

    if old_sign == new_sign:
        if abs(target_qty) >= abs(old_qty):
            added_abs = abs(target_qty) - abs(old_qty)
            total_abs = abs(target_qty)
            position.avg_entry = ((position.avg_entry * abs(old_qty)) + (price * added_abs)) / total_abs if total_abs > 0 else 0.0
            position.qty = target_qty
            cash -= cost
            return cash, realized, cost, old_notional, delta_notional

        closed_abs = abs(old_qty) - abs(target_qty)
        realized = closed_abs * (price - position.avg_entry) * old_sign
        position.qty = target_qty
        cash += realized - cost
        return cash, realized, cost, old_notional, delta_notional

    realized = abs(old_qty) * (price - position.avg_entry) * old_sign
    position.qty = target_qty
    position.avg_entry = price
    cash += realized - cost
    return cash, realized, cost, old_notional, delta_notional


def target_notionals(
    consensus: dict[str, ConsensusSnapshot],
    multiplier: float,
    equity: float,
    leverage: float,
    max_coin_margin_pct: float,
    max_total_margin_pct: float,
    min_order_notional: float,
    score_threshold: float,
    result: SimResult,
) -> dict[str, float]:
    max_coin_notional = max(equity, 0.0) * leverage * max_coin_margin_pct
    max_total_notional = max(equity, 0.0) * leverage * max_total_margin_pct
    targets: dict[str, float] = {}
    for coin, snapshot in consensus.items():
        if snapshot.action not in {"OPEN_LONG", "OPEN_SHORT"} or abs(snapshot.direction_score) < score_threshold:
            continue
        raw_target = snapshot.direction_score * multiplier
        raw_target = clamp(raw_target, -max_coin_notional, max_coin_notional)
        if abs(raw_target) < min_order_notional:
            result.below_min_target_count += 1
            continue
        targets[coin] = raw_target

    gross = sum(abs(x) for x in targets.values())
    if gross > max_total_notional > 0:
        scale = max_total_notional / gross
        targets = {coin: value * scale for coin, value in targets.items()}
    return targets


def simulate(
    *,
    requested_days: int,
    start: datetime,
    end: datetime,
    events: list[PositionEvent],
    general_profiles: dict[str, GeneralProfile],
    coin_profiles: dict[tuple[str, str], SkillProfile],
    side_profiles: dict[tuple[str, str, str], SkillProfile],
    candles: dict[str, list[tuple[datetime, float]]],
    event_prices: dict[str, list[tuple[datetime, float]]],
    multiplier: float,
    initial_equity: float,
    leverage: float,
    max_coin_margin_pct: float,
    max_total_margin_pct: float,
    min_order_notional: float,
    min_rebalance_notional: float,
    score_threshold: float,
    fee_bps: float,
    slippage_bps: float,
    step: timedelta,
) -> SimResult:
    result = SimResult(
        requested_days=requested_days,
        simulated_days=(end - start).total_seconds() / 86_400.0,
        multiplier=multiplier,
        initial_equity=initial_equity,
        final_equity=initial_equity,
        pnl_usd=0.0,
        pnl_pct=0.0,
        max_drawdown_pct=0.0,
    )
    active: dict[tuple[str, str], ActiveSourcePosition] = {}
    sim_positions: dict[str, SimPosition] = {}
    cash = initial_equity
    peak_equity = initial_equity
    event_index = 0
    cost_bps = fee_bps + slippage_bps
    relevant_events = [x for x in events if x.time <= end]

    t = start
    while t <= end:
        while event_index < len(relevant_events) and relevant_events[event_index].time <= t:
            event = relevant_events[event_index]
            key = (event.address, event.coin)
            if abs(event.end_position) <= 1e-12:
                active.pop(key, None)
            else:
                account_value = event.account_value if event.account_value > 0 else general_profiles[event.address].account_value
                side = "LONG" if event.end_position > 0 else "SHORT"
                notional = abs(event.end_position) * event.price
                active[key] = ActiveSourcePosition(
                    address=event.address,
                    coin=event.coin,
                    qty=event.end_position,
                    side=side,
                    last_event_price=event.price,
                    account_value=account_value,
                    current_price=event.price,
                    current_notional=notional,
                    current_alloc_pct=notional / account_value * 100.0 if account_value > 0 else 0.0,
                )
            event_index += 1

        result.missing_price_count += mark_active_positions(active, t, candles, event_prices)
        consensus = build_consensus(active, general_profiles, coin_profiles, side_profiles)
        if consensus:
            result.consensus_points += 1

        equity = equity_now(cash, sim_positions, t, candles, event_prices)
        targets = target_notionals(
            consensus,
            multiplier,
            equity,
            leverage,
            max_coin_margin_pct,
            max_total_margin_pct,
            min_order_notional,
            score_threshold,
            result,
        )

        for coin in sorted(set(sim_positions) | set(targets)):
            price = price_at(coin, t, candles, event_prices)
            if price is None or price <= 0:
                result.missing_price_count += 1
                continue
            current = sim_positions.get(coin, SimPosition()).signed_notional(price)
            target = targets.get(coin, 0.0)
            delta = target - current
            should_close = abs(target) <= 1e-12 and abs(current) > 1e-12
            if not should_close and abs(delta) < min_rebalance_notional:
                continue

            cash, realized, cost, old_notional, delta_notional = rebalance_position(
                cash,
                sim_positions,
                coin,
                target,
                price,
                cost_bps,
            )
            equity_after = equity_now(cash, sim_positions, t, candles, event_prices)
            result.fees_and_slippage += cost
            result.coin_pnl[coin] += realized - cost
            result.coin_turnover[coin] += abs(delta_notional)
            result.trades.append(
                SimTrade(
                    time=t,
                    coin=coin,
                    old_notional=old_notional,
                    target_notional=target,
                    delta_notional=delta_notional,
                    price=price,
                    realized_pnl=realized,
                    cost=cost,
                    equity_after=equity_after,
                )
            )

        equity = equity_now(cash, sim_positions, t, candles, event_prices)
        peak_equity = max(peak_equity, equity)
        dd = (peak_equity - equity) / peak_equity * 100.0 if peak_equity > 0 else 0.0
        result.max_drawdown_pct = max(result.max_drawdown_pct, dd)
        result.equity_curve.append(
            {
                "time": t.isoformat(),
                "equity": round(equity, 6),
                "cash": round(cash, 6),
                "open_positions": sum(1 for p in sim_positions.values() if abs(p.qty) > 1e-12),
                "active_source_positions": len(active),
                "consensus_coins": len(consensus),
            }
        )
        t += step

    # Close remaining simulated positions at the final price.
    for coin, position in list(sim_positions.items()):
        if abs(position.qty) <= 1e-12:
            continue
        price = price_at(coin, end, candles, event_prices)
        if price is None or price <= 0:
            continue
        cash, realized, cost, old_notional, delta_notional = rebalance_position(
            cash,
            sim_positions,
            coin,
            0.0,
            price,
            cost_bps,
        )
        result.fees_and_slippage += cost
        result.coin_pnl[coin] += realized - cost
        result.coin_turnover[coin] += abs(delta_notional)
        result.trades.append(
            SimTrade(
                time=end,
                coin=coin,
                old_notional=old_notional,
                target_notional=0.0,
                delta_notional=delta_notional,
                price=price,
                realized_pnl=realized,
                cost=cost,
                equity_after=cash,
            )
        )

    result.final_equity = cash
    result.pnl_usd = result.final_equity - initial_equity
    result.pnl_pct = result.pnl_usd / initial_equity * 100.0 if initial_equity else 0.0
    return result


def run_backtest(args: argparse.Namespace) -> Path:
    run_dir = Path(args.run_dir)
    scoreboard_path = Path(args.scoreboard)
    positions_path = Path(args.positions)
    okx_symbols_path = Path(args.okx_symbols)
    reports_root = Path(args.reports_root)
    out_dir = Path(args.out_dir) if args.out_dir else DEFAULT_OUT_ROOT / datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    out_dir.mkdir(parents=True, exist_ok=True)

    okx_symbols = load_okx_symbols(okx_symbols_path)
    addresses, general_profiles, selected_rows = load_scoreboard(scoreboard_path, args.top_traders)
    closed_positions = load_closed_positions(positions_path, set(addresses), okx_symbols)
    coin_profiles, side_profiles = build_profiles(closed_positions, general_profiles)
    events, event_sources = load_position_events(run_dir, addresses, okx_symbols, reports_root)
    if not events:
        raise RuntimeError("No replayable position events found.")

    event_prices: dict[str, list[tuple[datetime, float]]] = defaultdict(list)
    for event in events:
        event_prices[event.coin].append((event.time, event.price))
    for rows in event_prices.values():
        rows.sort(key=lambda x: x[0])

    min_time = min(x.time for x in events)
    max_time = max(x.time for x in events)
    coverage_days = (max_time - min_time).total_seconds() / 86_400.0
    requested_days = [int(x.strip()) for x in str(args.days).split(",") if x.strip()]
    multipliers = [float(x.strip()) for x in str(args.multipliers).split(",") if x.strip()]
    step = timedelta(minutes=args.step_minutes)
    candle_start = min_time - timedelta(hours=2)
    candle_end = max_time + timedelta(hours=2)
    coins = sorted({x.coin for x in events})
    print(
        f"Replay dataset: traders={len(addresses)} events={len(events)} coins={len(coins)} "
        f"closed_positions={len(closed_positions)} coverage={coverage_days:.2f}d",
        flush=True,
    )
    candles = load_candles(
        coins,
        candle_start,
        candle_end,
        args.candle_interval,
        Path(args.cache_dir),
        fetch=not args.no_fetch_candles,
    )

    summary_rows: list[dict[str, Any]] = []
    all_results: list[SimResult] = []
    for days in requested_days:
        start = max(max_time - timedelta(days=days), min_time)
        end = max_time
        simulated_days = (end - start).total_seconds() / 86_400.0
        if simulated_days <= 0:
            continue
        for multiplier in multipliers:
            result = simulate(
                requested_days=days,
                start=start,
                end=end,
                events=events,
                general_profiles=general_profiles,
                coin_profiles=coin_profiles,
                side_profiles=side_profiles,
                candles=candles,
                event_prices=event_prices,
                multiplier=multiplier,
                initial_equity=args.initial_equity,
                leverage=args.leverage,
                max_coin_margin_pct=args.max_coin_margin_pct,
                max_total_margin_pct=args.max_total_margin_pct,
                min_order_notional=args.min_order_notional,
                min_rebalance_notional=args.min_rebalance_notional,
                score_threshold=args.score_threshold,
                fee_bps=args.fee_bps,
                slippage_bps=args.slippage_bps,
                step=step,
            )
            all_results.append(result)
            best_coin = max(result.coin_pnl.items(), key=lambda x: x[1])[0] if result.coin_pnl else ""
            worst_coin = min(result.coin_pnl.items(), key=lambda x: x[1])[0] if result.coin_pnl else ""
            summary_rows.append(
                {
                    "requested_days": days,
                    "simulated_days": round(result.simulated_days, 4),
                    "coverage_limited": "yes" if result.simulated_days < days - 0.05 else "no",
                    "multiplier": result.multiplier,
                    "initial_equity": round(result.initial_equity, 4),
                    "final_equity": round(result.final_equity, 4),
                    "pnl_usd": round(result.pnl_usd, 4),
                    "pnl_pct": round(result.pnl_pct, 4),
                    "max_drawdown_pct": round(result.max_drawdown_pct, 4),
                    "trade_count": len(result.trades),
                    "fees_slippage_usd": round(result.fees_and_slippage, 4),
                    "below_min_target_count": result.below_min_target_count,
                    "missing_price_count": result.missing_price_count,
                    "consensus_points": result.consensus_points,
                    "best_coin": best_coin,
                    "best_coin_pnl": round(result.coin_pnl.get(best_coin, 0.0), 4) if best_coin else 0,
                    "worst_coin": worst_coin,
                    "worst_coin_pnl": round(result.coin_pnl.get(worst_coin, 0.0), 4) if worst_coin else 0,
                }
            )
            suffix = f"{days}d_m{str(multiplier).replace('.', 'p')}"
            write_csv(out_dir / f"equity_curve_{suffix}.csv", result.equity_curve)
            write_csv(
                out_dir / f"trades_{suffix}.csv",
                [
                    {
                        "time": x.time.isoformat(),
                        "coin": x.coin,
                        "old_notional": round(x.old_notional, 6),
                        "target_notional": round(x.target_notional, 6),
                        "delta_notional": round(x.delta_notional, 6),
                        "price": round(x.price, 8),
                        "realized_pnl": round(x.realized_pnl, 6),
                        "cost": round(x.cost, 6),
                        "equity_after": round(x.equity_after, 6),
                    }
                    for x in result.trades
                ],
            )
            write_csv(
                out_dir / f"coin_pnl_{suffix}.csv",
                [
                    {
                        "coin": coin,
                        "pnl_usd": round(pnl, 6),
                        "turnover_usd": round(result.coin_turnover.get(coin, 0.0), 6),
                    }
                    for coin, pnl in sorted(result.coin_pnl.items(), key=lambda x: x[1], reverse=True)
                ],
            )
            print(
                f"{days}d m={multiplier}: final=${result.final_equity:.2f} "
                f"pnl={result.pnl_pct:+.2f}% trades={len(result.trades)} dd={result.max_drawdown_pct:.2f}%",
                flush=True,
            )

    write_csv(out_dir / "summary.csv", sorted(summary_rows, key=lambda x: (x["requested_days"], -float(x["pnl_usd"]))))
    best = max(summary_rows, key=lambda x: float(x["pnl_usd"])) if summary_rows else None
    metadata = {
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "method": "event_replay_with_in_sample_side_profiles",
        "notes": [
            "Position events are replayed chronologically; current source allocation uses abs(end_position) * mark price / source account value.",
            "Trader coin/side skill profiles are built from the same historical run, so multiplier search is in-sample and must be validated with future live data.",
            "Target OKX notional = direction_score * multiplier, capped by equity, leverage, max coin margin, and max total margin.",
            "Fees and slippage are applied on every simulated rebalance.",
        ],
        "run_dir": str(run_dir),
        "scoreboard": str(scoreboard_path),
        "positions": str(positions_path),
        "okx_symbols": str(okx_symbols_path),
        "top_traders": args.top_traders,
        "selected_traders": addresses,
        "event_sources": event_sources,
        "events": len(events),
        "closed_positions": len(closed_positions),
        "coins": coins,
        "coverage_days": coverage_days,
        "best": best,
        "parameters": vars(args),
    }
    (out_dir / "metadata.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    write_report(out_dir, metadata, summary_rows)
    return out_dir


def write_report(out_dir: Path, metadata: dict[str, Any], summary_rows: list[dict[str, Any]]) -> None:
    sorted_rows = sorted(summary_rows, key=lambda x: float(x["pnl_usd"]), reverse=True)
    lines = [
        "# Hyperliquid Consensus Backtest",
        "",
        f"Generated: {metadata['generated_at']}",
        f"Method: `{metadata['method']}`",
        f"Traders: {metadata['top_traders']}",
        f"Replay events: {metadata['events']}",
        f"Closed positions for profiles: {metadata['closed_positions']}",
        f"Coverage days: {metadata['coverage_days']:.2f}",
        "",
        "Important caveat: coin/side skill profiles are built from the same historical run. Treat this as multiplier discovery, not proof of future profitability.",
        "",
        "## Best Runs",
        "",
        "| Requested | Simulated | Multiplier | Final | PnL | PnL % | Max DD % | Trades | Best coin | Worst coin |",
        "|---:|---:|---:|---:|---:|---:|---:|---:|---|---|",
    ]
    for row in sorted_rows[:12]:
        lines.append(
            f"| {row['requested_days']} | {row['simulated_days']} | {row['multiplier']} | "
            f"${row['final_equity']} | ${row['pnl_usd']} | {row['pnl_pct']}% | "
            f"{row['max_drawdown_pct']}% | {row['trade_count']} | "
            f"{row['best_coin']} ${row['best_coin_pnl']} | {row['worst_coin']} ${row['worst_coin_pnl']} |"
        )
    lines.extend(
        [
            "",
            "## Formula",
            "",
            "`target_notional_usd = direction_score * multiplier`",
            "",
            "Then target notional is capped by:",
            "",
            "- `equity * leverage * max_coin_margin_pct` per coin",
            "- `equity * leverage * max_total_margin_pct` across all coins",
            "- `min_order_notional` for executable OKX-like behavior",
            "- `min_rebalance_notional` to avoid tiny churn",
            "",
        ]
    )
    (out_dir / "report.md").write_text("\n".join(lines), encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Backtest Hyperliquid consensus-to-OKX exposure multipliers.")
    parser.add_argument("--run-dir", default=str(DEFAULT_RUN_DIR))
    parser.add_argument("--reports-root", default="data/reports/hyperliquid_profiles")
    parser.add_argument("--scoreboard", default=str(DEFAULT_SCOREBOARD))
    parser.add_argument("--positions", default=str(DEFAULT_POSITIONS))
    parser.add_argument("--okx-symbols", default=str(DEFAULT_OKX_SYMBOLS))
    parser.add_argument("--out-dir", default="")
    parser.add_argument("--cache-dir", default=str(DEFAULT_CACHE_DIR))
    parser.add_argument("--top-traders", type=int, default=40)
    parser.add_argument("--days", default="30,60,90")
    parser.add_argument("--multipliers", default="0.1,0.2,0.35,0.5,0.75,1,1.5,2,3")
    parser.add_argument("--initial-equity", type=float, default=100.0)
    parser.add_argument("--leverage", type=float, default=10.0)
    parser.add_argument("--max-coin-margin-pct", type=float, default=0.15)
    parser.add_argument("--max-total-margin-pct", type=float, default=0.35)
    parser.add_argument("--min-order-notional", type=float, default=10.0)
    parser.add_argument("--min-rebalance-notional", type=float, default=8.0)
    parser.add_argument("--score-threshold", type=float, default=25.0)
    parser.add_argument("--fee-bps", type=float, default=5.0)
    parser.add_argument("--slippage-bps", type=float, default=3.0)
    parser.add_argument("--step-minutes", type=int, default=60)
    parser.add_argument("--candle-interval", default="1h")
    parser.add_argument("--no-fetch-candles", action="store_true")
    return parser.parse_args()


def main() -> None:
    out_dir = run_backtest(parse_args())
    print(f"Backtest report written to {out_dir}", flush=True)


if __name__ == "__main__":
    main()
