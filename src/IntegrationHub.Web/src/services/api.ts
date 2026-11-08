import axios from 'axios'

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5121/api',
  timeout: 15000,
  headers: { 'Content-Type': 'application/json' },
})

api.interceptors.request.use((config) => {
  const token = sessionStorage.getItem('integrationhub.accessToken')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

api.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    if (axios.isAxiosError(error) && error.response?.status === 401 && !error.config?.url?.includes('/auth/login')) {
      window.dispatchEvent(new Event('integrationhub:session-expired'))
    }
    return Promise.reject(error)
  },
)

export interface Page<T> {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export type Role = 'Administrator' | 'Integration Analyst' | 'Support Analyst' | 'ReadOnly'
export interface User { id: string; email: string; fullName: string; roles: Role[] }
export interface LoginResult { accessToken: string; expiresAt: string; user: User }
export interface Client { id: string; code: string; name: string; legalName: string; email: string; phone: string | null; status: string; createdAt: string; integrationCount: number }
export interface Provider { id: string; code: string; name: string; description: string; type: string; status: string; timeoutSeconds: number; failureMode: string }
export interface Integration { id: string; code: string; name: string; description: string; clientId: string; clientName: string; providerId: string; providerName: string; type: string; status: string; environment: string; createdAt: string }
export interface ExecutionAttempt { attemptNumber: number; startedAt: string; completedAt: string | null; durationMs: number | null; succeeded: boolean; httpStatusCode: number | null; errorCode: string | null; errorMessage: string | null }
export interface Execution { id: string; integrationId: string; integrationCode: string; providerName: string; correlationId: string; idempotencyKey: string | null; requestPayload: string; responsePayload: string | null; status: string; attemptNumber: number; startedAt: string; completedAt: string | null; durationMs: number | null; httpStatusCode: number | null; errorCode: string | null; errorMessage: string | null; nextRetryAt: string | null; attempts: ExecutionAttempt[] }
export interface Incident { id: string; incidentNumber: string; integrationId: string; executionId: string | null; title: string; description: string; severity: string; status: string; errorCode: string | null; assignedToUserId: string | null; createdAt: string; updatedAt: string; resolvedAt: string | null; comments: { id: string; authorUserId: string; content: string; createdAt: string }[] }
export interface WebhookEvent { id: string; integrationId: string; integrationCode: string; externalEventId: string | null; correlationId: string; status: string; receivedAt: string; processedAt: string | null; errorMessage: string | null }
export interface Migration { id: string; migrationNumber: string; clientId: string; integrationId: string; sourceProvider: string; targetProvider: string; status: string; startDate: string | null; endDate: string | null; assignedToUserId: string | null; notes: string | null; createdAt: string; history: { status: string; changedByUserId: string | null; changedAt: string }[] }
export interface DashboardSummary { totalClients: number; totalIntegrations: number; activeIntegrations: number; totalExecutions: number; succeededExecutions: number; failedExecutions: number; successRatePercent: number | null; openIncidents: number; criticalIncidents: number; slaCompliancePercent: number | null; averageDurationMs: number | null; p95DurationMs: number | null }
export interface ExecutionSeries { windowStart: string; windowEnd: string; points: { date: string; succeeded: number; failed: number }[] }
export interface ProviderPerformance { providerId: string; providerName: string; executions: number; successRate: number; averageDurationMs: number | null }
export interface IntegrationLog { id: string; level: string; message: string; correlationId: string | null; integrationId: string | null; executionId: string | null; providerId: string | null; errorCode: string | null; occurredAt: string }

