# ArbiScannerWebApp Client — Architecture Review

## Summary

The client is a React + TypeScript SPA built with Vite, Redux Toolkit, RTK Query, Axios, and SignalR. The overall structure is reasonable, but there are several recurring problems: unsafe typing (`any`), duplicated auth-guard patterns, sensitive data leaked to the client type system, a security concern around token storage, and state management inconsistencies. The SignalR integration has a race condition that can manifest in production.

---

## Issues

### 1. `dispatch` Typed as `any` Everywhere — Loses All Type Safety (High)

**Files:** `App.tsx`, `AccountPage.tsx`, `SpreadsPage.tsx`, `LoginPage.tsx`, and others

```ts
const dispatch: any = useDispatch();        // App.tsx
const dispatcher: any = useDispatch();     // SpreadsPage.tsx, LoginPage.tsx
const dispatch: any = useDispatch();       // AccountPage.tsx
```

Casting `dispatch` to `any` removes all compile-time checking on dispatched actions. Thunks can be called with wrong arguments silently.

**Fix:** Export a typed `AppDispatch` from the store and use the typed `useAppDispatch` hook:

```ts
// store.ts
export type AppDispatch = typeof store.dispatch;

// hooks.ts
import { useDispatch } from 'react-redux';
import type { AppDispatch } from './store/store';
export const useAppDispatch = () => useDispatch<AppDispatch>();
```

---

### 2. JWT Tokens Persisted to `localStorage` via `redux-persist` (Security — High)

**File:** `src/store/store.ts`

```ts
const persistConfig = {
    key: "root",
    storage,              // redux-persist/es/storage — localStorage
    whitelist: ["account"],
};
```

The entire `account` slice — including `accessToken` and `refreshToken` — is persisted to `localStorage`. Tokens stored in `localStorage` are accessible to any JavaScript on the page, making them vulnerable to XSS attacks.

**Fix:** Either:
- Store tokens in `httpOnly` cookies (server-side) and remove them from Redux state entirely, or
- Use `sessionStorage` for short-lived access tokens and only persist the minimum non-sensitive fields (e.g., `isLoggedIn`, display name). Exclude token fields explicitly:

```ts
const persistConfig = {
    key: "root",
    storage,
    whitelist: ["account"],
    blacklist: [],        // use a custom transform instead
};
// Use createTransform to strip token fields before persisting
```

---

### 3. `AccountModel` Exposes Server-Side IdentityUser Internals (Security — High)

**File:** `src/types/accountType.ts`

```ts
export interface AccountModel {
    passwordHash?: string | null;
    securityStamp?: string | null;
    concurrencyStamp?: string | null;
    // ...all other IdentityUser fields
}
```

Fields like `passwordHash`, `securityStamp`, and `concurrencyStamp` are internal ASP.NET Identity fields that must never leave the server. If the API returns them and the client stores them in Redux (and then in `localStorage`), this is both a data exposure and a type-hygiene failure.

**Fix:** Create a minimal client-facing DTO that only includes fields the UI actually needs. Never mirror the full server entity on the client.

---

### 4. Auth Guard Logic Copy-Pasted into Every Protected Page (High)

**Files:** `SpreadPage.tsx`, `SpreadsPage.tsx`, `AccountPage.tsx`

```ts
// Repeated verbatim in three files
useEffect(() => {
    if (!isLoggedIn) {
        navigate('/account/login');
    }
}, [isLoggedIn, navigate]);

useEffect(() => {
    if (!isActive) {
        navigate('/subscriptions');
    }
}, [isActive, navigate]);
```

Duplicating auth-guard logic in every page creates maintenance risk (inconsistent conditions, forgetting to add the check in new pages) and makes the routing intent invisible at the route definition level.

**Fix:** Create a `<ProtectedRoute>` component and wrap protected routes in `App.tsx`:

```tsx
// components/ProtectedRoute.tsx
function ProtectedRoute({ children }: { children: ReactNode }) {
    const isLoggedIn = useSelector(...);
    const isActive = useSelector(...);
    if (!isLoggedIn) return <Navigate to="/account/login" replace />;
    if (!isActive) return <Navigate to="/subscriptions" replace />;
    return <>{children}</>;
}

// App.tsx
<Route path="spreads" element={<ProtectedRoute><SpreadsPage /></ProtectedRoute>} />
```

---

### 5. `PageWrapper` Component Defined Inside `App` on Every Render (Medium)

**File:** `src/App.tsx`

```ts
function App() {
    // ...
    const PageWrapper: React.FC<{ children: React.ReactNode }> = ({ children }) => (
        <motion.div ...>
            {children}
        </motion.div>
    );
```

A component defined inside another component is recreated on every parent render. React will see it as a new component type each time, causing full unmount/remount of all children on every `App` re-render.

**Fix:** Move `PageWrapper` outside `App` to the module level.

---

### 6. SignalR `useEffect` Cleanup Has a Race Condition (Medium)

**File:** `src/pages/Spread/SpreadPage.tsx`

```ts
useEffect(() => {
    if (!groupName) return;

    const start = async () => {
        await signalRService.connect('/hubs/PossiblePositions');
        await signalRService.joinGroup(groupName);
        signalRService.onTickerUpdate((message) => { ... });
    };

    start(); // fire-and-forget — not awaited

    return () => {
        signalRService.leaveGroup(groupName);  // called before connect() may have finished
        signalRService.offTickerUpdate();
        signalRService.disconnect();
    };
}, [groupName]);
```

If the component unmounts while `start()` is still awaiting `connect()` or `joinGroup()`, the cleanup runs before the connection is established. The result: `leaveGroup` is called on a connection that doesn't exist yet, then `joinGroup` completes on a service that has already been asked to disconnect.

**Fix:** Track the in-progress async call with an `isCancelled` flag and skip post-unmount state updates:

```ts
useEffect(() => {
    if (!groupName) return;
    let cancelled = false;

    const start = async () => {
        await signalRService.connect('/hubs/PossiblePositions');
        if (cancelled) return;
        await signalRService.joinGroup(groupName);
        if (cancelled) return;
        signalRService.onTickerUpdate(handleMessage);
    };
    start();

    return () => {
        cancelled = true;
        signalRService.leaveGroup(groupName).catch(() => {});
        signalRService.offTickerUpdate();
        signalRService.disconnect();
    };
}, [groupName]);
```

---

### 7. Inconsistent Data Fetching — Direct API Calls Bypass Redux (Medium)

**Files:** `SpreadPage.tsx` vs `SpreadsPage.tsx`

`SpreadsPage` dispatches a Redux thunk (`getSpreadsAsync`) and reads state from the store. `SpreadPage` calls the API directly inside `useEffect` and manages its own local state (`useState`), completely bypassing Redux.

There is no consistent pattern: some pages use RTK Query (`subscription.ts`), some use Redux thunks, and some call APIs directly. This makes state caching, loading state management, and error handling ad-hoc across pages.

**Fix:** Decide on one data-fetching strategy for each data domain and apply it consistently. RTK Query is already partially set up and is the best fit: it handles caching, loading/error states, and invalidation out of the box.

---

### 8. `rows` Local State Duplicates and Shadows Redux Store State (Medium)

**File:** `src/pages/Spread/SpreadsPage.tsx`

```ts
const possiblePositions = useSelector(...);
const [rows, setRows] = useState<any>([]);

useEffect(() => {
    const mappedrows = possiblePositions?.map(...);
    setRows(mappedrows);
}, [possiblePositions]);
```

`possiblePositions` comes from Redux. It is transformed and put into a separate `rows` local state. This creates two sources of truth that need to be kept in sync and adds unnecessary re-renders (store update → effect → setState → re-render).

**Fix:** Derive `rows` directly with `useMemo`:

```ts
const rows = useMemo(
    () => possiblePositions?.map((item, index) => ({ id: index, ... })) ?? [],
    [possiblePositions]
);
```

---

### 9. Array Index Used as DataGrid Row `id` (Medium)

**File:** `src/pages/Spread/SpreadsPage.tsx`

```ts
const mappedrows = possiblePositions?.map((item, index) => ({
    id: index,   // array index as ID
    guid: item.guid,
    ...
}));
```

Using array index as a row identifier causes stale identity issues when rows are sorted, filtered, or updated — MUI DataGrid will misidentify rows.

**Fix:** Use the item's actual unique identifier: `id: item.guid`.

---

### 10. `typeLabels` Map Recreated on Every Render (Low)

**File:** `src/pages/Spread/SpreadsPage.tsx`

```ts
function SpreadsPage() {
    const typeLabels: Record<number, string> = {
        2: "Spot",
        0: "Futures",
        1: "Funding"
    };
```

This constant object is recreated on every component render.

**Fix:** Move it to module scope, or use the existing `SpreadType` enum already in `src/types/SpreadType.ts`.

---

### 11. Non-Null Assertion on URL Search Param Without Fallback (Medium)

**File:** `src/pages/Spread/SpreadPage.tsx`

```ts
const id: string = searchParams.get("id")!;
const apiResult = await PossiblePositionsAPI.getSpreadInfo(id);
```

If `/spread` is visited without `?id=...`, `searchParams.get("id")` returns `null`. The `!` assertion suppresses the TypeScript error, but at runtime `null` is passed to `getSpreadInfo`, producing a broken API call or a runtime error.

**Fix:** Add an explicit guard:

```ts
const id = searchParams.get("id");
if (!id) { navigate('/spreads'); return; }
```

---

### 12. `console.log` Debug Statements Left in Production Code (Low)

**Files:** `SpreadPage.tsx`, `SpreadsPage.tsx`

```ts
console.log('Live ticker update received:', message);    // SpreadPage.tsx
console.log('Double clicked row:', fullData);            // SpreadsPage.tsx
console.log('Clicked row (mobile):', fullData);          // SpreadsPage.tsx
console.log(`Joined group: ${groupName}`);               // signalrService.ts
console.log('SignalR Connected');                        // signalrService.ts
```

These expose internal data to browser devtools in production and produce noise in logs. A `logger` service already exists in the project.

**Fix:** Remove or replace with `logger.debug(...)` calls. Add an ESLint `no-console` rule to catch future occurrences.

---

### 13. `IRootStore` Type Missing RTK Query Slice (Low)

**File:** `src/store/store.ts`

```ts
export interface IRootStore {
    account: AccountState,
    spreads: SpreadState
    // subscriptionsAPI is missing
}
```

The `subscriptionsAPI` reducer is registered in the store but absent from `IRootStore`. Any code that tries to access `state.subscriptionsAPI` with this type will produce a TypeScript error or require a cast.

**Fix:** Use `ReturnType<typeof store.getState>` as the root state type instead of maintaining a manual interface:

```ts
export type IRootStore = ReturnType<typeof store.getState>;
```

---

### 14. Inconsistent Naming: `navigator` vs `navigate` for `useNavigate` (Low)

**Files:** `SpreadsPage.tsx`, `LoginPage.tsx`, `AccountPage.tsx`, `SpreadPage.tsx`

Some files use `const navigator = useNavigate()`, others use `const navigate = useNavigate()`. `navigator` conflicts with the global browser `window.navigator` object, which can cause confusion.

**Fix:** Standardize on `const navigate = useNavigate()` everywhere.

---

### 15. Unused Import: `use` from React in `App.tsx` (Low)

**File:** `src/App.tsx`

```ts
import { use, useEffect, useState } from 'react';
```

`use` is imported but never referenced. This is dead code that may confuse readers into thinking it's used somewhere.

**Fix:** Remove the `use` import.

---

### 16. `authTokenService` Is an Indirect Indirection Layer with No Clear Owner (Low)

**File:** `src/services/authTokenService.ts`

The service provides getter functions that must be "registered" via `setTokenGetter`/`setRefreshTokenGetter` before use. If `getAccessTokenValue()` is called before the getter is registered, it returns `null` silently. There is no indication in the codebase of where these setters are called.

**Fix:** Either read tokens directly from the Redux store (in Axios interceptors, accept the store as a parameter) or document clearly where the setters are called and add a warning log if `getAccessTokenValue` is called before registration.

---

## Priority Summary

| # | Issue | Priority |
|---|-------|----------|
| 1 | `dispatch` typed as `any` — no type safety | High |
| 2 | JWT tokens persisted to `localStorage` | High (Security) |
| 3 | `AccountModel` exposes IdentityUser internals | High (Security) |
| 4 | Auth guard duplicated across every protected page | High |
| 5 | `PageWrapper` defined inside `App`, remounts children | Medium |
| 6 | SignalR `useEffect` race condition on unmount | Medium |
| 7 | Inconsistent data fetching (direct API vs Redux vs RTK Query) | Medium |
| 8 | `rows` local state duplicates Redux store state | Medium |
| 9 | Array index used as DataGrid row `id` | Medium |
| 11 | Non-null assertion on URL search param | Medium |
| 10 | `typeLabels` map recreated on every render | Low |
| 12 | `console.log` statements in production paths | Low |
| 13 | `IRootStore` missing RTK Query slice | Low |
| 14 | Inconsistent `navigator` vs `navigate` naming | Low |
| 15 | Unused `use` import in `App.tsx` | Low |
| 16 | `authTokenService` getter registration undocumented | Low |
