import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { ClerkProvider } from '@clerk/clerk-react'
import './index.css'
import App from './App.tsx'
import { AuthProvider } from './auth'

// Phase 0: the Clerk publishable key is injected by the AppHost as
// VITE_CLERK_PUBLISHABLE_KEY. With no Clerk tenant yet it's empty, so the
// shell renders without ClerkProvider and shows a "not configured" notice.
const clerkKey = import.meta.env.VITE_CLERK_PUBLISHABLE_KEY as string | undefined

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      {clerkKey ? (
        <ClerkProvider publishableKey={clerkKey}>
          <AuthProvider clerkConfigured>
            <App clerkConfigured />
          </AuthProvider>
        </ClerkProvider>
      ) : (
        <AuthProvider clerkConfigured={false}>
          <App clerkConfigured={false} />
        </AuthProvider>
      )}
    </BrowserRouter>
  </StrictMode>,
)
