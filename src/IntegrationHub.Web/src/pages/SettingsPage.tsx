import { useQuery } from '@tanstack/react-query'
import { CircleCheck, CircleHelp, PlugZap, ShieldAlert, SlidersHorizontal } from 'lucide-react'
import { api } from '../services/api'

type RuntimeStatus = {
  mode: 'Simulation' | 'Production'
  isProduction: boolean
  registeredProductionAdapters: string[]
  productionAdaptersMissing: boolean
  safetyMessage: string
}

export default function SettingsPage() {
  const runtime = useQuery({
    queryKey: ['runtime-mode'],
    queryFn: async () => (await api.get<RuntimeStatus>('/system/runtime')).data,
    retry: false,
  })
  const mode = runtime.data

  return <div className="page-content settings-page">
    <div className="page-heading">
      <div><div className="eyebrow">WORKSPACE CONFIGURATION <span className="eyebrow-line" /></div><h1>Runtime settings<span className="heading-period">.</span></h1><p>Review the active execution mode and provider adapter readiness.</p></div>
      <div className={`runtime-mode-chip ${mode?.isProduction ? 'production' : 'simulation'}`}><span />{mode?.mode ?? 'Checking mode'}</div>
    </div>

    {runtime.isError && <div className="error-banner"><ShieldAlert size={17} /> Runtime configuration could not be loaded. Check API access and try again.</div>}
    {mode?.productionAdaptersMissing && <div className="mode-warning"><ShieldAlert size={19}/><div><strong>Production mode is selected, but no real provider adapters are registered.</strong><span>External execution is blocked safely. Add and configure an adapter before routing production traffic.</span></div></div>}

    <div className="settings-grid">
      <section className="panel settings-mode-card">
        <div className="settings-icon"><SlidersHorizontal size={19}/></div>
        <span className="settings-kicker">ACTIVE EXECUTION MODE</span>
        <h2>{mode?.mode === 'Production' ? 'Production routing' : mode?.mode === 'Simulation' ? 'Simulation lab' : 'Loading runtime mode'}</h2>
        <p>{mode?.safetyMessage ?? 'Reading the API runtime configuration.'}</p>
        <div className="settings-mode-footer"><span>Controlled by backend configuration</span><code>IntegrationRuntime:Mode</code></div>
      </section>

      <section className="panel settings-adapters-card">
        <div className="settings-section-heading"><div className="settings-icon"><PlugZap size={19}/></div><div><h2>Production adapters</h2><p>Registered connectors available to the API</p></div></div>
        {mode?.registeredProductionAdapters.length ? <ul className="adapter-list">{mode.registeredProductionAdapters.map(name=><li key={name}><CircleCheck size={16}/><span>{name}</span><small>Registered</small></li>)}</ul> : <div className="adapter-empty"><CircleHelp size={18}/><span>No real provider adapters are registered yet.</span></div>}
        <p className="settings-note">Simulation stays available locally. Production requests never fall back to the mock simulator.</p>
      </section>
    </div>

    <section className="panel production-checklist">
      <div className="settings-section-heading"><div className="settings-icon"><ShieldAlert size={19}/></div><div><h2>Before enabling a real provider</h2><p>Complete these items for each external system.</p></div></div>
      <div className="checklist-grid">
        <div><b>01</b><span>Implement <code>IProductionIntegrationProvider</code> for the provider protocol.</span></div>
        <div><b>02</b><span>Register its adapter and configure an allowlisted HTTPS endpoint.</span></div>
        <div><b>03</b><span>Store API credentials in a secret manager; never in the database or frontend.</span></div>
        <div><b>04</b><span>Set timeout, signature verification, idempotency and retry rules with the provider.</span></div>
        <div><b>05</b><span>Verify sandbox behavior, alerts, audit trail, rollback and operational ownership.</span></div>
        <div><b>06</b><span>Switch the server to Production using deployment configuration and validate health.</span></div>
      </div>
      <p className="settings-doc-link">Deployment checklist: <code>docs/deployment.md</code></p>
    </section>
  </div>
}
