import { createContext, useContext, type ReactNode } from 'react'
import { useAuth, useClerk } from '@clerk/clerk-react'

// One auth abstraction so components never call Clerk hooks directly (those are
// only valid under ClerkProvider, which only exists when a publishable key is
// set). Three sources, decided once at the root:
//  - Clerk configured  → real session token + contextual sign-in modal.
//  - E2E (window flag)  → a fake token; the BFF is fully stubbed by Playwright
//                         so the value is irrelevant — lets the test drive the
//                         authed hook deterministically without a Clerk tenant.
//  - neither            → anonymous; refine UI is greyed.
export interface AuthState {
  authed: boolean
  getToken: () => Promise<string | null>
  signIn: () => void
}

declare global {
  interface Window { __E2E_TOKEN__?: string }
}

const Ctx = createContext<AuthState>({
  authed: false,
  getToken: async () => null,
  signIn: () => {},
})

export const useAuthState = () => useContext(Ctx)

// Mounted only inside ClerkProvider.
function ClerkBridge({ children }: { children: ReactNode }) {
  const { isSignedIn, getToken } = useAuth()
  const clerk = useClerk()
  const value: AuthState = {
    authed: !!isSignedIn,
    getToken: () => getToken(),
    signIn: () => clerk.openSignIn(),
  }
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>
}

export function AuthProvider({
  clerkConfigured, children,
}: { clerkConfigured: boolean; children: ReactNode }) {
  if (clerkConfigured) return <ClerkBridge>{children}</ClerkBridge>

  const e2e = typeof window !== 'undefined' ? window.__E2E_TOKEN__ : undefined
  const value: AuthState = e2e
    ? { authed: true, getToken: async () => e2e, signIn: () => {} }
    : { authed: false, getToken: async () => null,
        signIn: () => alert('Sign-in arrives once the Clerk tenant is configured.') }
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>
}
