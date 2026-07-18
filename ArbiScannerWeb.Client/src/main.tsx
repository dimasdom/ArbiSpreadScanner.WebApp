import { StrictMode, Suspense } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import './i18n/config.ts'
import App from './App.tsx'
import { BrowserRouter } from 'react-router'
import store from './store/store.ts'
import { Provider } from 'react-redux'
import { logger } from './services/loggerService.ts'
import ErrorBoundary from './components/ErrorBoundary.tsx'
import { ThemeProvider } from './contexts/ThemeContext.tsx'
import MuiBridge from './contexts/MuiBridge.tsx'
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
        <Provider store={store}>
            <BrowserRouter>
                <ThemeProvider>
                    <MuiBridge>
                        <ErrorBoundary>
                            <Suspense fallback={<FullPageLoader />}>
                                <App />
                            </Suspense>
                        </ErrorBoundary>
                    </MuiBridge>
                </ThemeProvider>
            </BrowserRouter>
        </Provider>
    </StrictMode>,
)
