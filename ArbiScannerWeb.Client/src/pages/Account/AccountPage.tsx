import React, { useEffect } from 'react';
import { useSelector } from 'react-redux';
import type { IRootStore } from '../../store/store';
import SettingsIcon from '@mui/icons-material/Settings';
import GuideModal from '../../components/GuideModal';
import type { GuideStep } from '../../components/GuideModal';
import type { AccountUpdateDTO } from '../../types/accountType';

const accountGuideSteps: GuideStep[] = [
    {
        icon: (
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
            </svg>
        ),
        title: 'Email & Spread Size',
        description: (
            <span>
                Keep your <strong>email</strong> up to date for account notifications.<br />
                Set a <strong>Spread Size Threshold</strong> — only spreads above this % will be shown and flagged as opportunities. Example: 0.5 means you only care about spreads ≥ 0.5%.
            </span>
        ),
    },
    {
        icon: (
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
        ),
        title: 'Position Size',
        description: 'Choose your trading position size — this is the dollar amount per trade leg. It is used to estimate your potential profit for each spread opportunity. Pick an amount that matches your risk tolerance.',
    },
    {
        icon: (
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4" />
            </svg>
        ),
        title: 'Monitoring Preferences',
        description: 'Toggle which spread types you want to track: Futures, Funding, or Spot. Select only the types you actively trade to reduce noise and focus on relevant opportunities.',
    },
    {
        icon: (
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
            </svg>
        ),
        title: 'Active Exchanges',
        description: 'Choose which exchanges to monitor. Only pairs involving your selected exchanges will be included in the live spread feed. Deselect exchanges you do not have accounts on.',
    },
    {
        icon: (
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
            </svg>
        ),
        title: 'Telegram Notifications',
        description: 'Connect your Telegram account to receive instant alerts when spreads matching your criteria appear. Click "Link Telegram" and follow the bot instructions. You can unlink at any time from this page.',
    },
];
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import { useNavigate } from 'react-router';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import InfoIcon from '@mui/icons-material/Info';
import TelegramLinkModal from '../../components/TelegramLinkModal';
import TelegramIcon from '@mui/icons-material/Telegram';
import { logger } from '../../services/loggerService';
import {
    useChangeEmailRequestMutation,
    useCreateTelegramLinkRequestMutation,
    useGetUserDataQuery,
    useRemoveTelegramLinkMutation,
    useUpdateAccountDetailsMutation,
} from '../../store/services/account';
import { useGetUserActiveSubscriptionsQuery } from '../../store/services/subscription';

export function AccountPage() {
    const userAccount = useSelector((state: IRootStore) => state.account.account);
    const userSettings = useSelector((state: IRootStore) => state.account.account.userSettings);
    const loading = useSelector((state: IRootStore) => state.account.loading);
    const error = useSelector((state: IRootStore) => state.account.error);
    const [email, setEmail] = React.useState('');
    const [originalEmail, setOriginalEmail] = React.useState('');
    const [spreadSize, setSpreadSize] = React.useState<number | string>('');
    const [positionSize, setPositionSize] = React.useState<number | ''>('');
    const [futuresSpread, setFuturesSpread] = React.useState(false);
    const [fundingSpread, setFundingSpread] = React.useState(false);
    const [spotSpread, setSpotSpread] = React.useState(false);
    const [exchanges, setExchanges] = React.useState<string[]>([]);
    const [emailError, setEmailError] = React.useState('');
    const [emailDialogOpen, setEmailDialogOpen] = React.useState(false);
    const [pendingData, setPendingData] = React.useState<AccountUpdateDTO | null>(null);
    const [telegramModalOpen, setTelegramModalOpen] = React.useState(false);
    const [telegramLinkRequestId, setTelegramLinkRequestId] = React.useState('');
    const [telegramActionLoading, setTelegramActionLoading] = React.useState(false);
    const [telegramActionError, setTelegramActionError] = React.useState('');

    const navigate = useNavigate();
    const isLoggedIn = useSelector((state: IRootStore) => state.account.isLoggedIn);
    const [updateAccountDetails] = useUpdateAccountDetailsMutation();
    const [changeEmailRequest] = useChangeEmailRequestMutation();
    const [createTelegramLinkRequest] = useCreateTelegramLinkRequestMutation();
    const [removeTelegramLink] = useRemoveTelegramLinkMutation();
    useGetUserDataQuery(undefined, { skip: !isLoggedIn });
    const { data: activeSubscriptionData } = useGetUserActiveSubscriptionsQuery(undefined, {
        skip: !isLoggedIn,
    });
    const userSubscription = activeSubscriptionData?.value;
    const isActiveSubscription = userSubscription?.isActive || false;

    useEffect(() => {
        if (!userSettings) return;
        setEmail(userAccount.email ?? '');
        setOriginalEmail(userAccount.email ?? '');
        setSpreadSize(userSettings.spreadSize ?? '');
        setPositionSize(userSettings.positionSize ?? '');
        setFuturesSpread(!!userSettings.futuresSpread);
        setFundingSpread(!!userSettings.fundingSpread);
        setSpotSpread(!!userSettings.spotSpread);
        setExchanges((userSettings.exchanges || []).map(e => e.exchange?.name ?? '').filter(Boolean));
    }, [userSettings, userAccount.email]);
    const validateEmail = () => {
        const re = /^\S+@\S+\.\S+$/;
        if (!email || !re.test(email)) {
            setEmailError('Please enter a valid email address.');
            return false;
        }
        setEmailError('');
        return true;
    };

    const handleSave = () => {
        if (!validateEmail()) return;

        const payload = {
            email,
            spreadSize: typeof spreadSize === 'number' ? spreadSize : Number(spreadSize) || 0,
            positionSize: typeof positionSize === 'number' ? positionSize : Number(positionSize) || 0,
            futuresSpread,
            fundingSpread,
            spotSpread,
            haveAccess: userSettings.haveAccess,
            exchanges: exchanges.map(name => ({ id: 0, userAccountId: '', exchangeId: 0, exchange: { id: 0, name } })),
        }
        // Check if email has changed
        if (email !== originalEmail) {
            setPendingData(payload);
            setEmailDialogOpen(true);
        } else {
            void updateAccountDetails(payload);
        }
    };

    const handleEmailChangeConfirm = async () => {
        setEmailDialogOpen(false);
        if (pendingData) {
            // Send email change request
            const res = await changeEmailRequest({ email }).unwrap();
            
            if (res?.isSuccess) {
                await updateAccountDetails(pendingData);
                navigate('/account/confirmemail?emailConfirmToken=' + res.value.id);
            }
            setPendingData(null);
        }
    };

    const handleEmailChangeCancel = () => {
        setEmailDialogOpen(false);
        setPendingData(null);
    };

    const handleLinkTelegram = async () => {
        try {
            const res = await createTelegramLinkRequest().unwrap();
            if (res && res.isSuccess && res.value) {
                setTelegramLinkRequestId(res.value.id);
                setTelegramModalOpen(true);
            }
        } catch (err) {
            const details = err instanceof Error ? err.message : 'Unknown telegram link request error';
            logger.error('Failed to create telegram link request', 'AccountPage', details);
        }
    };

    const handleTelegramModalClose = () => {
        setTelegramModalOpen(false);
        setTelegramLinkRequestId('');
    };

    const handleUnlinkTelegram = async () => {
        try {
            setTelegramActionError('');
            setTelegramActionLoading(true);
            const res = await removeTelegramLink().unwrap();
            if (res?.isSuccess) {
                return;
            }
            setTelegramActionError(res?.errors?.[0]?.message || 'Failed to unlink Telegram account.');
        } catch (err) {
            const details = err instanceof Error ? err.message : 'Unknown telegram unlink error';
            logger.error('Failed to remove telegram link', 'AccountPage', details);
            setTelegramActionError('Failed to unlink Telegram account.');
        } finally {
            setTelegramActionLoading(false);
        }
    };

    const isTelegramLinked = Boolean(userSettings?.userName || userSettings?.chatId);

    return (
        <>
        <GuideModal storageKey="guide_account_seen" title="Account Settings Guide" steps={accountGuideSteps} />
        <TelegramLinkModal
            isOpen={telegramModalOpen}
            linkRequestId={telegramLinkRequestId}
            onClose={handleTelegramModalClose}
        />
        <div className="max-w-7xl mx-auto mt-8 px-4 sm:px-6 lg:px-8 pb-20">
            {/* Header Section */}
            <div className="text-center mb-16 space-y-4">
                <h1 className="text-4xl md:text-5xl font-extrabold text-gray-900 tracking-tight">
                    Account Settings
                </h1>
                <p className="text-lg md:text-xl text-gray-500 max-w-2xl mx-auto">
                    Manage your profile, trading preferences, and exchange connections.
                </p>
            </div>

            {/* Subscription Status Section */}
            {/* {isLoading && (
                <div className="mb-8 bg-white rounded-3xl shadow-lg overflow-hidden border border-gray-100 p-8 text-center">
                    <div className="animate-pulse text-gray-400">Loading subscription...</div>
                </div>
            )} */}
            {(isActiveSubscription && userSubscription) && (
                <div className="mb-8 bg-linear-to-r from-green-50 to-emerald-50 rounded-3xl shadow-lg overflow-hidden border border-green-200">
                    <div className="p-8 flex items-start gap-6">
                        <div className="shrink-0">
                            <CheckCircleIcon sx={{ fontSize: 48, color: '#10b981' }} />
                        </div>
                        <div className="flex-1">
                            <h2 className="text-2xl font-bold text-gray-900 mb-2">Active Subscription</h2>
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                                <div>
                                    <p className="text-sm font-medium text-gray-600 mb-1">Subscription Type</p>
                                    <p className="text-lg font-semibold text-gray-900">{userSubscription.subscription?.type || 'Premium'}</p>
                                </div>
                                <div>
                                    <p className="text-sm font-medium text-gray-600 mb-1">Expires On</p>
                                    <p className="text-lg font-semibold text-gray-900">
                                        {new Date(userSubscription.endDate).toLocaleDateString('en-US', { 
                                            year: 'numeric', 
                                            month: 'long', 
                                            day: 'numeric' 
                                        })}
                                    </p>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            )}
            {!isActiveSubscription && (
                <div className="mb-8 bg-blue-50 rounded-3xl shadow-lg overflow-hidden border border-blue-200">
                    <div className="p-8 flex items-start gap-6">
                        <div className="shrink-0">
                            <InfoIcon sx={{ fontSize: 48, color: '#3b82f6' }} />
                        </div>
                        <div className="flex-1">
                            <h2 className="text-2xl font-bold text-gray-900 mb-2">No Active Subscription</h2>
                            <p className="text-gray-700 mb-4">
                                You don't currently have an active subscription. Explore our available options to unlock premium features.
                            </p>
                            <button
                                onClick={() => navigate('/subscriptions')}
                                className="inline-flex items-center px-6 py-3 bg-blue-600 text-white rounded-xl font-semibold hover:bg-blue-700 transition-colors shadow-md"
                            >
                                View Available Subscriptions →
                            </button>
                        </div>
                    </div>
                </div>
            )}

            <div className="bg-white rounded-3xl shadow-xl overflow-hidden border border-gray-100">
                <div className="bg-gray-50 p-6 md:p-8 border-b border-gray-100 flex items-center justify-center"> 
                     <div className="w-16 h-16 bg-white rounded-full flex items-center justify-center shadow-sm text-indigo-600">
                        <SettingsIcon fontSize="large" />
                    </div>
                </div>

                <div className="p-8 md:p-12 space-y-12">
                    
                    {/* Telegram Integration */}
                    <div>
                        <h3 className="text-lg font-bold text-gray-900 mb-6">Telegram Notifications</h3>
                        <div className="bg-gradient-to-r from-blue-50 to-cyan-50 rounded-2xl p-6 border border-blue-100 overflow-x-hidden">
                            {isTelegramLinked ? (
                                <div className="flex items-center gap-4">
                                    <div className="w-12 h-12 bg-blue-600 rounded-full flex items-center justify-center">
                                        <TelegramIcon sx={{ fontSize: 28, color: 'white' }} />
                                    </div>
                                    <div className="flex-1">
                                        <p className="font-semibold text-gray-900 mb-1">Telegram Connected</p>
                                        <p className="text-sm text-gray-600">
                                            {userSettings?.userName
                                                ? `@${userSettings.userName}`
                                                : `Chat ID: ${userSettings?.chatId}`}
                                        </p>
                                        {telegramActionError && (
                                            <p className="mt-2 text-sm text-red-600">{telegramActionError}</p>
                                        )}
                                    </div>
                                    <button
                                        type="button"
                                        onClick={handleUnlinkTelegram}
                                        disabled={telegramActionLoading}
                                        className="w-10 h-10 shrink-0 flex items-center justify-center rounded-full bg-red-100 text-red-700 font-medium hover:bg-red-200 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                                        title="Unlink Telegram"
                                    >
                                        {telegramActionLoading ? (
                                            <span className="w-4 h-4 border-2 border-red-400 border-t-transparent rounded-full animate-spin" />
                                        ) : (
                                            <span className="text-lg leading-none">&times;</span>
                                        )}
                                    </button>
                                    <CheckCircleIcon sx={{ fontSize: 32, color: '#10b981' }} className="hidden sm:block" />
                                </div>
                            ) : (
                                <div>
                                    <div className="flex items-start gap-4 mb-4">
                                        <div className="w-12 h-12 bg-blue-100 rounded-full flex items-center justify-center shrink-0">
                                            <TelegramIcon sx={{ fontSize: 28, color: '#3b82f6' }} />
                                        </div>
                                        <div>
                                            <p className="font-semibold text-gray-900 mb-2">Connect Telegram</p>
                                            <p className="text-sm text-gray-600 mb-4">
                                                Link your Telegram account to receive instant notifications about trading opportunities.
                                            </p>
                                            <button
                                                onClick={handleLinkTelegram}
                                                className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg font-medium hover:bg-blue-700 transition-colors shadow-sm"
                                            >
                                                <TelegramIcon sx={{ fontSize: 20 }} />
                                                Link Telegram Account
                                            </button>
                                        </div>
                                    </div>
                                </div>
                            )}
                        </div>
                    </div>

                    <div className="border-t border-gray-100" />

                    {/* General Settings */}
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">
                        <div>
                            <label className="block text-sm font-semibold text-gray-700 mb-2">Email Address</label>
                            <input
                                type="email"
                                value={email}
                                onChange={(e) => setEmail(e.target.value)}
                                onBlur={validateEmail}
                                className="block w-full rounded-xl border-gray-300 bg-gray-50 px-4 py-3 text-gray-900 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 transition-colors"
                                placeholder="your@email.com"
                            />
                            {emailError && (
                                <p className="mt-2 text-sm text-red-600 bg-red-50 px-3 py-1 rounded-lg">{emailError}</p>
                            )}
                        </div>

                        <div>
                            <label className="block text-sm font-semibold text-gray-700 mb-2">Spread Size Threshold</label>
                            <div className="relative rounded-md shadow-sm">
                                <input
                                    type="number"
                                    step="0.01"
                                    value={spreadSize}
                                    onChange={(e) => setSpreadSize(e.target.value)}
                                    className="block w-full rounded-xl border-gray-300 bg-gray-50 px-4 py-3 text-gray-900 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 transition-colors"
                                    placeholder="0.5"
                                />
                                <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-8">
                                    <span className="text-gray-500 sm:text-sm">%</span>
                                </div>
                            </div>
                        </div>

                        <div>
                            <label className="block text-sm font-semibold text-gray-700 mb-2">Position Size</label>
                            <select
                                value={positionSize as string | number}
                                onChange={(e) => setPositionSize(e.target.value === '' ? '' : Number(e.target.value))}
                                className="block w-full rounded-xl border-gray-300 bg-gray-50 px-4 py-3 text-gray-900 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 transition-colors"
                            >
                                <option value="">Select Size</option>
                                <option value={100}>$100</option>
                                <option value={300}>$300</option>
                                <option value={500}>$500</option>
                                <option value={1000}>$1,000</option>
                                <option value={5000}>$5,000</option>
                            </select>
                        </div>
                    </div>

                    <div className="border-t border-gray-100" />

                    {/* Spread Types */}
                    <div>
                        <h3 className="text-lg font-bold text-gray-900 mb-6">Monitoring Preferences</h3>
                        <div className="flex flex-wrap gap-4">
                            {[
                                { label: 'Futures Spreads', state: futuresSpread, setter: setFuturesSpread, color: 'indigo' },
                                { label: 'Funding Rates', state: fundingSpread, setter: setFundingSpread, color: 'indigo' },
                                { label: 'Spot Spreads', state: spotSpread, setter: setSpotSpread, color: 'indigo' },
                            ].map((item) => (
                                <label key={item.label} className={`
                                    cursor-pointer px-6 py-3 rounded-xl border transition-all duration-200 flex items-center gap-3
                                    ${item.state 
                                        ? `bg-${item.color}-50 border-${item.color}-200 text-${item.color}-700 shadow-sm` 
                                        : 'bg-white border-gray-200 text-gray-500 hover:bg-gray-50'
                                    }
                                `}>
                                    <input 
                                        type="checkbox" 
                                        checked={item.state} 
                                        onChange={(e) => item.setter(e.target.checked)} 
                                        className={`w-5 h-5 rounded border-gray-300 text-${item.color}-600 focus:ring-${item.color}-500`}
                                    />
                                    <span className="font-semibold">{item.label}</span>
                                </label>
                            ))}
                        </div>
                    </div>

                    <div className="border-t border-gray-100" />

                    {/* Exchanges */}
                    <div>
                        <h3 className="text-lg font-bold text-gray-900 mb-6">Active Exchanges</h3>
                        <div className="flex flex-wrap gap-3">
                            {[
                                'Binance', 'Bybit', 'OKX', 'KuCoin Futures', 'MEXC', 'Bitget', 'HTX', 'XT', 'CoinEX', 'LBank', 'WhiteBit', 'Gate.io', 'BingX'
                            ].map((ex) => {
                                const active = exchanges.includes(ex);
                                return (
                                    <button
                                        key={ex}
                                        type="button"
                                        onClick={() => active ? setExchanges(prev => prev.filter(e => e !== ex)) : setExchanges(prev => [...prev, ex])}
                                        className={`
                                            px-5 py-2.5 rounded-full font-medium text-sm transition-all duration-200 shadow-sm border
                                            ${active 
                                                ? 'bg-indigo-600 text-white border-indigo-600 hover:bg-indigo-700 hover:shadow-indigo-200' 
                                                : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50 hover:border-gray-300'
                                            }
                                        `}
                                    >
                                        {ex}
                                    </button>
                                );
                            })}
                        </div>
                    </div>

                    <div className="border-t border-gray-100" />

                    {/* Email Confirmation Dialog */}
                    {emailDialogOpen && (
                                <Dialog 
                                    open={emailDialogOpen} 
                                    onClose={handleEmailChangeCancel}
                                    PaperProps={{
                                        className: 'rounded-2xl'
                                    }}
                                >
                                    <DialogTitle className="text-xl font-bold text-gray-900 px-6 pt-6">
                                        Confirm Email Change
                                    </DialogTitle>
                                    <DialogContent className="px-6 py-4">
                                        <p className="text-gray-700">
                                            In order to use this email next time to sign in you need to confirm it.
                                        </p>
                                    </DialogContent>
                                    <DialogActions className="px-6 pb-6 gap-3">
                                        <button
                                            onClick={handleEmailChangeCancel}
                                            className="px-6 py-2.5 rounded-lg bg-gray-100 text-gray-700 font-medium hover:bg-gray-200 transition-colors"
                                        >
                                            Cancel
                                        </button>
                                        <button
                                            onClick={handleEmailChangeConfirm}
                                            className="px-6 py-2.5 rounded-lg bg-indigo-600 text-white font-medium hover:bg-indigo-700 transition-colors shadow-sm"
                                        >
                                            OK
                                        </button>
                                    </DialogActions>
                                </Dialog>
                                )
                            }
                        </div>
                    </div>

                    {/* Action Buttons */}
                    <div className="pt-6 flex items-center justify-end gap-4">
                         {error && (
                            <div className="text-sm font-medium text-red-600 bg-red-50 px-4 py-2 rounded-lg border border-red-100">
                                {error}
                            </div>
                        )}
                        <button 
                            onClick={handleSave}
                            disabled={loading}
                            className={`
                                px-8 py-3 bg-indigo-600 text-white rounded-xl font-bold shadow-lg shadow-indigo-200 
                                transition-all duration-200 transform hover:-translate-y-0.5 hover:shadow-xl
                                disabled:opacity-50 disabled:cursor-not-allowed disabled:transform-none
                            `}
                        >
                            {loading ? 'Saving...' : 'Save Changes'}
                        </button>
                    </div>
                </div>
        </>
    );
}

export default AccountPage;