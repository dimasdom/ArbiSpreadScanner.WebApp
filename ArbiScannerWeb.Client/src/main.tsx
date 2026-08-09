import { StrictMode, Suspense } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import './i18n/config.ts'
import App from './App.tsx'
import { BrowserRouter } from 'react-router'
import store from './store/store.ts'
import { Provider } from 'react-redux'
import { AuthProvider } from 'react-oidc-context'
import { oidcUserManager } from './services/oidcUserManager.ts'
import { logger } from './services/loggerService.ts'
import ErrorBoundary from './components/ErrorBoundary.tsx'
import { ThemeProvider } from './contexts/ThemeContext.tsx'
import MuiBridge from './contexts/MuiBridge.tsx'
import { ChatContextProvider } from './contexts/ChatContext.tsx'
import FullPageLoader from './components/FullPageLoader.tsx'

window.onerror = (message, source, lineno, colno, error) => {
    logger.error(
        typeof message === 'string' ? message : 'Unknown error',
        source ?? 'window.onerror',
        error?.stack ?? `line ${lineno}:${colno}`
    );
    return false;
};

window.addEventListener('unhandledrejection', (event) => {
    const reason = event.reason;
    const message = reason instanceof Error ? reason.message : String(reason ?? 'Unhandled promise rejection');
    const details = reason instanceof Error ? reason.stack : undefined;
    logger.error(message, 'unhandledrejection', details);
});

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <AuthProvider
            userManager={oidcUserManager}
            onSigninCallback={() => {
                // Strip ?code=&state= so a re-render doesn't re-process the same
                // callback (oidc-client-ts would reject it: state already consumed).
                window.history.replaceState({}, document.title, window.location.pathname);
            }}
        >
            <Provider store={store}>
                <BrowserRouter>
                    <ThemeProvider>
                        <MuiBridge>
                            <ErrorBoundary>
                                <Suspense fallback={<FullPageLoader />}>
                                    <ChatContextProvider>
                                        <App />
                                    </ChatContextProvider>
                                </Suspense>
                            </ErrorBoundary>
                        </MuiBridge>
                    </ThemeProvider>
                </BrowserRouter>
            </Provider>
        </AuthProvider>
    </StrictMode>,
)
