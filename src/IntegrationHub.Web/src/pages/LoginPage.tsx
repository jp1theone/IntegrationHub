import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { Activity, ArrowRight, Eye, EyeOff, Fingerprint, ShieldCheck, Workflow } from 'lucide-react'
import { useAuth } from '../auth/useAuth'

const loginSchema = z.object({ email: z.email('Enter a valid work email.'), password: z.string().min(1, 'Enter your password.') })
type LoginForm = z.infer<typeof loginSchema>

export default function LoginPage() {
  const { login } = useAuth()
  const [visible,setVisible]=useState(false)
  const [error,setError]=useState('')
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<LoginForm>({ resolver: zodResolver(loginSchema) })
  async function submit(values:LoginForm){setError('');try{await login(values.email,values.password)}catch{setError('We couldn’t sign you in. Check your credentials and API connection.')}}
  return <main className="login-screen"><section className="login-art"><div className="login-brand"><div className="brand-mark">I</div><div><strong>IntegrationHub</strong><small>CONTROL CENTER</small></div></div><div className="art-orbit orbit-one"/><div className="art-orbit orbit-two"/><div className="art-content"><div className="art-badge"><i/> THE INTEGRATION OPERATIONS PLATFORM</div><h1>Everything connected.<br/><span>Every detail in control.</span></h1><p>One secure workspace to monitor integrations, investigate incidents, and keep every transaction moving.</p><div className="art-stats"><div><Workflow size={18}/><strong>Connected</strong><span>Integration network</span></div><div><Activity size={18}/><strong>Real-time</strong><span>Operational visibility</span></div><div><ShieldCheck size={18}/><strong>Protected</strong><span>Access by role</span></div></div></div><div className="login-art-foot"><span>BUILT FOR RELIABLE OPERATIONS</span><span>v1.0</span></div></section><section className="login-form-side"><div className="login-form-wrap"><div className="login-mini-icon"><Fingerprint size={20}/></div><div className="eyebrow">WELCOME BACK</div><h2>Sign in to your workspace</h2><p>Enter your account details to continue.</p><form onSubmit={handleSubmit(submit)} noValidate><label htmlFor="email">Work email</label><input id="email" type="email" autoComplete="username" placeholder="you@company.com" aria-invalid={Boolean(errors.email)} {...register('email')}/>{errors.email&&<span className="field-error">{errors.email.message}</span>}<label htmlFor="password">Password</label><div className="password-wrap"><input id="password" type={visible?'text':'password'} autoComplete="current-password" placeholder="Enter your password" aria-invalid={Boolean(errors.password)} {...register('password')}/><button type="button" onClick={()=>setVisible(!visible)} aria-label={visible?'Hide password':'Show password'}>{visible?<EyeOff size={17}/>:<Eye size={17}/>}</button></div>{errors.password&&<span className="field-error">{errors.password.message}</span>}{error&&<div className="login-error">{error}</div>}<button className="button button-primary login-submit" disabled={isSubmitting}>{isSubmitting?'Signing in…':'Sign in'}<ArrowRight size={17}/></button></form><div className="login-security"><ShieldCheck size={16}/><span>Your session is protected with secure authentication.</span></div><div className="login-help">Need access? Contact your workspace administrator.</div></div><footer>© 2026 IntegrationHub <span>·</span> Secure operations workspace</footer></section></main>
}
