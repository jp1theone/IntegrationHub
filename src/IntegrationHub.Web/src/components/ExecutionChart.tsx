import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'

interface Point { date: string; succeeded: number; failed: number }

export default function ExecutionChart({ points, loading }: { points: Point[]; loading: boolean }) {
  if (loading) return <div className="chart-loading">Loading activity…</div>
  if (!points.length) return <div className="chart-loading">No execution activity in this window.</div>
  return <ResponsiveContainer width="100%" height="100%"><AreaChart data={points} margin={{ top: 8, right: 8, left: -24, bottom: 0 }}>
    <defs><linearGradient id="successFill" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="#20b486" stopOpacity={.18}/><stop offset="100%" stopColor="#20b486" stopOpacity={0}/></linearGradient><linearGradient id="failedFill" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="#ef6c75" stopOpacity={.12}/><stop offset="100%" stopColor="#ef6c75" stopOpacity={0}/></linearGradient></defs>
    <CartesianGrid vertical={false} stroke="#edf0f4"/><XAxis dataKey="date" tickLine={false} axisLine={false} tick={{ fill: '#8b94a5', fontSize: 11 }} tickFormatter={value => new Date(value).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}/><YAxis tickLine={false} axisLine={false} tick={{ fill: '#8b94a5', fontSize: 11 }}/><Tooltip contentStyle={{ border: '1px solid #e8ebf0', borderRadius: 10, boxShadow: '0 8px 24px #17233b12' }}/><Area type="monotone" dataKey="succeeded" stroke="#20b486" strokeWidth={2.5} fill="url(#successFill)"/><Area type="monotone" dataKey="failed" stroke="#ef6c75" strokeWidth={2} fill="url(#failedFill)"/>
  </AreaChart></ResponsiveContainer>
}
