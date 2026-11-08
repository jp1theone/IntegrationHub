import { useEffect, useMemo, useState } from 'react'
import type { PropsWithChildren } from 'react'
import { api, type LoginResult, type User } from '../services/api'
import { AuthContext } from './authStore'

export function AuthProvider({ children }: PropsWithChildren) {
  const [token, setToken] = useState(() => sessionStorage.getItem('integrationhub.accessToken'))
  const [user, setUser] = useState<User | null>(() => {
    const saved = sessionStorage.getItem('integrationhub.user')
    return saved ? (JSON.parse(saved) as User) : null
  })
  const [loading, setLoading] = useState(Boolean(token))

  useEffect(() => {
    if (!token) { setLoading(false); return }
    let active = true
    api.get<User>('/auth/me').then(({ data }) => {
      if (!active) return
      setUser(data)
      sessionStorage.setItem('integrationhub.user', JSON.stringify(data))
    }).catch(() => clearSession()).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
    // Restore only when the initial token is loaded.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    const expire = () => clearSession()
    window.addEventListener('integrationhub:session-expired', expire)
    return () => window.removeEventListener('integrationhub:session-expired', expire)
  }, [])

  function clearSession() {
    sessionStorage.removeItem('integrationhub.accessToken')
    sessionStorage.removeItem('integrationhub.user')
    setToken(null)
    setUser(null)
    setLoading(false)
  }

  async function login(email: string, password: string) {
    const { data } = await api.post<LoginResult>('/auth/login', { email, password })
    sessionStorage.setItem('integrationhub.accessToken', data.accessToken)
    sessionStorage.setItem('integrationhub.user', JSON.stringify(data.user))
    setToken(data.accessToken)
    setUser(data.user)
    setLoading(false)
  }

  async function logout() {
    try { await api.post('/auth/logout') } finally { clearSession() }
  }

  const value = useMemo(() => ({ user, token, loading, login, logout }), [user, token, loading])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

