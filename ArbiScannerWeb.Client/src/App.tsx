import './App.css';
import { Toaster } from 'react-hot-toast';
import NavBar from './components/NavBar';
import AuthLoadingOverlay from './components/AuthLoadingOverlay';
import { Route, Routes, useLocation } from 'react-router';
import { AnimatePresence, motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import SpreadsPage from './pages/Spread/SpreadsPage';
import SpreadPage from './pages/Spread/SpreadPage';
import AccountPage from './pages/Account/AccountPage';
import LoginPage from './pages/Account/LoginPage';
import { useSelector } from 'react-redux';
import { Suspense, useEffect } from 'react';
import { clearUserData, hasStoredSession, markSessionChecked } from './store/slices/accountSlice';
import type { IRootStore } from './store/store';
import RegisterPage from './pages/Account/RegisterPage';
import ConfirmEmailPage from './pages/Account/ConfirmEmailPage';
import ForgotPasswordPage from './pages/Account/ForgotPasswordPage';
import ResetPasswordPage from './pages/Account/ResetPasswordPage';
import MainPage from './pages/Main/MainPage';
import SubscriptionPage from './pages/Subscription/SubscriptionPage';
import PaymentCryptoPage from './pages/Subscription/Payment/PaymentCryptoPage';
import PaymentInfoPage from './pages/Subscription/Payment/PaymentInfoPage';
import FaqPage from './pages/Faq/FaqPage';
import Footer from './components/Footer';
import ProtectedRoute from './components/ProtectedRoute';
import CookieConsentModal from './components/CookieConsentModal';
import { LangGuard, RootRedirect } from './components/LangGuard';
import { useAppDispatch } from './hooks';
import { useLocalizedNavigate, stripLangPrefix } from './i18n/routing';
import { useGetUserDataQuery, useLogoutMutation } from './store/services/account';
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
    const isLoggedIn = useSelector((state: IRootStore) => state.account.isLoggedIn);
    const sessionChecked = useSelector((state: IRootStore) => state.account.sessionChecked);
    const dispatch = useAppDispatch();
    const hadSession = hasStoredSession();
    const showAuthLoader = hadSession && !sessionChecked;
    useGetUserDataQuery(undefined, { skip: !hadSession, refetchOnMountOrArgChange: true });
    useEffect(() => {
        if (!hadSession) {
            dispatch(markSessionChecked());
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);
    const { data: activeSubscriptionData } = useGetUserActiveSubscriptionsQuery(undefined, {
        skip: !isLoggedIn,
    });
    const isActiveSubscription = activeSubscriptionData?.value?.isActive || false;
    const location = useLocation();
    const navigate = useLocalizedNavigate();
    const [logout] = useLogoutMutation();
    const { t } = useTranslation('common');

    return (
        <div className="flex flex-col min-h-screen dark:bg-gray-950 transition-colors duration-200">
            <AuthLoadingOverlay show={showAuthLoader} />
            <NavBar isLoggedIn={isLoggedIn} isActiveSubscription={isActiveSubscription} onLogin={() => { dispatch(clearUserData()); navigate("/account/login") }} onLogout={() => { void logout(); }} />

            <main className="flex-1 pt-20">
                <Suspense fallback={<PageLoader />}>
                    <AnimatePresence mode="wait">
                        <Routes location={location} key={stripLangPrefix(location.pathname)}>
                            <Route path="/" element={<RootRedirect />} />
                            <Route path=":lang" element={<LangGuard />}>
                                <Route path="account">
                                    <Route index element={<ProtectedRoute><PageWrapper><AccountPage /></PageWrapper></ProtectedRoute>} />
                                    <Route path="login" element={<PageWrapper><LoginPage /></PageWrapper>} />
                                    <Route path="register" element={<PageWrapper><RegisterPage /></PageWrapper>} />
                                    <Route path="confirmemail" element={<PageWrapper><ConfirmEmailPage /></PageWrapper>} />
                                    <Route path="resetpassword" element={<PageWrapper><ResetPasswordPage /></PageWrapper>} />
                                    <Route path="forgotpassword" element={<PageWrapper><ForgotPasswordPage /></PageWrapper>} />
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
            <Toaster position="bottom-right" />
        </div>
    )
}

export default App;