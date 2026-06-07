import './App.css';
import { Toaster } from 'react-hot-toast';
import NavBar from './components/NavBar';
import { Route, Routes, useNavigate, useLocation } from 'react-router';
import { AnimatePresence, motion } from 'framer-motion';
import SpreadsPage from './pages/Spread/SpreadsPage';
import SpreadPage from './pages/Spread/SpreadPage';
import AccountPage from './pages/Account/AccountPage';
import LoginPage from './pages/Account/LoginPage';
import { useSelector } from 'react-redux';
import { clearUserData } from './store/slices/accountSlice';
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
import { useAppDispatch } from './hooks';
import { useGetUserDataQuery, useLogoutMutation } from './store/services/account';
import { useGetUserActiveSubscriptionsQuery } from './store/services/subscription';

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
    useGetUserDataQuery(undefined, { refetchOnMountOrArgChange: true });
    const { data: activeSubscriptionData } = useGetUserActiveSubscriptionsQuery(undefined, {
        skip: !isLoggedIn,
    });
    const isActiveSubscription = activeSubscriptionData?.value?.isActive || false;
    const dispatch = useAppDispatch();
    const location = useLocation();
    const navigate = useNavigate();
    const [logout] = useLogoutMutation();

    return (
        <div className="flex flex-col min-h-screen dark:bg-gray-950 transition-colors duration-200">
            <NavBar isLoggedIn={isLoggedIn} isActiveSubscription={isActiveSubscription} onLogin={() => { dispatch(clearUserData()); navigate("/account/login") }} onLogout={() => { void logout(); }} />

            <main className="flex-1 pt-20">
                <AnimatePresence mode="wait">
                    <Routes location={location} key={location.pathname}>
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
                        <Route path="*" element={<PageWrapper><div className="p-4">Page Not Found</div></PageWrapper>} />
                        <Route path="payment" element={<PageWrapper><PaymentInfoPage /></PageWrapper>} />
                        <Route path="payment/pay" element={<PageWrapper><PaymentCryptoPage /></PageWrapper>} />
                        <Route path="faq" element={<PageWrapper><FaqPage /></PageWrapper>} />
                    </Routes>
                </AnimatePresence>
            </main>
            <Footer/>
            <CookieConsentModal />
            <Toaster position="bottom-right" />
        </div>
    )
}

export default App;