import './App.css';
import { Toaster } from 'react-hot-toast';
import NavBar from './components/NavBar';
import AuthLoadingOverlay from './components/AuthLoadingOverlay';
import { Route, Routes, useLocation } from 'react-router';
import { AnimatePresence, motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { useAuth } from 'react-oidc-context';
import SpreadsPage from './pages/Spread/SpreadsPage';
import SpreadPage from './pages/Spread/SpreadPage';
import AccountPage from './pages/Account/AccountPage';
import McpTokenPage from './pages/McpToken/McpTokenPage';
import AuthCallbackPage from './pages/Auth/AuthCallbackPage';
import { useSelector } from 'react-redux';
import { Suspense, useEffect } from 'react';
import { logout, markSessionChecked } from './store/slices/accountSlice';
import type { IRootStore } from './store/store';
import MainPage from './pages/Main/MainPage';
import SubscriptionPage from './pages/Subscription/SubscriptionPage';
import PaymentCryptoPage from './pages/Subscription/Payment/PaymentCryptoPage';
import PaymentInfoPage from './pages/Subscription/Payment/PaymentInfoPage';
import FaqPage from './pages/Faq/FaqPage';
import Footer from './components/Footer';
import ProtectedRoute from './components/ProtectedRoute';
import CookieConsentModal from './components/CookieConsentModal';
import ChatWidget from './components/ChatWidget';
import { LangGuard, RootRedirect } from './components/LangGuard';
import { useAppDispatch } from './hooks';
import { stripLangPrefix } from './i18n/routing';
import { useGetUserDataQuery } from './store/services/account';
import { useGetUserActiveSubscriptionsQuery } from './store/services/subscription';

const PageLoader = () => (
    <div className="flex items-center justify-center min-h-64 mt-6">
        <div className="w-10 h-10 border-4 border-gray-200 border-t-indigo-500 rounded-full animate-spin" />
    </div>
);

const PageWrapper: React.FC<{ children: React.ReactNode }> = ({ children }) => (
    <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        transition={{ duration: 0.18 }}
        className="min-h-full"
    >
        {children}
    </motion.div>
);


function App() {
    const auth = useAuth();
    const isLoggedIn = useSelector((state: IRootStore) => state.account.isLoggedIn);
    const dispatch = useAppDispatch();
    const showAuthLoader = auth.isLoading;
    useGetUserDataQuery(undefined, { skip: !auth.isAuthenticated, refetchOnMountOrArgChange: true });
    useEffect(() => {
        if (auth.isLoading) return;
        dispatch(markSessionChecked());
        if (!auth.isAuthenticated) {
            dispatch(logout());
        }
    }, [auth.isLoading, auth.isAuthenticated, dispatch]);
    const { data: activeSubscriptionData } = useGetUserActiveSubscriptionsQuery(undefined, {
        skip: !isLoggedIn,
    });
    const isActiveSubscription = activeSubscriptionData?.value?.isActive || false;
    const location = useLocation();
    const { t } = useTranslation('common');

    return (
        <div className="flex flex-col min-h-screen dark:bg-gray-950 transition-colors duration-200">
            <AuthLoadingOverlay show={showAuthLoader} />
            <NavBar isLoggedIn={isLoggedIn} isActiveSubscription={isActiveSubscription} onLogin={() => { void auth.signinRedirect(); }} onLogout={() => { void auth.signoutRedirect(); }} />

            <main className="flex-1 pt-20">
                <Suspense fallback={<PageLoader />}>
                    <AnimatePresence mode="wait">
                        <Routes location={location} key={stripLangPrefix(location.pathname)}>
                            <Route path="/" element={<RootRedirect />} />
                            <Route path="auth/callback" element={<AuthCallbackPage />} />
                            <Route path=":lang" element={<LangGuard />}>
                                <Route path="account">
                                    <Route index element={<ProtectedRoute><PageWrapper><AccountPage /></PageWrapper></ProtectedRoute>} />
                                </Route>
                                <Route path="mcp-token">
                                    <Route index element={<ProtectedRoute><PageWrapper><McpTokenPage /></PageWrapper></ProtectedRoute>} />
                                </Route>
                                <Route index element={<PageWrapper><MainPage /></PageWrapper>} />
                                <Route path="spreads" element={<ProtectedRoute requireActiveSubscription><PageWrapper><SpreadsPage /></PageWrapper></ProtectedRoute>} />
                                <Route path="spread" element={<ProtectedRoute requireActiveSubscription><PageWrapper><SpreadPage /></PageWrapper></ProtectedRoute>} />
                                <Route path="subscriptions" element={<PageWrapper><SubscriptionPage /></PageWrapper>} />
                                <Route path="payment" element={<PageWrapper><PaymentInfoPage /></PageWrapper>} />
                                <Route path="payment/pay" element={<PageWrapper><PaymentCryptoPage /></PageWrapper>} />
                                <Route path="faq" element={<PageWrapper><FaqPage /></PageWrapper>} />
                                <Route path="*" element={<PageWrapper><div className="p-4">{t('notFound')}</div></PageWrapper>} />
                            </Route>
                        </Routes>
                    </AnimatePresence>
                </Suspense>
            </main>
            <Footer/>
            <CookieConsentModal />
            <ChatWidget />
            <Toaster position="bottom-right" />
        </div>
    )
}

export default App;