import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { CSSProperties, KeyboardEvent as ReactKeyboardEvent, PointerEvent as ReactPointerEvent, ReactNode } from 'react'
import ForceGraph3D from 'react-force-graph-3d'
import SpriteText from 'three-spritetext'
import * as THREE from 'three'
import { Canvas, useFrame } from '@react-three/fiber'
import { OrbitControls, Stars } from '@react-three/drei'
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import {
  Activity,
  Bot,
  BrainCircuit,
  CircleDollarSign,
  Database,
  ExternalLink,
  GripVertical,
  Plus,
  Radar,
  RefreshCw,
  Send,
  ShieldCheck,
  Zap,
} from 'lucide-react'
import './App.css'

type Wallet = {
  id: number
  walletAddress: string
  label: string
  source: string
  isActive: boolean
  confidenceScore: number
  estimatedProfitUsd: number
  assetSymbol: string
  lastCheckedAt?: string
  lastSeenTxHash?: string
}

type LiveEvent = {
  id: number
  type: string
  severity: string
  walletAddress: string
  txHash: string
  symbol: string
  usdValue?: number
  summary: string
  payloadJson: string
  createdAt: string
}

type AiState = {
  biasScore: number
  direction: string
  summary: string
  eventCount: number
  lastEventAt?: string
}

type OperationsSnapshot = {
  checkedAt: string
  okx?: {
    available: boolean
    totalUsd: number
    positions: number
    mode: string
  }
  recentExecutions?: Array<{
    createdAt: string
    symbol: string
    action: string
    isSuccess: boolean
    marginUsdt: number
    confidence: number
  }>
}

type TraderScan = {
  id: number
  startUtc: string
  endUtc: string
  minimumStartingValueUsd: number
  requestedTop: number
  evaluatedWalletCount: number
  qualifiedWalletCount: number
  state: string
  progressPercent: number
  currentStage: string
  statusMessage: string
  errorMessage: string
  progressLog: TraderDiscoveryProgress[]
  createdAt: string
}

type TraderCandidate = {
  id: number
  traderScanId: number
  walletAddress: string
  startingValueUsd: number
  endingValueUsd: number
  receivedExternalUsd: number
  sentExternalUsd: number
  totalFeesUsd: number
  adjustedProfitUsd: number
  adjustedReturnPercent: number
  realizedGainUsd: number
  score: number
  startPointUtc: string
  endPointUtc: string
  chartPeriod: string
}

type TraderDiscoveryRun = {
  id: number
  provider: string
  executionId: string
  state: string
  lookbackDays: number
  minimumActiveWeeks: number
  minimumMeaningfulSwaps: number
  minimumSwapUsd: number
  candidateLimit: number
  candidateCount: number
  progressPercent: number
  currentStage: string
  statusMessage: string
  errorMessage: string
  progressLog: TraderDiscoveryProgress[]
  startedAtUtc: string
  completedAtUtc: string
  createdAt: string
}

type TraderDiscoveryProgress = {
  percent: number
  stage: string
  state: string
  message: string
  executionId: string
  timestampUtc: string
}

type TraderDiscoveryCandidate = {
  id: number
  traderDiscoveryRunId: number
  walletAddress: string
  meaningfulSwapCount: number
  activeWeekCount: number
  approvedNotionalUsd: number
  averageSwapUsd: number
  maximumDailySwaps: number
  distinctMajorAssets: number
  copyabilityScore: number
  currentCopyableValueUsd: number
  activeChainCount: number
  activeChains: string[]
  firstTradeUtc: string
  lastTradeUtc: string
}

type CoinConsensusView = {
  id: number
  coin: string
  timestamp: string
  longPower: number
  shortPower: number
  netSignal: number
  participation: number
  conflictRatio: number
  directionScore: number
  qualityScore: number
  targetSide: string
  targetNotionalUsd: number
  action: string
  skipReason: string
  contributorCount: number
  topContributorsJson: string
}

type TraderCoinExposureView = {
  traderAddress: string
  label: string
  coin: string
  side: string
  currentNotionalUsd: number
  currentAccountValueUsd: number
  currentAllocPct: number
  unrealizedPnlUsd: number
  entryPrice: number
  openedAt: string
  lastSeenAt: string
  normalizedExposure: number
  allocationConviction: number
  coinSkillScore: number
  sampleConfidence: number
  freshnessScore: number
  riskAdjustment: number
  weightedSignal: number
  isBaseline: boolean
}

type HyperliquidConsensusSnapshot = {
  checkedAt: string
  coins: CoinConsensusView[]
  exposures: TraderCoinExposureView[]
  topProfiles: Array<Record<string, string | number>>
}

type GraphNode = {
  id: string
  name: string
  kind: 'ai' | 'wallet' | 'okx' | 'event'
  color: string
  size: number
  x?: number
  y?: number
  z?: number
  wallet?: Wallet
  event?: LiveEvent
  flowCreatedAt?: number
  flowExpiresAt?: number
}

type GraphLink = {
  source: string
  target: string
  color: string
  particles: number
  flowCreatedAt?: number
  flowExpiresAt?: number
}

type Tab = 'events' | 'wallets' | 'insider' | 'chat'

type ChatAiMeta = {
  provider: string
  model: string
  mode: string
  elapsedMs: number
  source: string
  sourceWallet: string
  positions: number
  usedGroq: boolean
}

type ChatLine = {
  role: 'user' | 'ai'
  text: string
  meta?: ChatAiMeta
}

const FLOW_LIFETIME_MS = 120_000
const FLOW_FADE_START_MS = 60_000
const animatedEventTypes = new Set(['WalletActivityDetected', 'AiDecisionCompleted', 'TradeSubmitted', 'TradeRejected', 'TradeSkipped'])

function isSkippedTrade(event: LiveEvent) {
  if (event.type === 'TradeSkipped') return true
  if (event.type !== 'TradeRejected') return false
  const payload = parsePayload(event)
  return event.summary.toLowerCase().startsWith('trade skipped:') ||
    String(payload?.decision || '').toUpperCase() === 'IGNORE'
}

function isManualExecutionProbe(event: LiveEvent) {
  return parsePayload(event)?.mode === 'live-execution-probe'
}

function eventStepLabel(event: LiveEvent) {
  const payload = parsePayload(event)
  if (event.type === 'WalletActivityDetected') {
    return `${event.symbol || 'Wallet'} movement ${formatUsd(event.usdValue)}`
  }
  if (event.type === 'AiDecisionCompleted') {
    const decision = payload?.decision || payload?.action || 'Decision'
    return `AI: ${decision} ${event.symbol || ''}`.trim()
  }
  if (event.type === 'TradeSubmitted') {
    return `OKX accepted ${event.symbol || ''}`.trim()
  }
  if (event.type === 'TradeRejected') {
    const request = payload?.request
    return request
      ? `OKX rejected ${request.side || ''} ${request.symbol || event.symbol || ''}`.trim()
      : `OKX rejected ${event.symbol || ''}`.trim()
  }
  if (event.type === 'TradeSkipped') {
    return `No trade: ${event.symbol || 'ignored'}`
  }
  return event.type
}

function flowOpacity(createdAt?: number, expiresAt?: number) {
  if (!createdAt || !expiresAt) return 0.42
  const now = Date.now()
  if (now >= expiresAt) return 0
  const age = now - createdAt
  if (age <= FLOW_FADE_START_MS) return 0.72
  return Math.max(0, 0.72 * (expiresAt - now) / (FLOW_LIFETIME_MS - FLOW_FADE_START_MS))
}

async function fetchJson<T>(url: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(url, {
    credentials: 'include',
    ...options,
    headers: {
      ...(options.body ? { 'Content-Type': 'application/json' } : {}),
      ...(options.headers || {}),
    },
  })

  if (response.status === 401 || response.redirected) {
    window.location.href = '/login.html'
    throw new Error('Login required')
  }

  if (!response.ok) {
    const text = await response.text()
    let message = text || `HTTP ${response.status}`
    try {
      const payload = JSON.parse(text)
      message = payload.message || payload.error || message
    } catch {
      // Keep the raw response text when the error body is not JSON.
    }
    throw new Error(message)
  }

  return response.json()
}

function formatUsd(value?: number) {
  if (value === null || value === undefined || Number.isNaN(Number(value))) return '--'
  return `$${Number(value).toLocaleString(undefined, { maximumFractionDigits: 2 })}`
}

function shortAddress(value?: string) {
  if (!value) return '--'
  return `${value.slice(0, 6)}...${value.slice(-4)}`
}

function zerionUrl(value?: string) {
  return value ? `https://app.zerion.io/${value}/overview` : 'https://app.zerion.io/'
}

function formatTime(value?: string) {
  if (!value) return '--'
  return new Date(value).toLocaleString()
}

function formatDuration(seconds?: number) {
  const value = Number(seconds || 0)
  if (!Number.isFinite(value) || value <= 0) return '--'
  if (value >= 86400) return `${(value / 86400).toFixed(1)}d`
  if (value >= 3600) return `${(value / 3600).toFixed(1)}h`
  return `${Math.max(1, Math.round(value / 60))}m`
}

function parsePayload(event?: LiveEvent) {
  if (!event?.payloadJson) return null
  try {
    return JSON.parse(event.payloadJson)
  } catch {
    return null
  }
}

function makeCanvasSprite(
  width: number,
  height: number,
  draw: (context: CanvasRenderingContext2D) => void,
  scale: [number, number],
) {
  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height
  const context = canvas.getContext('2d')
  if (!context) return new THREE.Sprite()

  context.clearRect(0, 0, width, height)
  draw(context)
  const texture = new THREE.CanvasTexture(canvas)
  texture.needsUpdate = true
  texture.colorSpace = THREE.SRGBColorSpace

  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({
    map: texture,
    transparent: true,
    depthTest: false,
    depthWrite: false,
  }))
  sprite.scale.set(scale[0], scale[1], 1)
  sprite.renderOrder = 30
  return sprite
}

function makeOkxBillboard(totalUsd?: number) {
  return makeCanvasSprite(640, 300, (context) => {
    context.fillStyle = '#ffffff'

    const scale = 0.62
    const offsetX = 88
    const offsetY = 50
    const rect = (x: number, y: number, width: number, height: number) => {
      context.fillRect(offsetX + (x - 166) * scale, offsetY + (y - 428) * scale, width * scale, height * scale)
    }

    rect(166, 428, 224, 224)
    context.save()
    context.globalCompositeOperation = 'destination-out'
    rect(241, 503, 75, 75)
    context.restore()

    rect(428, 428, 75, 224)
    rect(503, 503, 75, 75)
    rect(577, 428, 75, 75)
    rect(577, 577, 75, 75)
    rect(689, 428, 75, 75)
    rect(689, 577, 75, 75)
    rect(764, 503, 75, 75)
    rect(838, 428, 75, 75)
    rect(838, 577, 75, 75)

    context.fillStyle = '#fed7aa'
    context.font = '800 42px Arial, Helvetica, sans-serif'
    context.textAlign = 'center'
    context.textBaseline = 'middle'
    context.fillText(formatUsd(totalUsd), 320, 235)
  }, [30, 14])
}

function AiCoreOrb({ bias }: { bias: string }) {
  const mesh = useRef<THREE.Mesh>(null)
  const color = bias === 'BULLISH' ? '#22c55e' : bias === 'BEARISH' ? '#ef4444' : '#67e8f9'

  useFrame(({ clock }) => {
    if (!mesh.current) return
    const t = clock.getElapsedTime()
    mesh.current.rotation.y = t * 0.35
    mesh.current.rotation.x = Math.sin(t * 0.4) * 0.16
    const scale = 1 + Math.sin(t * 1.8) * 0.045
    mesh.current.scale.setScalar(scale)
  })

  return (
    <>
      <Stars radius={60} depth={24} count={550} factor={3} saturation={0} fade speed={0.35} />
      <ambientLight intensity={0.35} />
      <pointLight position={[4, 3, 5]} intensity={2.3} color={color} />
      <mesh ref={mesh}>
        <dodecahedronGeometry args={[1.22, 0]} />
        <meshStandardMaterial color="#07111f" emissive={color} emissiveIntensity={0.62} roughness={0.18} metalness={0.74} />
      </mesh>
      <mesh rotation={[0.62, 0.12, 0.8]}>
        <torusGeometry args={[1.72, 0.018, 8, 96]} />
        <meshBasicMaterial color={color} transparent opacity={0.72} />
      </mesh>
      <mesh rotation={[1.35, 0.7, 0.15]}>
        <torusGeometry args={[1.38, 0.014, 8, 96]} />
        <meshBasicMaterial color="#e0f2fe" transparent opacity={0.46} />
      </mesh>
      <OrbitControls enableZoom={false} enablePan={false} autoRotate autoRotateSpeed={0.6} />
    </>
  )
}

type HyperSummary = Record<string, any>
type HyperTraderRow = Record<string, any>
type HyperTraderProfile = Record<string, any>
type HyperPositionRow = Record<string, any>

function dash(value: unknown, formatter?: (value: number) => string) {
  if (value === null || value === undefined || value === '') return '—'
  if (typeof value === 'number') return formatter ? formatter(value) : Number(value).toFixed(2)
  return String(value)
}

function copyText(value: string) {
  navigator.clipboard?.writeText(value).catch(() => undefined)
}

function routeTo(path: string) {
  window.history.pushState({}, '', path)
  window.dispatchEvent(new PopStateEvent('popstate'))
}

function HyperNav({ current }: { current: string }) {
  const items = [
    ['Overview', '/hyperliquid'],
    ['Live Leaderboard', '/hyperliquid/live-leaderboard'],
    ['Trader Profiles', '/hyperliquid/live-leaderboard'],
    ['Consensus', '/hyperliquid/consensus'],
    ['Positions', '/hyperliquid/positions'],
    ['Execution', '/hyperliquid/execution'],
    ['Settings', '/hyperliquid/execution'],
  ]
  return (
    <nav className="hyper-top-tabs">
      {items.map(([label, path]) => (
        <button key={`${label}-${path}`} className={current === path ? 'active' : ''} onClick={() => routeTo(path)}>
          {label}
        </button>
      ))}
    </nav>
  )
}

function HyperShell({ children, current, title, subtitle }: { children: ReactNode, current: string, title: string, subtitle: string }) {
  return (
    <main className="hyper-page">
      <header className="hyper-header">
        <button className="hyper-home-link" onClick={() => routeTo('/')}>Mission Control</button>
        <div>
          <p className="eyebrow">Hyperliquid Research Terminal</p>
          <h1>{title}</h1>
          <span>{subtitle}</span>
        </div>
      </header>
      <HyperNav current={current} />
      {children}
    </main>
  )
}

function HyperMetricCards({ cards }: { cards: Array<{ label: string, value: React.ReactNode, tone?: string }> }) {
  return (
    <section className="hyper-card-grid">
      {cards.map((card) => (
        <div className={`hyper-card ${card.tone || ''}`} key={card.label}>
          <span>{card.label}</span>
          <strong>{card.value}</strong>
        </div>
      ))}
    </section>
  )
}

function HyperTable({
  columns,
  rows,
  getKey,
  onRowClick,
}: {
  columns: Array<{ key: string, label: string, render?: (row: any) => React.ReactNode, numeric?: boolean }>
  rows: any[]
  getKey: (row: any, index: number) => string
  onRowClick?: (row: any) => void
}) {
  const [sortKey, setSortKey] = useState(columns[0]?.key || '')
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>(columns[0]?.key === 'rank' ? 'asc' : 'desc')
  const sortedRows = useMemo(() => {
    const list = [...rows]
    list.sort((a, b) => {
      const av = a?.[sortKey]
      const bv = b?.[sortKey]
      const an = Number(av)
      const bn = Number(bv)
      const result = Number.isFinite(an) && Number.isFinite(bn)
        ? an - bn
        : String(av ?? '').localeCompare(String(bv ?? ''))
      return sortDir === 'asc' ? result : -result
    })
    return list
  }, [rows, sortDir, sortKey])

  return (
    <div className="hyper-table-wrap">
      <table className="hyper-table">
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column.key} className={column.numeric ? 'num' : ''}>
                <button onClick={() => {
                  if (sortKey === column.key) setSortDir(sortDir === 'asc' ? 'desc' : 'asc')
                  else {
                    setSortKey(column.key)
                    setSortDir('desc')
                  }
                }}>
                  {column.label}{sortKey === column.key ? (sortDir === 'asc' ? ' ↑' : ' ↓') : ''}
                </button>
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {sortedRows.map((row, index) => (
            <tr key={getKey(row, index)} onClick={onRowClick ? () => onRowClick(row) : undefined} className={onRowClick ? 'clickable' : ''}>
              {columns.map((column) => <td key={column.key} className={column.numeric ? 'num' : ''}>{column.render ? column.render(row) : dash(row[column.key])}</td>)}
            </tr>
          ))}
          {rows.length === 0 && (
            <tr><td colSpan={columns.length} className="empty-cell">No live data available.</td></tr>
          )}
        </tbody>
      </table>
    </div>
  )
}

function HyperliquidResearchApp({ path }: { path: string }) {
  if (path.startsWith('/hyperliquid/traders/')) {
    return <HyperTraderPage address={decodeURIComponent(path.split('/').pop() || '')} path={path} />
  }
  if (path === '/hyperliquid/live-leaderboard') return <HyperLeaderboardPage path={path} />
  if (path === '/hyperliquid/consensus') return <HyperConsensusPage path={path} />
  if (path === '/hyperliquid/positions') return <HyperPositionsPage path={path} />
  if (path === '/hyperliquid/execution') return <HyperExecutionPage path={path} />
  return <HyperOverviewPage path="/hyperliquid" />
}

function HyperOverviewPage({ path }: { path: string }) {
  const [summary, setSummary] = useState<HyperSummary | null>(null)
  const [consensus, setConsensus] = useState<HyperliquidConsensusSnapshot | null>(null)
  useEffect(() => {
    fetchJson<HyperSummary>('/api/hyperliquid/live/summary').then(setSummary).catch(() => setSummary(null))
    fetchJson<HyperliquidConsensusSnapshot>('/api/hyperliquid/consensus').then(setConsensus).catch(() => setConsensus(null))
  }, [])
  return (
    <HyperShell current={path} title="Hyperliquid Overview" subtitle="Full-screen research and execution area for tracked Hyperliquid traders.">
      <HyperMetricCards cards={[
        { label: 'Tracked traders', value: summary?.trackedTraders ?? '—', tone: 'info' },
        { label: 'Active traders', value: summary?.activeTraders ?? '—', tone: 'info' },
        { label: 'Live open positions', value: summary?.liveOpenPositions ?? '—' },
        { label: 'Closed live positions', value: summary?.closedLivePositions ?? '—' },
        { label: 'Source realized PnL', value: formatUsd(summary?.sourceRealizedPnlUsd), tone: Number(summary?.sourceRealizedPnlUsd || 0) >= 0 ? 'good' : 'bad' },
        { label: 'OKX equity', value: formatUsd(summary?.currentOkxEquity), tone: 'info' },
        { label: 'Real execution', value: summary?.realExecutionMode ?? '—', tone: summary?.realExecutionMode === 'Enabled' ? 'warn' : 'muted' },
        { label: 'Consensus coins', value: consensus?.coins.length ?? '—', tone: 'info' },
      ]} />
      <section className="hyper-panel">
        <div className="hyper-panel-head">
          <strong>Current strongest consensus</strong>
          <button onClick={() => routeTo('/hyperliquid/consensus')}>Open Consensus</button>
        </div>
        <HyperTable
          columns={[
            { key: 'coin', label: 'Coin' },
            { key: 'targetSide', label: 'Direction' },
            { key: 'directionScore', label: 'Score', numeric: true, render: r => Number(r.directionScore || 0).toFixed(1) },
            { key: 'qualityScore', label: 'Quality', numeric: true, render: r => Number(r.qualityScore || 0).toFixed(1) },
            { key: 'conflictRatio', label: 'Conflict', numeric: true, render: r => `${(Number(r.conflictRatio || 0) * 100).toFixed(0)}%` },
            { key: 'action', label: 'Action' },
            { key: 'contributorCount', label: 'Traders', numeric: true },
          ]}
          rows={consensus?.coins.slice(0, 20) || []}
          getKey={(row) => row.coin}
        />
      </section>
    </HyperShell>
  )
}

function HyperLeaderboardPage({ path }: { path: string }) {
  const [summary, setSummary] = useState<HyperSummary | null>(null)
  const [rows, setRows] = useState<HyperTraderRow[]>([])
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [pnlFilter, setPnlFilter] = useState('all')
  const load = useCallback(() => {
    fetchJson<HyperSummary>('/api/hyperliquid/live/summary').then(setSummary).catch(() => setSummary(null))
    fetchJson<HyperTraderRow[]>('/api/hyperliquid/live/traders').then(setRows).catch(() => setRows([]))
  }, [])
  useEffect(() => {
    load()
    const timer = window.setInterval(load, 15000)
    return () => window.clearInterval(timer)
  }, [load])
  const filtered = rows.filter((row) => {
    const text = `${row.label || ''} ${row.address || ''}`.toLowerCase()
    if (search && !text.includes(search.toLowerCase())) return false
    if (status !== 'all' && String(row.status || '').toLowerCase() !== status) return false
    if (pnlFilter === 'positive' && Number(row.liveRealizedPnlUsd || 0) <= 0) return false
    if (pnlFilter === 'negative' && Number(row.liveRealizedPnlUsd || 0) >= 0) return false
    return true
  })
  return (
    <HyperShell current={path} title="Live Trader Leaderboard" subtitle="Full-width sortable table for manually reviewing tracked wallets before risking real money.">
      <HyperMetricCards cards={[
        { label: 'Tracked traders', value: summary?.trackedTraders ?? '—' },
        { label: 'Active traders', value: summary?.activeTraders ?? '—' },
        { label: 'Live open positions', value: summary?.liveOpenPositions ?? '—' },
        { label: 'Closed live positions', value: summary?.closedLivePositions ?? '—' },
        { label: 'Source realized PnL', value: formatUsd(summary?.sourceRealizedPnlUsd), tone: Number(summary?.sourceRealizedPnlUsd || 0) >= 0 ? 'good' : 'bad' },
        { label: 'Pure virtual PnL', value: dash(summary?.pureVirtualPnlUsd, formatUsd), tone: 'muted' },
        { label: 'Executable virtual PnL', value: dash(summary?.executableVirtualPnlUsd, formatUsd), tone: 'muted' },
        { label: 'Real OKX PnL', value: dash(summary?.realOkxPnlUsd, formatUsd), tone: 'muted' },
        { label: 'Current OKX equity', value: formatUsd(summary?.currentOkxEquity), tone: 'info' },
        { label: 'Real execution mode', value: summary?.realExecutionMode ?? '—', tone: summary?.realExecutionMode === 'Enabled' ? 'warn' : 'muted' },
      ]} />
      <section className="hyper-panel">
        <div className="hyper-toolbar">
          <input placeholder="Search label or address" value={search} onChange={e => setSearch(e.target.value)} />
          <select value={status} onChange={e => setStatus(e.target.value)}>
            <option value="all">All status</option>
            <option value="real enabled">Real enabled</option>
            <option value="shadow">Shadow</option>
            <option value="paused">Paused</option>
          </select>
          <select value={pnlFilter} onChange={e => setPnlFilter(e.target.value)}>
            <option value="all">All PnL</option>
            <option value="positive">Positive PnL</option>
            <option value="negative">Negative PnL</option>
          </select>
          <button onClick={load}>Refresh</button>
        </div>
        <HyperTable
          columns={[
            { key: 'rank', label: 'Rank', numeric: true, render: r => Number(r.rank || 0).toLocaleString(undefined, { maximumFractionDigits: 0 }) },
            { key: 'status', label: 'Status', render: r => <span className={`tag ${String(r.status || '').includes('Real') ? 'warn' : String(r.status || '') === 'Shadow' ? 'muted' : 'bad'}`}>{r.status}</span> },
            { key: 'label', label: 'Label' },
            { key: 'address', label: 'Address', render: r => <button className="copy-cell" onClick={(e) => { e.stopPropagation(); copyText(r.address) }}>{shortAddress(r.address)}</button> },
            { key: 'liveScore', label: 'Live Score', numeric: true, render: r => dash(r.liveScore) },
            { key: 'confidence', label: 'Confidence', numeric: true, render: r => dash(r.confidence) },
            { key: 'historicalScore', label: 'Historical Score', numeric: true, render: r => dash(r.historicalScore) },
            { key: 'currentAccountValue', label: 'Account Value', numeric: true, render: r => dash(r.currentAccountValue, formatUsd) },
            { key: 'liveRealizedPnlUsd', label: 'Live Realized', numeric: true, render: r => <span className={Number(r.liveRealizedPnlUsd || 0) >= 0 ? 'pos' : 'neg'}>{dash(r.liveRealizedPnlUsd, formatUsd)}</span> },
            { key: 'liveRealizedPnlPct', label: 'Live %', numeric: true, render: r => dash(r.liveRealizedPnlPct, v => `${v.toFixed(2)}%`) },
            { key: 'pureVirtualPnl', label: 'Pure Virtual', numeric: true, render: r => dash(r.pureVirtualPnl, formatUsd) },
            { key: 'executableVirtualPnl', label: 'Executable Virtual', numeric: true, render: r => dash(r.executableVirtualPnl, formatUsd) },
            { key: 'realOkxPnl', label: 'Real OKX', numeric: true, render: r => dash(r.realOkxPnl, formatUsd) },
            { key: 'openPositions', label: 'Open', numeric: true },
            { key: 'closedPositions', label: 'Closed', numeric: true },
            { key: 'wins', label: 'Wins', numeric: true, render: r => dash(r.wins) },
            { key: 'losses', label: 'Losses', numeric: true, render: r => dash(r.losses) },
            { key: 'winrate', label: 'Winrate', numeric: true, render: r => dash(r.winrate, v => `${v.toFixed(1)}%`) },
            { key: 'grossProfitUsd', label: 'Gross Profit', numeric: true, render: r => dash(r.grossProfitUsd, formatUsd) },
            { key: 'grossLossUsd', label: 'Gross Loss', numeric: true, render: r => dash(r.grossLossUsd, formatUsd) },
            { key: 'profitFactor', label: 'PF', numeric: true, render: r => dash(r.profitFactor) },
            { key: 'avgHoldSeconds', label: 'Avg Hold', numeric: true, render: r => dash(r.avgHoldSeconds, formatDuration) },
            { key: 'medianHoldSeconds', label: 'Median Hold', numeric: true, render: r => dash(r.medianHoldSeconds, formatDuration) },
            { key: 'bestTrade', label: 'Best', numeric: true, render: r => dash(r.bestTrade, formatUsd) },
            { key: 'worstTrade', label: 'Worst', numeric: true, render: r => dash(r.worstTrade, formatUsd) },
            { key: 'maxDrawdown', label: 'Max DD', numeric: true, render: r => dash(r.maxDrawdown) },
            { key: 'okxCopyablePnl', label: 'OKX Copyable PnL', numeric: true, render: r => dash(r.okxCopyablePnl, formatUsd) },
            { key: 'minOrderRejectRate', label: 'Min Reject', numeric: true, render: r => dash(r.minOrderRejectRate) },
            { key: 'conflictRate', label: 'Conflict', numeric: true, render: r => dash(r.conflictRate) },
            { key: 'lastSignalAt', label: 'Last Signal', render: r => r.lastSignalAt ? formatTime(r.lastSignalAt) : '—' },
            { key: 'topCoins', label: 'Top Coins' },
            { key: 'actions', label: 'Actions', render: r => <button onClick={(e) => { e.stopPropagation(); routeTo(`/hyperliquid/traders/${r.address}`) }}>View</button> },
          ]}
          rows={filtered}
          getKey={(row) => row.address}
          onRowClick={(row) => routeTo(`/hyperliquid/traders/${row.address}`)}
        />
      </section>
    </HyperShell>
  )
}

function HyperTraderPage({ address, path }: { address: string, path: string }) {
  const [profile, setProfile] = useState<HyperTraderProfile | null>(null)
  const [active, setActive] = useState<HyperPositionRow[]>([])
  const [closed, setClosed] = useState<HyperPositionRow[]>([])
  const [coins, setCoins] = useState<any[]>([])
  const [alloc, setAlloc] = useState<any[]>([])
  const [tab, setTab] = useState('overview')
  const load = useCallback(() => {
    fetchJson<HyperTraderProfile>(`/api/hyperliquid/live/traders/${address}`).then(setProfile).catch(() => setProfile(null))
    fetchJson<HyperPositionRow[]>(`/api/hyperliquid/live/traders/${address}/active-positions`).then(setActive).catch(() => setActive([]))
    fetchJson<HyperPositionRow[]>(`/api/hyperliquid/live/traders/${address}/closed-positions`).then(setClosed).catch(() => setClosed([]))
    fetchJson<any[]>(`/api/hyperliquid/live/traders/${address}/coin-performance`).then(setCoins).catch(() => setCoins([]))
    fetchJson<any[]>(`/api/hyperliquid/live/traders/${address}/allocation-profile`).then(setAlloc).catch(() => setAlloc([]))
  }, [address])
  useEffect(() => { load() }, [load])
  const metrics = profile?.metrics || {}
  return (
    <HyperShell current={path} title={profile?.label || shortAddress(address)} subtitle="Trader profile and copyability research terminal.">
      <section className="hyper-profile-head">
        <div>
          <span>Address</span>
          <strong>{address}</strong>
          <button onClick={() => copyText(address)}>Copy</button>
        </div>
        <div><span>Status</span><strong>{profile?.status || '—'}</strong></div>
        <div><span>Historical</span><strong>{dash(profile?.historicalScore)}</strong></div>
        <div><span>Live</span><strong>{dash(profile?.liveScore)}</strong></div>
        <div><span>Confidence</span><strong>{dash(profile?.confidence)}</strong></div>
        <div><span>Account</span><strong>{dash(profile?.currentAccountValue, formatUsd)}</strong></div>
        <div><span>Withdrawable</span><strong>{dash(profile?.currentWithdrawable, formatUsd)}</strong></div>
        <div><span>Margin Used</span><strong>{dash(profile?.currentMarginUsed, formatUsd)}</strong></div>
        <div><span>Total Notional</span><strong>{dash(profile?.totalPositionNotional, formatUsd)}</strong></div>
        <div><span>Active</span><strong>{profile?.activePositionCount ?? '—'}</strong></div>
        <div><span>Last Seen</span><strong>{profile?.lastSeen ? formatTime(profile.lastSeen) : '—'}</strong></div>
        <div><span>Tracking Start</span><strong>{profile?.trackingStartDate ? formatTime(profile.trackingStartDate) : '—'}</strong></div>
      </section>
      <HyperMetricCards cards={[
        { label: '30D realized PnL', value: dash(metrics.realized30dPnlUsd, formatUsd), tone: Number(metrics.realized30dPnlUsd || 0) >= 0 ? 'good' : 'bad' },
        { label: '30D realized %', value: dash(metrics.realized30dPnlPct, v => `${v.toFixed(2)}%`) },
        { label: 'Live realized PnL', value: dash(metrics.liveRealizedPnlUsd, formatUsd) },
        { label: 'Live realized %', value: dash(metrics.liveRealizedPnlPct, v => `${v.toFixed(2)}%`) },
        { label: 'Closed positions', value: metrics.totalClosedPositions ?? '—' },
        { label: 'Winning', value: metrics.winningPositions ?? '—', tone: 'good' },
        { label: 'Losing', value: metrics.losingPositions ?? '—', tone: 'bad' },
        { label: 'Winrate', value: dash(metrics.winrate, v => `${v.toFixed(1)}%`) },
        { label: 'Gross profit', value: dash(metrics.grossProfitUsd, formatUsd), tone: 'good' },
        { label: 'Gross loss', value: dash(metrics.grossLossUsd, formatUsd), tone: 'bad' },
        { label: 'Profit factor', value: dash(metrics.profitFactor) },
        { label: 'Avg hold', value: dash(metrics.averageHoldSeconds, formatDuration) },
        { label: 'Median hold', value: dash(metrics.medianHoldSeconds, formatDuration) },
        { label: 'Best trade', value: dash(metrics.bestTrade, formatUsd), tone: 'good' },
        { label: 'Worst trade', value: dash(metrics.worstTrade, formatUsd), tone: 'bad' },
        { label: 'Max drawdown', value: dash(metrics.maxDrawdown) },
        { label: 'OKX-copyable PnL', value: dash(metrics.okxCopyablePnl, formatUsd) },
        { label: 'Pure virtual PnL', value: dash(metrics.pureVirtualCopyPnl, formatUsd) },
        { label: 'Executable virtual PnL', value: dash(metrics.executableVirtualCopyPnl, formatUsd) },
        { label: 'Real OKX copied PnL', value: dash(metrics.realOkxCopiedPnl, formatUsd) },
        { label: 'Min order reject', value: dash(metrics.minOrderRejectRate) },
        { label: 'Skipped signals', value: metrics.skippedSignalCount ?? '—' },
      ]} />
      <nav className="hyper-sub-tabs">
        {['overview', 'active positions', 'closed positions', 'coin performance', 'allocation profile', 'copy simulation', 'okx orders', 'raw events'].map(item => (
          <button key={item} className={tab === item ? 'active' : ''} onClick={() => setTab(item)}>{item}</button>
        ))}
      </nav>
      {tab === 'overview' && <TraderOverview active={active} closed={closed} coins={coins} />}
      {tab === 'active positions' && <PositionTable rows={active} active />}
      {tab === 'closed positions' && <PositionTable rows={closed} />}
      {tab === 'coin performance' && <CoinPerformanceTable rows={coins} />}
      {tab === 'allocation profile' && <AllocationTable rows={alloc} />}
      {['copy simulation', 'okx orders', 'raw events'].includes(tab) && <section className="hyper-panel"><p className="empty-cell">No rows available from the current DB for this tab.</p></section>}
    </HyperShell>
  )
}

function TraderOverview({ active, coins }: { active: any[], closed: any[], coins: any[] }) {
  return (
    <section className="hyper-two-col">
      <div className="hyper-panel">
        <div className="hyper-panel-head"><strong>Largest active positions</strong></div>
        <PositionTable rows={active.slice(0, 10)} active compact />
      </div>
      <div className="hyper-panel">
        <div className="hyper-panel-head"><strong>Best coin profiles</strong></div>
        <CoinPerformanceTable rows={coins.slice(0, 10)} compact />
      </div>
    </section>
  )
}

function PositionTable({ rows, active, compact }: { rows: any[], active?: boolean, compact?: boolean }) {
  return (
    <HyperTable
      columns={[
        { key: 'coin', label: 'Coin' },
        { key: 'side', label: 'Side', render: r => <span className={String(r.side).toUpperCase() === 'LONG' ? 'pos' : 'neg'}>{r.side}</span> },
        { key: 'status', label: 'Status' },
        { key: 'openedAt', label: 'Opened', render: r => formatTime(r.openedAt) },
        ...(active ? [] : [{ key: 'closedAt', label: 'Closed', render: (r: any) => r.closedAt ? formatTime(r.closedAt) : '—' }]),
        { key: 'entryPrice', label: 'Entry', numeric: true, render: r => dash(r.entryPrice) },
        { key: 'exitPrice', label: 'Exit/Mark', numeric: true, render: r => dash(active ? r.currentMarkPrice : r.exitPrice) },
        { key: 'currentSize', label: 'Size', numeric: true, render: r => dash(r.currentSize || r.maxSize) },
        { key: 'currentNotionalUsd', label: 'Notional', numeric: true, render: r => dash(r.currentNotionalUsd || r.maxNotionalUsd, formatUsd) },
        { key: 'sourceAccountValueAtOpen', label: 'Account @ Open', numeric: true, render: r => dash(r.sourceAccountValueAtOpen, formatUsd) },
        { key: 'allocPctOfAccount', label: 'Alloc %', numeric: true, render: r => dash(r.allocPctOfAccount, v => `${v.toFixed(2)}%`) },
        ...(compact ? [] : [
          { key: 'marginMode', label: 'Margin' },
          { key: 'leverage', label: 'Lev', render: (r: any) => dash(r.leverage) },
        ]),
        { key: 'unrealizedPnlUsd', label: active ? 'uPnL' : 'Realized', numeric: true, render: r => <span className={Number((active ? r.unrealizedPnlUsd : r.realizedPnlUsd) || 0) >= 0 ? 'pos' : 'neg'}>{dash(active ? r.unrealizedPnlUsd : r.realizedPnlUsd, formatUsd)}</span> },
        { key: 'pnlPctAccount', label: 'PnL % Account', numeric: true, render: r => dash(r.pnlPctAccount, v => `${v.toFixed(3)}%`) },
        { key: 'fees', label: 'Fees', numeric: true, render: r => dash(r.fees, formatUsd) },
        { key: 'isOkxTradable', label: 'OKX', render: r => r.isOkxTradable ? 'yes' : 'no' },
        { key: 'copiedReal', label: 'Copied', render: r => r.copiedReal ? 'yes' : 'no' },
        { key: 'copyStatus', label: 'Copy status' },
        { key: 'skipReason', label: 'Skip reason' },
      ]}
      rows={rows}
      getKey={(row, index) => `${row.id || index}`}
    />
  )
}

function CoinPerformanceTable({ rows, compact }: { rows: any[], compact?: boolean }) {
  return (
    <HyperTable
      columns={[
        { key: 'coin', label: 'Coin' },
        { key: 'closedPositions', label: 'Closed', numeric: true },
        { key: 'winningPositions', label: 'Wins', numeric: true },
        { key: 'losingPositions', label: 'Losses', numeric: true },
        { key: 'winRate', label: 'Winrate', numeric: true, render: r => dash(r.winRate, v => `${v.toFixed(1)}%`) },
        { key: 'netPnlUsd', label: 'Net PnL', numeric: true, render: r => <span className={Number(r.netPnlUsd || 0) >= 0 ? 'pos' : 'neg'}>{dash(r.netPnlUsd, formatUsd)}</span> },
        ...(compact ? [] : [
          { key: 'grossProfitUsd', label: 'Gross Profit', numeric: true, render: (r: any) => dash(r.grossProfitUsd, formatUsd) },
          { key: 'grossLossUsd', label: 'Gross Loss', numeric: true, render: (r: any) => dash(r.grossLossUsd, formatUsd) },
        ]),
        { key: 'profitFactor', label: 'PF', numeric: true, render: r => dash(r.profitFactor) },
        { key: 'avgAllocPct', label: 'Avg Alloc', numeric: true, render: r => dash(r.avgAllocPct, v => `${v.toFixed(2)}%`) },
        { key: 'medianAllocPct', label: 'Median Alloc', numeric: true, render: r => dash(r.medianAllocPct, v => `${v.toFixed(2)}%`) },
        { key: 'p75AllocPct', label: 'P75', numeric: true, render: r => dash(r.p75AllocPct, v => `${v.toFixed(2)}%`) },
        { key: 'p90AllocPct', label: 'P90', numeric: true, render: r => dash(r.p90AllocPct, v => `${v.toFixed(2)}%`) },
        { key: 'maxAllocPct', label: 'Max Alloc', numeric: true, render: r => dash(r.maxAllocPct, v => `${v.toFixed(2)}%`) },
        { key: 'avgHoldSeconds', label: 'Avg Hold', numeric: true, render: r => dash(r.avgHoldSeconds, formatDuration) },
        { key: 'bestTradePnlUsd', label: 'Best', numeric: true, render: r => dash(r.bestTradePnlUsd, formatUsd) },
        { key: 'worstTradePnlUsd', label: 'Worst', numeric: true, render: r => dash(r.worstTradePnlUsd, formatUsd) },
        { key: 'coinSkillScore', label: 'Skill', numeric: true, render: r => dash(r.coinSkillScore) },
        { key: 'sampleConfidence', label: 'Sample Conf', numeric: true, render: r => dash(r.sampleConfidence) },
      ]}
      rows={rows}
      getKey={(row) => row.coin}
    />
  )
}

function AllocationTable({ rows }: { rows: any[] }) {
  return (
    <HyperTable
      columns={[
        { key: 'coin', label: 'Coin' },
        { key: 'minAllocPct', label: 'Min', numeric: true, render: r => dash(r.minAllocPct, v => `${v.toFixed(2)}%`) },
        { key: 'p25AllocPct', label: 'P25', numeric: true, render: r => dash(r.p25AllocPct, v => `${v.toFixed(2)}%`) },
        { key: 'medianAllocPct', label: 'Median', numeric: true, render: r => dash(r.medianAllocPct, v => `${v.toFixed(2)}%`) },
        { key: 'avgAllocPct', label: 'Average', numeric: true, render: r => dash(r.avgAllocPct, v => `${v.toFixed(2)}%`) },
        { key: 'p75AllocPct', label: 'P75', numeric: true, render: r => dash(r.p75AllocPct, v => `${v.toFixed(2)}%`) },
        { key: 'p90AllocPct', label: 'P90', numeric: true, render: r => dash(r.p90AllocPct, v => `${v.toFixed(2)}%`) },
        { key: 'maxAllocPct', label: 'Max', numeric: true, render: r => dash(r.maxAllocPct, v => `${v.toFixed(2)}%`) },
        { key: 'currentAllocPct', label: 'Current', numeric: true, render: r => dash(r.currentAllocPct, v => `${v.toFixed(2)}%`) },
        { key: 'currentVsMedian', label: 'Current / Median', numeric: true, render: r => dash(r.currentVsMedian, v => `${v.toFixed(2)}x`) },
        { key: 'currentVsP90', label: 'Current / P90', numeric: true, render: r => dash(r.currentVsP90, v => `${v.toFixed(2)}x`) },
        { key: 'allocationConviction', label: 'Conviction', numeric: true, render: r => dash(r.allocationConviction) },
      ]}
      rows={rows}
      getKey={(row) => row.coin}
    />
  )
}

function HyperConsensusPage({ path }: { path: string }) {
  const [data, setData] = useState<HyperliquidConsensusSnapshot | null>(null)
  const [selectedCoin, setSelectedCoin] = useState('BTC')
  const [contributors, setContributors] = useState<any[]>([])
  const load = useCallback(() => {
    fetchJson<HyperliquidConsensusSnapshot>('/api/hyperliquid/consensus').then((snapshot) => {
      setData(snapshot)
      if (!snapshot.coins.some(x => x.coin === selectedCoin) && snapshot.coins[0]) setSelectedCoin(snapshot.coins[0].coin)
    }).catch(() => setData(null))
  }, [selectedCoin])
  useEffect(() => {
    load()
    const timer = window.setInterval(load, 15000)
    return () => window.clearInterval(timer)
  }, [load])
  useEffect(() => {
    fetchJson<any[]>(`/api/hyperliquid/consensus/${selectedCoin}/contributors`).then(setContributors).catch(() => setContributors([]))
  }, [selectedCoin])
  const score = (coin: string) => data?.coins.find(x => x.coin === coin)?.directionScore
  return (
    <HyperShell current={path} title="Smart Consensus Engine" subtitle="Coin-level directional bias generated from tracked trader exposure and historical coin skill.">
      <HyperMetricCards cards={[
        { label: 'BTC Score', value: dash(score('BTC')) },
        { label: 'ETH Score', value: dash(score('ETH')) },
        { label: 'SOL Score', value: dash(score('SOL')) },
        { label: 'HYPE Score', value: dash(score('HYPE')) },
        { label: 'ALT Score', value: dash(data?.coins.filter(x => !['BTC','ETH','SOL','HYPE'].includes(x.coin)).reduce((a, b) => a + b.directionScore, 0)) },
        { label: 'Risk-On Score', value: dash(data?.coins.filter(x => x.targetSide === 'LONG').reduce((a, b) => a + Math.max(0, b.directionScore), 0)) },
        { label: 'BTC vs ALT', value: dash((score('BTC') || 0) - (score('ETH') || 0)) },
        { label: 'Regime', value: data?.coins[0]?.targetSide === 'LONG' ? 'Risk-on / long-led' : 'Defensive / mixed', tone: 'info' },
      ]} />
      <section className="hyper-panel">
        <HyperTable
          columns={[
            { key: 'coin', label: 'Coin' },
            { key: 'targetSide', label: 'Direction' },
            { key: 'directionScore', label: 'Direction Score', numeric: true, render: r => dash(r.directionScore) },
            { key: 'qualityScore', label: 'Quality', numeric: true, render: r => dash(r.qualityScore) },
            { key: 'longPower', label: 'Long Power', numeric: true, render: r => dash(r.longPower) },
            { key: 'shortPower', label: 'Short Power', numeric: true, render: r => dash(r.shortPower) },
            { key: 'conflictRatio', label: 'Conflict %', numeric: true, render: r => dash(r.conflictRatio, v => `${(v * 100).toFixed(0)}%`) },
            { key: 'participation', label: 'Participation %', numeric: true, render: r => dash(r.participation, v => `${(v * 100).toFixed(0)}%`) },
            { key: 'contributorCount', label: 'Active Traders', numeric: true },
            { key: 'targetNotionalUsd', label: 'Target OKX Notional', numeric: true, render: r => dash(r.targetNotionalUsd, formatUsd) },
            { key: 'currentOkxNotional', label: 'Current OKX Notional', render: () => '—' },
            { key: 'delta', label: 'Delta', render: () => '—' },
            { key: 'action', label: 'Action' },
            { key: 'skipReason', label: 'Skip reason' },
          ]}
          rows={data?.coins || []}
          getKey={(row) => row.coin}
          onRowClick={(row) => setSelectedCoin(row.coin)}
        />
      </section>
      <section className="hyper-panel">
        <div className="hyper-panel-head"><strong>Top Contributors: {selectedCoin}</strong></div>
        <HyperTable
          columns={[
            { key: 'traderAddress', label: 'Trader', render: r => <button onClick={() => routeTo(`/hyperliquid/traders/${r.traderAddress}`)}>{shortAddress(r.traderAddress)}</button> },
            { key: 'side', label: 'Side' },
            { key: 'currentNotionalUsd', label: 'Current Notional', numeric: true, render: r => dash(r.currentNotionalUsd, formatUsd) },
            { key: 'currentAccountValueUsd', label: 'Account Value', numeric: true, render: r => dash(r.currentAccountValueUsd, formatUsd) },
            { key: 'currentAllocPct', label: 'Alloc %', numeric: true, render: r => dash(r.currentAllocPct, v => `${v.toFixed(2)}%`) },
            { key: 'historicalMedianAllocPct', label: 'Historical Median', render: () => '—' },
            { key: 'historicalP90AllocPct', label: 'Historical P90', render: () => '—' },
            { key: 'allocationConviction', label: 'Conviction', numeric: true, render: r => dash(r.allocationConviction) },
            { key: 'coinSkillScore', label: 'Coin Skill', numeric: true, render: r => dash(r.coinSkillScore) },
            { key: 'weightedSignal', label: 'Weighted Signal', numeric: true, render: r => dash(r.weightedSignal) },
            { key: 'unrealizedPnlUsd', label: 'uPnL', numeric: true, render: r => dash(r.unrealizedPnlUsd, formatUsd) },
          ]}
          rows={contributors}
          getKey={(row, index) => `${row.traderAddress}-${row.coin}-${index}`}
        />
      </section>
    </HyperShell>
  )
}

function HyperPositionsPage({ path }: { path: string }) {
  const [active, setActive] = useState<any[]>([])
  const [closed, setClosed] = useState<any[]>([])
  useEffect(() => {
    fetchJson<any[]>('/api/hyperliquid/positions/active').then(setActive).catch(() => setActive([]))
    fetchJson<any[]>('/api/hyperliquid/positions/closed').then(setClosed).catch(() => setClosed([]))
  }, [])
  return (
    <HyperShell current={path} title="Position Monitor" subtitle="Full-screen monitor for source positions, baseline positions, closed positions and copy state.">
      <section className="hyper-panel"><div className="hyper-panel-head"><strong>Live active source positions</strong></div><PositionTable rows={active.filter(x => x.openedFromTracking)} active /></section>
      <section className="hyper-panel"><div className="hyper-panel-head"><strong>Baseline positions already open before tracking</strong></div><PositionTable rows={active.filter(x => !x.openedFromTracking)} active /></section>
      <section className="hyper-panel"><div className="hyper-panel-head"><strong>Recently closed source positions</strong></div><PositionTable rows={closed} /></section>
      <section className="hyper-panel"><div className="hyper-panel-head"><strong>Pure virtual copy positions</strong></div><p className="empty-cell">No virtual ledger rows available yet.</p></section>
      <section className="hyper-panel"><div className="hyper-panel-head"><strong>Executable virtual copy positions</strong></div><p className="empty-cell">No executable virtual ledger rows available yet.</p></section>
      <section className="hyper-panel"><div className="hyper-panel-head"><strong>Real OKX positions</strong></div><p className="empty-cell">Open the Execution page for live OKX positions.</p></section>
    </HyperShell>
  )
}

function HyperExecutionPage({ path }: { path: string }) {
  const [summary, setSummary] = useState<any>(null)
  const [orders, setOrders] = useState<any[]>([])
  const [positions, setPositions] = useState<any[]>([])
  const load = useCallback(() => {
    fetchJson<any>('/api/hyperliquid/execution/summary').then(setSummary).catch(() => setSummary(null))
    fetchJson<any[]>('/api/hyperliquid/execution/orders').then(setOrders).catch(() => setOrders([]))
    fetchJson<any[]>('/api/hyperliquid/execution/positions').then(setPositions).catch(() => setPositions([]))
  }, [])
  useEffect(() => { load() }, [load])
  const confirmAction = async (message: string, url: string) => {
    if (!window.confirm(message)) return
    await fetchJson(url, { method: 'POST' })
    load()
  }
  return (
    <HyperShell current={path} title="OKX Execution & Ledger" subtitle="Real execution, shadow-only state, target exposure and audit rows.">
      <HyperMetricCards cards={[
        { label: 'Current OKX equity', value: dash(summary?.okxEquity, formatUsd), tone: 'info' },
        { label: 'Real execution mode', value: summary?.realExecutionMode ?? '—', tone: summary?.realExecutionMode === 'Enabled' ? 'warn' : 'muted' },
        { label: 'Real execution traders', value: summary?.realExecutionTraders ?? '—' },
        { label: 'Open OKX positions', value: positions.length },
      ]} />
      <section className="hyper-danger-row">
        <button onClick={() => confirmAction('Disable real execution for every trader?', '/api/hyperliquid/execution/disable-real')}>Disable real execution</button>
        <button onClick={() => confirmAction('Switch all traders to shadow-only mode?', '/api/hyperliquid/execution/enable-shadow-only')}>Enable shadow-only</button>
        <button onClick={() => confirmAction('Pause all is not yet wired separately. Disable real execution instead?', '/api/hyperliquid/execution/disable-real')}>Pause all</button>
      </section>
      <section className="hyper-panel">
        <div className="hyper-panel-head"><strong>Target exposure per coin</strong></div>
        <HyperTable
          columns={[
            { key: 'coin', label: 'Coin' },
            { key: 'targetSide', label: 'Target Side' },
            { key: 'targetNotionalUsd', label: 'Target Notional', numeric: true, render: r => dash(r.targetNotionalUsd, formatUsd) },
            { key: 'action', label: 'Action' },
            { key: 'skipReason', label: 'Skip reason' },
          ]}
          rows={summary?.targetExposurePerCoin || []}
          getKey={(row) => row.coin}
        />
      </section>
      <section className="hyper-panel"><div className="hyper-panel-head"><strong>Open OKX positions</strong></div><pre>{JSON.stringify(positions, null, 2)}</pre></section>
      <section className="hyper-panel"><div className="hyper-panel-head"><strong>Recent copy / order events</strong></div><HyperTable columns={[
        { key: 'createdAt', label: 'Time', render: r => formatTime(r.createdAt) },
        { key: 'traderAddress', label: 'Trader', render: r => shortAddress(r.traderAddress) },
        { key: 'symbol', label: 'Coin' },
        { key: 'side', label: 'Side' },
        { key: 'eventType', label: 'Type' },
        { key: 'message', label: 'Message' },
        { key: 'isSuccess', label: 'Success', render: r => r.isSuccess ? 'yes' : 'no' },
      ]} rows={orders} getKey={(row, i) => `${row.id || i}`} /></section>
    </HyperShell>
  )
}

function App() {
  const [currentPath, setCurrentPath] = useState(window.location.pathname)
  const graphRef = useRef<any>(null)
  const aiCoreVisualRef = useRef<THREE.Group | null>(null)
  const panelResizeRef = useRef({ startX: 0, startWidth: 420 })
  const [wallets, setWallets] = useState<Wallet[]>([])
  const [events, setEvents] = useState<LiveEvent[]>([])
  const [aiState, setAiState] = useState<AiState>({ biasScore: 0, direction: 'NEUTRAL', summary: '', eventCount: 0 })
  const [operations, setOperations] = useState<OperationsSnapshot | null>(null)
  const [traderScans, setTraderScans] = useState<TraderScan[]>([])
  const [traderCandidates, setTraderCandidates] = useState<TraderCandidate[]>([])
  const [activeTraderScan, setActiveTraderScan] = useState<TraderScan | null>(null)
  const [discoveryRuns, setDiscoveryRuns] = useState<TraderDiscoveryRun[]>([])
  const [discoveryCandidates, setDiscoveryCandidates] = useState<TraderDiscoveryCandidate[]>([])
  const [activeDiscoveryRun, setActiveDiscoveryRun] = useState<TraderDiscoveryRun | null>(null)
  const [isDiscoveryRunning, setIsDiscoveryRunning] = useState(false)
  const [isTraderScanRunning, setIsTraderScanRunning] = useState(false)
  const [selected, setSelected] = useState<GraphNode | null>(null)
  const [activeTab, setActiveTab] = useState<Tab>('events')
  const [connectionState, setConnectionState] = useState('connecting')
  const [alert, setAlert] = useState('')
  const [chatQuestion, setChatQuestion] = useState('')
  const [chatLines, setChatLines] = useState<ChatLine[]>([])
  const [isChatThinking, setIsChatThinking] = useState(false)
  const [eventPulseRevision, setEventPulseRevision] = useState(0)
  const [panelWidth, setPanelWidth] = useState(() => {
    const stored = Number(window.localStorage.getItem('mission-control-panel-width'))
    return Number.isFinite(stored) && stored >= 340 ? stored : 420
  })

  useEffect(() => {
    const updatePath = () => setCurrentPath(window.location.pathname)
    window.addEventListener('popstate', updatePath)
    return () => window.removeEventListener('popstate', updatePath)
  }, [])
  const [isPanelResizing, setIsPanelResizing] = useState(false)
  const [traderForm, setTraderForm] = useState({
    startUtc: '',
    endUtc: '',
    minimumStartingValueUsd: 100000,
    top: 10,
    candidateWallets: '',
  })
  const [discoveryForm, setDiscoveryForm] = useState({
    lookbackDays: 28,
    minimumActiveWeeks: 3,
    minimumMeaningfulSwaps: 4,
    minimumSwapUsd: 1500,
    candidateLimit: 100,
  })

  const clampPanelWidth = useCallback((width: number) => {
    const maximum = Math.min(760, Math.max(420, window.innerWidth * 0.58))
    return Math.round(Math.min(maximum, Math.max(340, width)))
  }, [])

  const beginPanelResize = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (window.innerWidth <= 1120) return
    panelResizeRef.current = { startX: event.clientX, startWidth: panelWidth }
    setIsPanelResizing(true)
    event.currentTarget.setPointerCapture(event.pointerId)
    event.preventDefault()
  }

  const resizePanelWithKeyboard = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return
    event.preventDefault()
    setPanelWidth((current) => clampPanelWidth(current + (event.key === 'ArrowLeft' ? 24 : -24)))
  }

  useEffect(() => {
    if (!isPanelResizing) return

    const handlePointerMove = (event: PointerEvent) => {
      const nextWidth = panelResizeRef.current.startWidth + panelResizeRef.current.startX - event.clientX
      setPanelWidth(clampPanelWidth(nextWidth))
    }
    const stopResizing = () => setIsPanelResizing(false)

    window.addEventListener('pointermove', handlePointerMove)
    window.addEventListener('pointerup', stopResizing, { once: true })
    window.addEventListener('pointercancel', stopResizing, { once: true })
    return () => {
      window.removeEventListener('pointermove', handlePointerMove)
      window.removeEventListener('pointerup', stopResizing)
      window.removeEventListener('pointercancel', stopResizing)
    }
  }, [clampPanelWidth, isPanelResizing])

  useEffect(() => {
    window.localStorage.setItem('mission-control-panel-width', String(panelWidth))
    const frame = window.requestAnimationFrame(() => window.dispatchEvent(new Event('resize')))
    return () => window.cancelAnimationFrame(frame)
  }, [panelWidth])

  useEffect(() => {
    let animationFrame = 0
    const clock = new THREE.Clock()

    const animateAiCore = () => {
      const visual = aiCoreVisualRef.current
      if (visual) {
        const elapsed = clock.getElapsedTime()
        const core = visual.getObjectByName('ai-core-mesh')
        const wire = visual.getObjectByName('ai-core-wire')
        const orbitA = visual.getObjectByName('ai-orbit-a')
        const orbitB = visual.getObjectByName('ai-orbit-b')
        const orbitC = visual.getObjectByName('ai-orbit-c')

        if (core) {
          core.rotation.x = 0.2 + elapsed * 0.18
          core.rotation.y = 0.55 + elapsed * 0.34
          const pulse = 1 + Math.sin(elapsed * 1.8) * 0.055
          core.scale.setScalar(pulse)
        }
        if (wire && core) {
          wire.rotation.copy(core.rotation)
          wire.scale.copy(core.scale)
        }
        if (orbitA) orbitA.rotation.z = elapsed * 0.42
        if (orbitB) orbitB.rotation.x = elapsed * -0.31
        if (orbitC) orbitC.rotation.y = elapsed * 0.24
      }

      graphRef.current?.scene()?.traverse((object: THREE.Object3D) => {
        const createdAt = Number(object.userData?.flowCreatedAt || 0)
        const expiresAt = Number(object.userData?.flowExpiresAt || 0)
        if (!createdAt || !expiresAt) return

        const opacity = flowOpacity(createdAt, expiresAt) / 0.72
        object.visible = opacity > 0
        object.traverse((child: THREE.Object3D) => {
          const material = (child as THREE.Mesh).material as THREE.Material | THREE.Material[] | undefined
          const materials = Array.isArray(material) ? material : material ? [material] : []
          materials.forEach((item) => {
            item.transparent = true
            item.opacity = opacity
          })
        })
      })

      animationFrame = requestAnimationFrame(animateAiCore)
    }

    animateAiCore()
    return () => cancelAnimationFrame(animationFrame)
  }, [])

  useEffect(() => {
    const now = Date.now()
    const transitions = events.flatMap((event) => {
      const createdAt = new Date(event.createdAt).getTime()
      return [createdAt + 12_000, createdAt + FLOW_LIFETIME_MS]
    })
    const nextExpiry = transitions
      .filter((transitionAt) => transitionAt > now)
      .sort((a, b) => a - b)[0]

    if (!nextExpiry) return

    const timeout = window.setTimeout(
      () => setEventPulseRevision((revision) => revision + 1),
      Math.max(0, nextExpiry - now + 50),
    )
    return () => window.clearTimeout(timeout)
  }, [events, eventPulseRevision])

  const loadMissionState = useCallback(async () => {
    try {
      const [walletList, eventList, state, ops, traderScanList, discoveryRunList] = await Promise.all([
        fetchJson<Wallet[]>('/api/tracked-wallets?includeInactive=true'),
        fetchJson<LiveEvent[]>('/api/live-events?count=120'),
        fetchJson<AiState>('/api/ai-memory/state'),
        fetchJson<OperationsSnapshot>('/api/operations/snapshot'),
        fetchJson<TraderScan[]>('/api/trader-finder/scans?count=10'),
        fetchJson<TraderDiscoveryRun[]>('/api/trader-finder/discovery-runs?count=10'),
      ])
      setWallets((current) => JSON.stringify(current) === JSON.stringify(walletList) ? current : walletList)
      setEvents((current) => JSON.stringify(current) === JSON.stringify(eventList) ? current : eventList)
      setAiState(state)
      setOperations(ops)
      setTraderScans((current) => JSON.stringify(current) === JSON.stringify(traderScanList) ? current : traderScanList)
      setDiscoveryRuns((current) => JSON.stringify(current) === JSON.stringify(discoveryRunList) ? current : discoveryRunList)
      setAlert('')
    } catch (error) {
      setAlert(error instanceof Error ? error.message : 'Mission state unavailable')
    }
  }, [])

  useEffect(() => {
    loadMissionState()
    const timer = window.setInterval(loadMissionState, 30000)
    return () => window.clearInterval(timer)
  }, [loadMissionState])

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/mission-control')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('liveEvent', (event: LiveEvent) => {
      setEvents((current) => [event, ...current.filter((item) => item.id !== event.id)].slice(0, 160))
      setSelected({
        id: `event:${event.id}`,
        name: event.summary || event.type,
        kind: 'event',
        color: event.severity === 'danger' ? '#ef4444' : event.severity === 'success' ? '#22c55e' : '#facc15',
        size: 5,
        event,
      })
    })

    connection.on('traderDiscoveryProgress', (run: TraderDiscoveryRun) => {
      setActiveDiscoveryRun(run)
      setDiscoveryRuns((current) => [run, ...current.filter((item) => item.id !== run.id)].slice(0, 10))
    })

    connection.on('traderPerformanceProgress', (scan: TraderScan) => {
      setActiveTraderScan(scan)
      setTraderScans((current) => [scan, ...current.filter((item) => item.id !== scan.id)].slice(0, 10))
    })

    connection.onreconnecting(() => setConnectionState('reconnecting'))
    connection.onreconnected(() => setConnectionState('live'))
    connection.onclose(() => setConnectionState('offline'))

    connection
      .start()
      .then(() => setConnectionState(connection.state === HubConnectionState.Connected ? 'live' : connection.state))
      .catch((error) => {
        if (String(error).includes('401') || String(error).includes('Unauthorized')) {
          window.location.href = '/login.html'
          return
        }

        setConnectionState('offline')
      })

    return () => {
      connection.stop()
    }
  }, [])

  const graphData = useMemo(() => {
    const nodes = new Map<string, GraphNode>()
    const links: GraphLink[] = []
    const now = Date.now()

    nodes.set('ai', {
      id: 'ai',
      name: `AI Core ${aiState.direction || 'NEUTRAL'}`,
      kind: 'ai',
      color: aiState.direction === 'BULLISH' ? '#22c55e' : aiState.direction === 'BEARISH' ? '#ef4444' : '#67e8f9',
      size: 18,
    })

    nodes.set('okx', {
      id: 'okx',
      name: `OKX ${formatUsd(operations?.okx?.totalUsd)}`,
      kind: 'okx',
      color: '#f97316',
      size: 15,
    })

    wallets.forEach((wallet) => {
      const id = `wallet:${wallet.walletAddress}`
      nodes.set(id, {
        id,
        name: wallet.label || shortAddress(wallet.walletAddress),
        kind: 'wallet',
        color: wallet.isActive ? '#a78bfa' : '#64748b',
        size: 6 + Math.min(10, Number(wallet.confidenceScore || 0) / 10),
        wallet,
      })
    })

    const flows = new Map<string, LiveEvent[]>()
    events.forEach((event) => {
      if (!animatedEventTypes.has(event.type) && event.type !== 'AiAwakened') return
      const key = isManualExecutionProbe(event)
        ? `probe:${event.id}`
        : event.txHash
        ? `${event.walletAddress.toLowerCase()}:${event.txHash.toLowerCase()}`
        : `event:${event.id}`
      flows.set(key, [...(flows.get(key) || []), event])
    })

    flows.forEach((flowEvents) => {
      const ordered = flowEvents.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())
      const visibleSteps = ordered.filter((event) => event.type !== 'AiAwakened')
      const latestAt = Math.max(...ordered.map((event) => new Date(event.createdAt).getTime()))
      const flowExpiresAt = latestAt + FLOW_LIFETIME_MS
      if (flowExpiresAt <= now) return

      const flowCreatedAt = Math.min(...ordered.map((event) => new Date(event.createdAt).getTime()))
      const activity = visibleSteps.find((event) => event.type === 'WalletActivityDetected')
      const decision = [...visibleSteps].reverse().find((event) => event.type === 'AiDecisionCompleted')
      const execution = [...visibleSteps].reverse().find((event) =>
        event.type === 'TradeSubmitted' || (event.type === 'TradeRejected' && !isSkippedTrade(event)))
      const manualProbe = execution ? isManualExecutionProbe(execution) : false
      const skipped = [...visibleSteps].reverse().find((event) => isSkippedTrade(event))
      const walletAddress = ordered.find((event) => event.walletAddress)?.walletAddress || ''
      const walletId = `wallet:${walletAddress}`
      let previousId = nodes.has(walletId) ? walletId : ''

      const addStep = (event: LiveEvent, color: string) => {
        const eventId = `event:${event.id}`
        const isFresh = now - new Date(event.createdAt).getTime() <= 12_000
        nodes.set(eventId, {
          id: eventId,
          name: eventStepLabel(event),
          kind: 'event',
          color,
          size: 4 + Math.min(8, Number(event.usdValue || 0) / 25000),
          event,
          flowCreatedAt,
          flowExpiresAt,
        })
        if (previousId) {
          links.push({
            source: previousId,
            target: eventId,
            color,
            particles: isFresh ? 3 : 0,
            flowCreatedAt,
            flowExpiresAt,
          })
        }
        previousId = eventId
      }

      if (activity) addStep(activity, '#38bdf8')
      if (!manualProbe && previousId) {
        links.push({
          source: previousId,
          target: 'ai',
          color: '#67e8f9',
          particles: now - latestAt <= 12_000 ? 4 : 0,
          flowCreatedAt,
          flowExpiresAt,
        })
      }
      if (!manualProbe) {
        previousId = 'ai'
        if (decision) addStep(decision, '#facc15')
        if (skipped) addStep(skipped, '#94a3b8')
      }
      if (execution) {
        addStep(execution, execution.type === 'TradeSubmitted' ? '#22c55e' : '#ef4444')
        links.push({
          source: previousId,
          target: 'okx',
          color: execution.type === 'TradeSubmitted' ? '#22c55e' : '#ef4444',
          particles: now - new Date(execution.createdAt).getTime() <= 12_000 ? 5 : 0,
          flowCreatedAt,
          flowExpiresAt,
        })
      }
    })

    return { nodes: Array.from(nodes.values()), links }
  }, [aiState.direction, eventPulseRevision, events, operations?.okx?.totalUsd, wallets])

  const nodeThreeObject = useCallback((node: GraphNode) => {
    const group = new THREE.Group()

    if (node.kind === 'ai') {
      const coreMaterial = new THREE.MeshStandardMaterial({
        color: '#07111f',
        emissive: node.color,
        emissiveIntensity: 0.54,
        roughness: 0.16,
        metalness: 0.78,
      })
      const coreGeometry = new THREE.DodecahedronGeometry(4.9, 0)
      const core = new THREE.Mesh(coreGeometry, coreMaterial)
      core.name = 'ai-core-mesh'
      core.rotation.set(0.2, 0.55, -0.12)
      group.add(core)

      const wire = new THREE.Mesh(
        coreGeometry,
        new THREE.MeshBasicMaterial({
          color: node.color,
          wireframe: true,
          transparent: true,
          opacity: 0.68,
        }),
      )
      wire.name = 'ai-core-wire'
      wire.rotation.copy(core.rotation)
      group.add(wire)

      const ringMaterial = new THREE.MeshBasicMaterial({
        color: node.color,
        transparent: true,
        opacity: 0.62,
      })
      const ringA = new THREE.Mesh(new THREE.TorusGeometry(7.0, 0.06, 10, 96), ringMaterial)
      const ringB = new THREE.Mesh(new THREE.TorusGeometry(5.8, 0.04, 10, 96), ringMaterial)
      const ringC = new THREE.Mesh(new THREE.TorusGeometry(8.2, 0.025, 10, 96), new THREE.MeshBasicMaterial({
        color: '#e0f2fe',
        transparent: true,
        opacity: 0.34,
      }))
      ringA.rotation.x = Math.PI / 2.6
      ringB.rotation.y = Math.PI / 2.8
      ringC.rotation.set(Math.PI / 2.2, 0.45, 0.85)

      const orbitA = new THREE.Group()
      const orbitB = new THREE.Group()
      const orbitC = new THREE.Group()
      orbitA.name = 'ai-orbit-a'
      orbitB.name = 'ai-orbit-b'
      orbitC.name = 'ai-orbit-c'
      orbitA.add(ringA)
      orbitB.add(ringB)
      orbitC.add(ringC)
      group.add(orbitA, orbitB, orbitC)

      const satelliteGeometry = new THREE.OctahedronGeometry(0.58, 0)
      const satelliteMaterial = new THREE.MeshBasicMaterial({ color: '#ecfeff', transparent: true, opacity: 0.86 })
      const satellites = [
        { orbit: orbitA, position: [7.0, 0, 0] },
        { orbit: orbitA, position: [-7.0, 0, 0] },
        { orbit: orbitB, position: [0, 5.8, 0] },
        { orbit: orbitC, position: [8.2, 0, 0] },
      ] as const
      satellites.forEach(({ orbit, position }) => {
        const satellite = new THREE.Mesh(satelliteGeometry, satelliteMaterial)
        satellite.position.set(position[0], position[1], position[2])
        orbit.add(satellite)
      })

      const label = new SpriteText('AI CORE')
      label.color = '#a5f3fc'
      label.textHeight = 2.45
      label.position.y = 9.5
      label.material.depthTest = false
      label.renderOrder = 20
      group.add(label)
      aiCoreVisualRef.current = group
      return group
    }

    if (node.kind === 'okx') {
      group.add(makeOkxBillboard(operations?.okx?.totalUsd))
      return group
    }

    const geometry = new THREE.SphereGeometry(node.size / 4, 24, 24)
    const material = new THREE.MeshStandardMaterial({
      color: node.color,
      emissive: node.color,
      emissiveIntensity: 0.28,
      roughness: 0.38,
      metalness: node.kind === 'event' ? 0.45 : 0.12,
    })
    group.add(new THREE.Mesh(geometry, material))

    const label = new SpriteText(node.name)
    label.color = '#dbeafe'
    label.textHeight = 2.4
    label.position.y = node.size / 3 + 3
    group.add(label)
    if (node.flowCreatedAt && node.flowExpiresAt) {
      group.userData.flowCreatedAt = node.flowCreatedAt
      group.userData.flowExpiresAt = node.flowExpiresAt
    }
    return group
  }, [operations?.okx?.totalUsd])

  const handleNodeClick = useCallback((node: GraphNode) => {
    setSelected(node)
    if (graphRef.current) {
      const distance = 90
      const distRatio = 1 + distance / Math.hypot(node.x || 1, node.y || 1, node.z || 1)
      graphRef.current.cameraPosition(
        { x: (node.x || 0) * distRatio, y: (node.y || 0) * distRatio, z: (node.z || 0) * distRatio },
        node,
        900,
      )
    }
  }, [])

  const runTraderScan = async () => {
    if (!traderForm.startUtc || !traderForm.endUtc || isTraderScanRunning) return
    setIsTraderScanRunning(true)
    try {
      const candidateWallets = traderForm.candidateWallets
        .split(/[\s,;]+/)
        .map((value) => value.trim())
        .filter(Boolean)
      const scan = await fetchJson<TraderScan>('/api/trader-finder/scan', {
        method: 'POST',
        body: JSON.stringify({
          startUtc: new Date(traderForm.startUtc).toISOString(),
          endUtc: new Date(traderForm.endUtc).toISOString(),
          minimumStartingValueUsd: traderForm.minimumStartingValueUsd,
          top: traderForm.top,
          includeTrackedWallets: true,
          candidateWallets,
        }),
      })
      setActiveTraderScan(scan)
      setTraderCandidates([])
      await loadMissionState()
    } catch (error) {
      setAlert(error instanceof Error ? error.message : 'Trader scan failed')
      setIsTraderScanRunning(false)
    }
  }

  const runTraderDiscovery = async () => {
    if (isDiscoveryRunning) return
    setIsDiscoveryRunning(true)
    try {
      const run = await fetchJson<TraderDiscoveryRun>('/api/trader-finder/discover', {
        method: 'POST',
        body: JSON.stringify(discoveryForm),
      })
      setActiveDiscoveryRun(run)
      setDiscoveryCandidates([])
      await loadMissionState()
    } catch (error) {
      setAlert(error instanceof Error ? error.message : 'Dune discovery failed')
      setIsDiscoveryRunning(false)
    }
  }

  const retryTraderDiscovery = async () => {
    if (!activeDiscoveryRun || activeDiscoveryRun.state !== 'FAILED' || isDiscoveryRunning) return
    setIsDiscoveryRunning(true)
    try {
      const run = await fetchJson<TraderDiscoveryRun>(
        `/api/trader-finder/discovery-runs/${activeDiscoveryRun.id}/retry`,
        { method: 'POST' },
      )
      setActiveDiscoveryRun(run)
      setDiscoveryCandidates([])
      setAlert('')
      await loadMissionState()
    } catch (error) {
      setAlert(error instanceof Error ? error.message : 'Dune retry failed')
      setIsDiscoveryRunning(false)
    }
  }

  const loadDiscoveryCandidates = async (runId: number) => {
    const run = await fetchJson<TraderDiscoveryRun>(`/api/trader-finder/discovery-runs/${runId}`)
    const rows = await fetchJson<TraderDiscoveryCandidate[]>(
      `/api/trader-finder/discovery-runs/${runId}/candidates`,
    )
    setActiveDiscoveryRun(run)
    setDiscoveryCandidates(rows)
    setTraderForm((current) => ({
      ...current,
      candidateWallets: rows.map((candidate) => candidate.walletAddress).join('\n'),
    }))
  }

  useEffect(() => {
    if (!activeDiscoveryRun ||
        activeDiscoveryRun.state === 'COMPLETED' ||
        activeDiscoveryRun.state === 'FAILED') {
      setIsDiscoveryRunning(false)
      return
    }

    setIsDiscoveryRunning(true)
    const timer = window.setInterval(async () => {
      try {
        const run = await fetchJson<TraderDiscoveryRun>(
          `/api/trader-finder/discovery-runs/${activeDiscoveryRun.id}`,
        )
        setActiveDiscoveryRun(run)
        setDiscoveryRuns((current) => [run, ...current.filter((item) => item.id !== run.id)].slice(0, 10))
        if (run.state === 'COMPLETED') {
          await loadDiscoveryCandidates(run.id)
          setAlert(run.candidateCount === 0 ? 'Dune scan completed, but no wallet matched these filters.' : '')
        } else if (run.state === 'FAILED') {
          setAlert(run.errorMessage || 'Dune discovery failed')
        }
      } catch (error) {
        setAlert(error instanceof Error ? error.message : 'Discovery progress unavailable')
      }
    }, 1000)
    return () => window.clearInterval(timer)
  }, [activeDiscoveryRun?.id, activeDiscoveryRun?.state])

  const loadTraderCandidates = async (scanId: number) => {
    const scan = await fetchJson<TraderScan>(`/api/trader-finder/scans/${scanId}`)
    const rows = await fetchJson<TraderCandidate[]>(`/api/trader-finder/scans/${scanId}/candidates`)
    setActiveTraderScan(scan)
    setTraderCandidates(rows)
  }

  useEffect(() => {
    if (!activeTraderScan ||
        activeTraderScan.state === 'COMPLETED' ||
        activeTraderScan.state === 'FAILED') {
      setIsTraderScanRunning(false)
      return
    }

    setIsTraderScanRunning(true)
    const timer = window.setInterval(async () => {
      try {
        const scan = await fetchJson<TraderScan>(`/api/trader-finder/scans/${activeTraderScan.id}`)
        setActiveTraderScan(scan)
        setTraderScans((current) => [scan, ...current.filter((item) => item.id !== scan.id)].slice(0, 10))
        if (scan.state === 'COMPLETED') {
          await loadTraderCandidates(scan.id)
        } else if (scan.state === 'FAILED') {
          setAlert(scan.errorMessage || 'Performance verification failed')
        }
      } catch (error) {
        setAlert(error instanceof Error ? error.message : 'Performance progress unavailable')
      }
    }, 1000)
    return () => window.clearInterval(timer)
  }, [activeTraderScan?.id, activeTraderScan?.state])

  const trackTraderCandidate = async (candidate: TraderCandidate) => {
    await fetchJson(`/api/trader-finder/candidates/${candidate.id}/track`, { method: 'POST' })
    await loadMissionState()
  }

  const trackTopTraders = async (scanId: number) => {
    await fetchJson(`/api/trader-finder/scans/${scanId}/track-top?limit=10`, { method: 'POST' })
    await loadMissionState()
  }

  const sendChat = async () => {
    const question = chatQuestion.trim()
    if (!question || isChatThinking) return
    setChatQuestion('')
    setChatLines((lines) => [...lines, { role: 'user', text: question }])
    setIsChatThinking(true)
    try {
      const response = await fetchJson<{ answer: string; ai?: ChatAiMeta }>('/api/dashboard/chat', {
        method: 'POST',
        body: JSON.stringify({ question }),
      })
      setChatLines((lines) => [...lines, { role: 'ai', text: response.answer || 'No answer.', meta: response.ai }])
    } catch (error) {
      setChatLines((lines) => [...lines, { role: 'ai', text: error instanceof Error ? error.message : 'AI unavailable.' }])
    } finally {
      setIsChatThinking(false)
    }
  }

  const selectedPayload = parsePayload(selected?.event)

  if (currentPath.startsWith('/hyperliquid')) {
    return <HyperliquidResearchApp path={currentPath} />
  }

  return (
    <main
      className={`mission-shell${isPanelResizing ? ' panel-resizing' : ''}`}
      style={{ '--side-panel-width': `${panelWidth}px` } as CSSProperties}
    >
      <section className="universe-stage">
        <header className="topbar">
          <div>
            <p className="eyebrow">WhaleTracker Mission Control</p>
            <h1>Living AI Wallet Universe</h1>
          </div>
          <div className="status-cluster">
            <button className="status-pill hyper-link-button" onClick={() => routeTo('/hyperliquid/live-leaderboard')}>Hyperliquid</button>
            <span className={`status-pill ${connectionState}`}>{connectionState}</span>
            <span className="status-pill"><ShieldCheck size={15} /> auth</span>
            <button className="icon-button" onClick={loadMissionState} aria-label="Refresh mission state"><RefreshCw size={17} /></button>
          </div>
        </header>

        {alert && <div className="alert-line">{alert}</div>}

        <div className="graph-wrap">
          <ForceGraph3D
            ref={graphRef}
            graphData={graphData}
            backgroundColor="rgba(0,0,0,0)"
            nodeThreeObject={nodeThreeObject}
            nodeRelSize={4}
            linkColor={(link: GraphLink) => link.color}
            linkOpacity={(link: GraphLink) => flowOpacity(link.flowCreatedAt, link.flowExpiresAt)}
            linkWidth={1.2}
            linkDirectionalParticles={(link: GraphLink) => link.particles}
            linkDirectionalParticleSpeed={0.012}
            linkDirectionalParticleWidth={2.5}
            onNodeClick={handleNodeClick}
            cooldownTicks={140}
          />
        </div>

        <section className="bottom-dock">
          <div className="metric-block">
            <BrainCircuit size={18} />
            <div>
              <span>AI Bias</span>
              <strong>{aiState.direction || 'NEUTRAL'} {Number(aiState.biasScore || 0).toFixed(1)}</strong>
            </div>
          </div>
          <div className="metric-block">
            <CircleDollarSign size={18} />
            <div>
              <span>OKX Equity</span>
              <strong>{formatUsd(operations?.okx?.totalUsd)}</strong>
            </div>
          </div>
          <div className="metric-block">
            <Radar size={18} />
            <div>
              <span>Tracked Wallets</span>
              <strong>{wallets.filter((wallet) => wallet.isActive).length} active</strong>
            </div>
          </div>
          <div className="metric-block">
            <Zap size={18} />
            <div>
              <span>Live Events</span>
              <strong>{events.length}</strong>
            </div>
          </div>
        </section>
      </section>

      <aside className="side-panel">
        <div
          className="panel-resize-handle"
          role="separator"
          aria-label="Resize side panel"
          aria-orientation="vertical"
          aria-valuemin={340}
          aria-valuemax={760}
          aria-valuenow={panelWidth}
          tabIndex={0}
          onPointerDown={beginPanelResize}
          onKeyDown={resizePanelWithKeyboard}
        >
          <GripVertical size={16} />
        </div>
        <div className="ai-vitals">
          <Canvas camera={{ position: [0, 0, 5], fov: 45 }}>
            <AiCoreOrb bias={aiState.direction} />
          </Canvas>
        </div>

        <div className="panel-section">
          <div className="section-title"><Bot size={17} /> AI Core</div>
          <p className="summary">{aiState.summary || 'No AI memory summary recorded yet.'}</p>
        </div>

        <div className="panel-section selected-panel">
          <div className="section-title"><Activity size={17} /> Selection</div>
          {!selected && <p className="muted">Click a wallet, event, AI core, or OKX node.</p>}
          {selected?.wallet && (
            <div className="detail-grid">
              <span>Wallet</span><strong>{shortAddress(selected.wallet.walletAddress)}</strong>
              <span>Source</span><strong>{selected.wallet.source || '--'}</strong>
              <span>Confidence</span><strong>{Number(selected.wallet.confidenceScore || 0).toFixed(1)}</strong>
              <span>Profit</span><strong>{formatUsd(selected.wallet.estimatedProfitUsd)}</strong>
              <span>Last tx</span><strong>{shortAddress(selected.wallet.lastSeenTxHash)}</strong>
            </div>
          )}
          {selected?.event && (
            <div className="event-detail">
              <div className="event-kind">{selected.event.type}</div>
              <p>{selected.event.summary}</p>
              <div className="detail-grid">
                <span>Wallet</span><strong>{shortAddress(selected.event.walletAddress)}</strong>
                <span>Symbol</span><strong>{selected.event.symbol || '--'}</strong>
                <span>Value</span><strong>{formatUsd(selected.event.usdValue)}</strong>
                <span>Tx</span><strong>{shortAddress(selected.event.txHash)}</strong>
                <span>Time</span><strong>{formatTime(selected.event.createdAt)}</strong>
              </div>
              {selectedPayload && <pre>{JSON.stringify(selectedPayload, null, 2)}</pre>}
            </div>
          )}
          {selected?.kind === 'ai' && <p className="summary">{aiState.summary || 'AI is idle until a wallet event arrives.'}</p>}
          {selected?.kind === 'okx' && (
            <div className="detail-grid">
              <span>Mode</span><strong>{operations?.okx?.mode || '--'}</strong>
              <span>Available</span><strong>{operations?.okx?.available ? 'yes' : 'no'}</strong>
              <span>Equity</span><strong>{formatUsd(operations?.okx?.totalUsd)}</strong>
              <span>Positions</span><strong>{operations?.okx?.positions ?? 0}</strong>
            </div>
          )}
        </div>

        <nav className="tab-row">
          <button className={activeTab === 'events' ? 'active' : ''} onClick={() => setActiveTab('events')}>Events</button>
          <button className={activeTab === 'wallets' ? 'active' : ''} onClick={() => setActiveTab('wallets')}>Wallets</button>
          <button className={activeTab === 'insider' ? 'active' : ''} onClick={() => setActiveTab('insider')}>Trader Finder</button>
          <button onClick={() => routeTo('/hyperliquid/live-leaderboard')}>Hyperliquid</button>
          <button className={activeTab === 'chat' ? 'active' : ''} onClick={() => setActiveTab('chat')}>Chat</button>
        </nav>

        <div className="tab-panel">
          {activeTab === 'events' && (
            <div className="timeline">
              {events.length === 0 && <p className="muted">No live events recorded yet.</p>}
              {events.slice(0, 40).map((event) => (
                <button key={event.id} className="timeline-row" onClick={() => setSelected({ id: `event:${event.id}`, name: event.type, kind: 'event', color: '#facc15', size: 5, event })}>
                  <span>{event.type}</span>
                  <strong>{event.summary}</strong>
                  <small>{formatTime(event.createdAt)}</small>
                </button>
              ))}
            </div>
          )}

          {activeTab === 'wallets' && (
            <div className="wallet-list">
              {wallets.length === 0 && <p className="muted">No tracked wallets yet.</p>}
              {wallets.map((wallet) => (
                <button key={wallet.id} className="wallet-row" onClick={() => setSelected({ id: `wallet:${wallet.walletAddress}`, name: wallet.label || wallet.walletAddress, kind: 'wallet', color: '#a78bfa', size: 8, wallet })}>
                  <div>
                    <span>{wallet.label || shortAddress(wallet.walletAddress)}</span>
                    <strong>{Number(wallet.confidenceScore || 0).toFixed(1)}</strong>
                    <small>{wallet.source || '--'} · {wallet.isActive ? 'active' : 'paused'}</small>
                  </div>
                  <a
                    className="external-wallet-link"
                    href={zerionUrl(wallet.walletAddress)}
                    target="_blank"
                    rel="noreferrer"
                    title="Open in Zerion"
                    aria-label={`Open ${shortAddress(wallet.walletAddress)} in Zerion`}
                    onClick={(event) => event.stopPropagation()}
                  >
                    <ExternalLink size={15} />
                  </a>
                </button>
              ))}
            </div>
          )}

          {activeTab === 'insider' && (
            <div className="insider-lab">
              <div className="section-heading">
                <strong>Dune discovery</strong>
                <span>Find active, copyable-major traders across Ethereum and L2s.</span>
              </div>
              <div className="scan-grid">
                <label>Lookback days<input type="number" min="7" max="180" value={discoveryForm.lookbackDays} onChange={(e) => setDiscoveryForm({ ...discoveryForm, lookbackDays: Number(e.target.value) })} /></label>
                <label>Active weeks<input type="number" min="1" max="26" value={discoveryForm.minimumActiveWeeks} onChange={(e) => setDiscoveryForm({ ...discoveryForm, minimumActiveWeeks: Number(e.target.value) })} /></label>
                <label>Min swaps<input type="number" min="1" max="1000" value={discoveryForm.minimumMeaningfulSwaps} onChange={(e) => setDiscoveryForm({ ...discoveryForm, minimumMeaningfulSwaps: Number(e.target.value) })} /></label>
                <label>Min swap USD<input type="number" min="1" step="100" value={discoveryForm.minimumSwapUsd} onChange={(e) => setDiscoveryForm({ ...discoveryForm, minimumSwapUsd: Number(e.target.value) })} /></label>
                <label>Candidate limit<input type="number" min="1" max="500" value={discoveryForm.candidateLimit} onChange={(e) => setDiscoveryForm({ ...discoveryForm, candidateLimit: Number(e.target.value) })} /></label>
              </div>
              <button className="primary-action" disabled={isDiscoveryRunning} onClick={runTraderDiscovery}>
                <Database size={16} /> {isDiscoveryRunning ? 'Scanning Dune...' : 'Discover active traders'}
              </button>
              {activeDiscoveryRun && (
                <div className={`discovery-progress ${activeDiscoveryRun.state === 'FAILED' ? 'failed' : ''}`}>
                  <div className="progress-summary">
                    <strong>{activeDiscoveryRun.currentStage.replaceAll('_', ' ')}</strong>
                    <span>{activeDiscoveryRun.progressPercent}%</span>
                  </div>
                  <div
                    className="progress-track"
                    role="progressbar"
                    aria-valuemin={0}
                    aria-valuemax={100}
                    aria-valuenow={activeDiscoveryRun.progressPercent}
                  >
                    <span style={{ width: `${activeDiscoveryRun.progressPercent}%` }} />
                  </div>
                  <p>{activeDiscoveryRun.errorMessage || activeDiscoveryRun.statusMessage}</p>
                  {activeDiscoveryRun.executionId && <small>Dune execution: {activeDiscoveryRun.executionId}</small>}
                  {activeDiscoveryRun.state === 'FAILED' && (
                    <button className="retry-action" onClick={retryTraderDiscovery}>
                      <RefreshCw size={14} /> Retry discovery
                    </button>
                  )}
                  <div className="progress-log">
                    {[...(activeDiscoveryRun.progressLog || [])].reverse().map((entry, index) => (
                      <div key={`${entry.timestampUtc}-${index}`} className={entry.state === 'FAILED' ? 'error' : ''}>
                        <time>{formatTime(entry.timestampUtc)}</time>
                        <span>{entry.stage.replaceAll('_', ' ')}</span>
                        <p>{entry.message}</p>
                      </div>
                    ))}
                  </div>
                </div>
              )}
              <div className="scan-list">
                {discoveryRuns.map((run) => (
                  <button key={run.id} onClick={() => loadDiscoveryCandidates(run.id)}>
                    Discovery #{run.id} · {run.progressPercent}% · {run.state} · {run.candidateCount} candidates
                  </button>
                ))}
              </div>
              <div className="candidate-list">
                {discoveryCandidates.map((candidate) => (
                  <div className="candidate-row" key={candidate.id}>
                    <div>
                      <strong>{shortAddress(candidate.walletAddress)}</strong>
                      <span>
                        copy {Number(candidate.copyabilityScore || 0).toFixed(1)} · {candidate.meaningfulSwapCount} swaps · max/day {candidate.maximumDailySwaps}
                      </span>
                      <small>
                        current {formatUsd(candidate.currentCopyableValueUsd)} · avg {formatUsd(candidate.averageSwapUsd)} · {candidate.distinctMajorAssets} majors · {candidate.activeChains.join(', ')}
                      </small>
                    </div>
                    <a
                      className="external-wallet-link"
                      href={zerionUrl(candidate.walletAddress)}
                      target="_blank"
                      rel="noreferrer"
                      title="Open in Zerion"
                      aria-label={`Open ${shortAddress(candidate.walletAddress)} in Zerion`}
                    >
                      <ExternalLink size={15} />
                    </a>
                  </div>
                ))}
              </div>
              <div className="section-heading">
                <strong>Performance verification</strong>
                <span>Analyze discovered wallets before adding them to live tracking.</span>
              </div>
              <div className="scan-grid">
                <label>Start<input type="datetime-local" value={traderForm.startUtc} onChange={(e) => setTraderForm({ ...traderForm, startUtc: e.target.value })} /></label>
                <label>End<input type="datetime-local" value={traderForm.endUtc} onChange={(e) => setTraderForm({ ...traderForm, endUtc: e.target.value })} /></label>
                <label>Min portfolio USD<input type="number" min="0" step="10000" value={traderForm.minimumStartingValueUsd} onChange={(e) => setTraderForm({ ...traderForm, minimumStartingValueUsd: Number(e.target.value) })} /></label>
                <label>Top wallets<input type="number" min="1" max="100" value={traderForm.top} onChange={(e) => setTraderForm({ ...traderForm, top: Number(e.target.value) })} /></label>
              </div>
              <label className="wallet-seed-field">
                Candidate wallets
                <textarea
                  value={traderForm.candidateWallets}
                  onChange={(e) => setTraderForm({ ...traderForm, candidateWallets: e.target.value })}
                  placeholder="0x... addresses, one per line"
                />
              </label>
              <button className="primary-action" disabled={isTraderScanRunning} onClick={runTraderScan}>
                <Database size={16} /> {isTraderScanRunning ? 'Analyzing wallets...' : 'Find top traders'}
              </button>
              {activeTraderScan && (
                <div className={`discovery-progress ${activeTraderScan.state === 'FAILED' ? 'failed' : ''}`}>
                  <div className="progress-summary">
                    <strong>{activeTraderScan.currentStage.replaceAll('_', ' ')}</strong>
                    <span>{activeTraderScan.progressPercent}%</span>
                  </div>
                  <div
                    className="progress-track"
                    role="progressbar"
                    aria-valuemin={0}
                    aria-valuemax={100}
                    aria-valuenow={activeTraderScan.progressPercent}
                  >
                    <span style={{ width: `${activeTraderScan.progressPercent}%` }} />
                  </div>
                  <p>{activeTraderScan.errorMessage || activeTraderScan.statusMessage}</p>
                  <small>
                    Evaluated {activeTraderScan.evaluatedWalletCount} · qualified {activeTraderScan.qualifiedWalletCount}
                  </small>
                  <div className="progress-log">
                    {[...(activeTraderScan.progressLog || [])].reverse().map((entry, index) => (
                      <div key={`${entry.timestampUtc}-${index}`} className={entry.stage === 'wallet_failed' || entry.state === 'FAILED' ? 'error' : ''}>
                        <time>{formatTime(entry.timestampUtc)}</time>
                        <span>{entry.stage.replaceAll('_', ' ')}</span>
                        <p>{entry.message}</p>
                      </div>
                    ))}
                  </div>
                </div>
              )}
              <div className="scan-list">
                {traderScans.map((scan) => (
                  <button key={scan.id} onClick={() => loadTraderCandidates(scan.id)}>
                    Scan #{scan.id} · {scan.progressPercent}% · {scan.state} · {scan.qualifiedWalletCount}/{scan.evaluatedWalletCount}
                  </button>
                ))}
              </div>
              <div className="candidate-list">
                {traderCandidates.length > 0 && (
                  <button className="secondary-action" onClick={() => trackTopTraders(traderCandidates[0].traderScanId)}>
                    <Plus size={16} /> Track top 10
                  </button>
                )}
                {traderCandidates.map((candidate) => (
                  <div className="candidate-row" key={candidate.id}>
                    <div>
                      <strong>{shortAddress(candidate.walletAddress)}</strong>
                      <span>
                        score {Number(candidate.score || 0).toFixed(1)} · profit {formatUsd(candidate.adjustedProfitUsd)} · return {Number(candidate.adjustedReturnPercent || 0).toFixed(2)}%
                      </span>
                      <small>
                        {formatUsd(candidate.startingValueUsd)} → {formatUsd(candidate.endingValueUsd)} · external in {formatUsd(candidate.receivedExternalUsd)} · out {formatUsd(candidate.sentExternalUsd)}
                      </small>
                    </div>
                    <div className="candidate-actions">
                      <a
                        className="external-wallet-link"
                        href={zerionUrl(candidate.walletAddress)}
                        target="_blank"
                        rel="noreferrer"
                        title="Open in Zerion"
                        aria-label={`Open ${shortAddress(candidate.walletAddress)} in Zerion`}
                      >
                        <ExternalLink size={15} />
                      </a>
                      <button onClick={() => trackTraderCandidate(candidate)} aria-label="Track trader"><Plus size={16} /></button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {activeTab === 'chat' && (
            <div className="chat-panel">
              <div className="chat-lines">
                {chatLines.length === 0 && <p className="muted">Ask the AI about wallet bias, OKX exposure, or recent decisions.</p>}
                {chatLines.map((line, index) => (
                  <div key={`${line.role}-${index}`} className={`chat-line ${line.role}`}>
                    <div>{line.text}</div>
                    {line.meta && (
                      <div className="chat-meta">
                        {line.meta.provider === 'groq' ? 'Groq' : 'Local'} · {line.meta.model} · {line.meta.mode} · {line.meta.elapsedMs}ms
                        <br />
                        Source: {line.meta.source} · {shortAddress(line.meta.sourceWallet)} · {line.meta.positions} positions
                      </div>
                    )}
                  </div>
                ))}
                {isChatThinking && (
                  <div className="chat-line ai thinking">
                    Groq llama-3.3-70b-versatile dusunuyor...
                  </div>
                )}
              </div>
              <div className="chat-input">
                <input value={chatQuestion} disabled={isChatThinking} onChange={(e) => setChatQuestion(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && sendChat()} placeholder="Piyasa biası ve son hareketler ne söylüyor?" />
                <button onClick={sendChat} disabled={isChatThinking} aria-label="Send chat"><Send size={17} /></button>
              </div>
            </div>
          )}
        </div>
      </aside>
    </main>
  )
}

export default App
