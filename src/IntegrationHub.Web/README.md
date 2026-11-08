# IntegrationHub Web

React + TypeScript single-page client for the IntegrationHub API. It uses Vite, Tailwind CSS, React Router, Axios, TanStack Query, React Hook Form, Zod, Recharts, and Lucide React.

From this directory:

```powershell
npm ci
npm run dev
npm run build
```

The development server runs at `http://localhost:5173`. API requests default to `http://localhost:5121/api`; set `VITE_API_BASE_URL` to override it. This value is public browser configuration and must never contain credentials.
