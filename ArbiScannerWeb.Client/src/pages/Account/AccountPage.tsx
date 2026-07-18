import React, { useEffect } from 'react';
import toast from 'react-hot-toast';
import { useSelector } from 'react-redux';
import { Trans, useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import type { IRootStore } from '../../store/store';
import SettingsIcon from '@mui/icons-material/Settings';
import GuideModal from '../../components/GuideModal';
import type { GuideStep } from '../../components/GuideModal';
import type { AccountUpdateDTO } from '../../types/accountType';
import { useLocalizedNavigate } from '../../i18n/routing';
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

function buildAccountGuideSteps(t: TFunction<'account'>): GuideStep[] {
    return [
        {
            icon: (
                <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                </svg>
            ),
            title: t('accountPage.guide.steps.emailAndSpread.title'),
            description: (
                <span>
                    <Trans
                        i18nKey="accountPage.guide.steps.emailAndSpread.description"
                        ns="account"
                        components={{ bold: <strong />, br: <br /> }}
                    />
                </span>
            ),
        },
        {
            icon: (
                <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
            ),
            title: t('accountPage.guide.steps.positionSize.title'),
            description: t('accountPage.guide.steps.positionSize.description'),
        },
        {
            icon: (
                <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4" />
                </svg>
            ),
            title: t('accountPage.guide.steps.monitoring.title'),
            description: t('accountPage.guide.steps.monitoring.description'),
        },
        {
            icon: (
                <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
                </svg>
            ),
            title: t('accountPage.guide.steps.exchanges.title'),
            description: t('accountPage.guide.steps.exchanges.description'),
        },
        {
            icon: (
                <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
                </svg>
            ),
            title: t('accountPage.guide.steps.telegram.title'),
            description: t('accountPage.guide.steps.telegram.description'),
        },
    ];
}

export function AccountPage() {
    const { t, i18n } = useTranslation(['account', 'common']);
    const accountGuideSteps = buildAccountGuideSteps(t);
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
    const [emailChangeError, setEmailChangeError] = React.useState('');
    const [emailChangeLoading, setEmailChangeLoading] = React.useState(false);
    const [pendingData, setPendingData] = React.useState<AccountUpdateDTO | null>(null);
    const [telegramModalOpen, setTelegramModalOpen] = React.useState(false);
    const [telegramLinkRequestId, setTelegramLinkRequestId] = React.useState('');
    const [telegramActionLoading, setTelegramActionLoading] = React.useState(false);
    const [telegramActionError, setTelegramActionError] = React.useState('');

    const navigate = useLocalizedNavigate();
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
            setEmailError(t('common:validation.email.invalid'));
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
            void updateAccountDetails(payload).unwrap()
                .then(() => toast.success(t('accountPage.toasts.saveSuccess')))
                .catch(() => toast.error(t('accountPage.toasts.saveError')));
        }
    };

    const handleEmailChangeConfirm = async () => {
        if (!pendingData) return;
        setEmailChangeError('');
        setEmailChangeLoading(true);
        try {
            const res = await changeEmailRequest({ email }).unwrap();
            if (res?.isSuccess) {
                setEmailDialogOpen(false);
                setPendingData(null);
                await updateAccountDetails(pendingData);
                navigate('/account/confirmemail?emailConfirmToken=' + res.value.id);
            } else {
                setEmailChangeError(res?.errors?.[0]?.message || t('accountPage.emailChangeDialog.confirmError'));
            }
        } catch (err) {
            setEmailChangeError(err instanceof Error ? err.message : t('errors.networkError'));
        } finally {
            setEmailChangeLoading(false);
        }
    };

    const handleEmailChangeCancel = () => {
        setEmailDialogOpen(false);
        setPendingData(null);
        setEmailChangeError('');
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
            setTelegramActionError(res?.errors?.[0]?.message || t('accountPage.telegram.unlinkError'));
        } catch (err) {
            const details = err instanceof Error ? err.message : 'Unknown telegram unlink error';
            logger.error('Failed to remove telegram link', 'AccountPage', details);
            setTelegramActionError(t('accountPage.telegram.unlinkError'));
        } finally {
            setTelegramActionLoading(false);
        }
    };

    const isTelegramLinked = Boolean(userSettings?.userName || userSettings?.chatId);

    return (
        <>
        <GuideModal storageKey="guide_account_seen" title={t('accountPage.guide.title')} steps={accountGuideSteps} />
        {emailDialogOpen && (
            <div className="fixed inset-0 z-50 flex items-center justify-center backdrop-blur-md bg-white/50 dark:bg-black/50">
                <button
                    className="absolute opacity-0 inset-0 w-full h-full cursor-default"
                    onClick={handleEmailChangeCancel}
                    aria-label={t('common:actions.close')}
                    tabIndex={-1}
                />
                <dialog
                    open
                    className="relative bg-white dark:bg-gray-900 w-full sm:max-w-md sm:mx-4 sm:rounded-2xl shadow-2xl flex flex-col overflow-hidden m-0 p-0 border-0"
                >
                    <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 dark:border-gray-700 bg-linear-to-r from-indigo-600 to-blue-600 text-white rounded-t-2xl">
                        <div className="flex items-center gap-2">
                            <svg className="w-5 h-5 opacity-80" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                            </svg>
                            <h2 className="text-base font-bold tracking-wide">{t('accountPage.emailChangeDialog.title')}</h2>
                        </div>
                        <button
                            onClick={handleEmailChangeCancel}
                            className="text-white/70 hover:text-white transition-colors p-1 rounded-lg hover:bg-white/10"
                            aria-label={t('common:actions.close')}
                        >
                            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                            </svg>
                        </button>
                    </div>
                    <div className="px-6 py-8 flex flex-col items-center text-center gap-4">
                        <div className="w-16 h-16 rounded-2xl bg-indigo-50 dark:bg-indigo-900/30 flex items-center justify-center text-indigo-600 dark:text-indigo-400 shrink-0">
                            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                            </svg>
                        </div>
                        <p className="text-gray-600 dark:text-gray-400 text-sm leading-relaxed max-w-sm">
                            <Trans
                                i18nKey="accountPage.emailChangeDialog.body"
                                ns="account"
                                values={{ email }}
                                components={{ bold: <span className="font-semibold text-gray-900 dark:text-gray-100" /> }}
                            />
                        </p>
                        {emailChangeError && (
                            <p className="w-full text-sm text-red-700 bg-red-50 dark:bg-red-900/20 rounded-lg px-3 py-2 ring-1 ring-red-100 dark:ring-red-800">{emailChangeError}</p>
                        )}
                    </div>
                    <div className="px-6 py-4 border-t border-gray-100 dark:border-gray-700 flex items-center justify-end gap-3">
                        <button
                            onClick={handleEmailChangeCancel}
                            disabled={emailChangeLoading}
                            className="px-5 py-2 rounded-xl text-sm font-semibold bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors disabled:opacity-50"
                        >
                            {t('common:actions.cancel')}
                        </button>
                        <button
                            onClick={handleEmailChangeConfirm}
                            disabled={emailChangeLoading}
                            className="px-5 py-2 rounded-xl text-sm font-semibold bg-indigo-600 text-white hover:bg-indigo-700 transition-colors shadow-sm disabled:opacity-50"
                        >
                            {emailChangeLoading ? t('accountPage.emailChangeDialog.sending') : t('accountPage.emailChangeDialog.confirm')}
                        </button>
                    </div>
                </dialog>
            </div>
        )}
        <TelegramLinkModal
            isOpen={telegramModalOpen}
            linkRequestId={telegramLinkRequestId}
            onClose={handleTelegramModalClose}
        />
        <div className="max-w-7xl mx-auto mt-8 px-4 sm:px-6 lg:px-8 pb-20">
            {/* Header Section */}
            <div className="text-center mb-16 space-y-4">
                <h1 className="text-4xl md:text-5xl font-extrabold text-gray-900 dark:text-gray-100 tracking-tight">
                    {t('accountPage.header.title')}
                </h1>
                <p className="text-lg md:text-xl text-gray-500 dark:text-gray-400 max-w-2xl mx-auto">
                    {t('accountPage.header.subtitle')}
                </p>
            </div>

            {(isActiveSubscription && userSubscription) && (
                <div className="mb-8 bg-linear-to-r from-green-50 to-emerald-50 dark:from-green-900/20 dark:to-emerald-900/20 rounded-3xl shadow-lg overflow-hidden border border-green-200 dark:border-green-800">
                    <div className="p-8 flex items-start gap-6">
                        <div className="shrink-0">
                            <CheckCircleIcon sx={{ fontSize: 48, color: '#10b981' }} />
                        </div>
                        <div className="flex-1">
                            <h2 className="text-2xl font-bold text-gray-900 dark:text-gray-100 mb-2">{t('accountPage.subscription.active.title')}</h2>
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                                <div>
                                    <p className="text-sm font-medium text-gray-600 dark:text-gray-400 mb-1">{t('accountPage.subscription.active.typeLabel')}</p>
                                    <p className="text-lg font-semibold text-gray-900 dark:text-gray-100">{userSubscription.subscription?.type || t('accountPage.subscription.active.premiumFallback')}</p>
                                </div>
                                <div>
                                    <p className="text-sm font-medium text-gray-600 dark:text-gray-400 mb-1">{t('accountPage.subscription.active.expiresLabel')}</p>
                                    <p className="text-lg font-semibold text-gray-900 dark:text-gray-100">
                                        {new Date(userSubscription.endDate).toLocaleDateString(i18n.language, {
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
                <div className="mb-8 bg-blue-50 dark:bg-blue-900/20 rounded-3xl shadow-lg overflow-hidden border border-blue-200 dark:border-blue-800">
                    <div className="p-8 flex items-start gap-6">
                        <div className="shrink-0">
                            <InfoIcon sx={{ fontSize: 48, color: '#3b82f6' }} />
                        </div>
                        <div className="flex-1">
                            <h2 className="text-2xl font-bold text-gray-900 dark:text-gray-100 mb-2">{t('accountPage.subscription.none.title')}</h2>
                            <p className="text-gray-700 dark:text-gray-300 mb-4">
                                {t('accountPage.subscription.none.body')}
                            </p>
                            <button
                                onClick={() => navigate('/subscriptions')}
                                className="inline-flex items-center px-6 py-3 bg-blue-600 text-white rounded-xl font-semibold hover:bg-blue-700 transition-colors shadow-md"
                            >
                                {t('accountPage.subscription.none.viewButton')}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            <div className="bg-white dark:bg-gray-900 rounded-3xl shadow-xl overflow-hidden border border-gray-100 dark:border-gray-700 transition-colors duration-200">
                <div className="bg-gray-50 dark:bg-gray-800 p-6 md:p-8 border-b border-gray-100 dark:border-gray-700 flex items-center justify-center">
                     <div className="w-16 h-16 bg-white dark:bg-gray-700 rounded-full flex items-center justify-center shadow-sm text-indigo-600 dark:text-indigo-400">
                        <SettingsIcon fontSize="large" />
                    </div>
                </div>

                <div className="p-8 md:p-12 space-y-12">

                    {/* Telegram Integration */}
                    <div>
                        <h3 className="text-lg font-bold text-gray-900 dark:text-gray-100 mb-6">{t('accountPage.telegram.sectionTitle')}</h3>
                        <div className="bg-linear-to-r from-blue-50 to-cyan-50 dark:from-blue-900/20 dark:to-cyan-900/20 rounded-2xl p-6 border border-blue-100 dark:border-blue-800 overflow-x-hidden">
                            {isTelegramLinked ? (
                                <div className="flex items-center gap-4">
                                    <div className="w-12 h-12 bg-blue-600 rounded-full flex items-center justify-center">
                                        <TelegramIcon sx={{ fontSize: 28, color: 'white' }} />
                                    </div>
                                    <div className="flex-1">
                                        <p className="font-semibold text-gray-900 dark:text-gray-100 mb-1">{t('accountPage.telegram.connected.label')}</p>
                                        <p className="text-sm text-gray-600 dark:text-gray-400">
                                            {userSettings?.userName
                                                ? `@${userSettings.userName}`
                                                : t('accountPage.telegram.connected.chatIdLabel', { chatId: userSettings?.chatId })}
                                        </p>
                                        {telegramActionError && (
                                            <p className="mt-2 text-sm text-red-600">{telegramActionError}</p>
                                        )}
                                    </div>
                                    <button
                                        type="button"
                                        onClick={handleUnlinkTelegram}
                                        disabled={telegramActionLoading}
                                        className="w-10 h-10 shrink-0 flex items-center justify-center rounded-full bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400 font-medium hover:bg-red-200 dark:hover:bg-red-900/50 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                                        title={t('accountPage.telegram.unlinkAria')}
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
                                        <div className="w-12 h-12 bg-blue-100 dark:bg-blue-900/40 rounded-full flex items-center justify-center shrink-0">
                                            <TelegramIcon sx={{ fontSize: 28, color: '#3b82f6' }} />
                                        </div>
                                        <div>
                                            <p className="font-semibold text-gray-900 dark:text-gray-100 mb-2">{t('accountPage.telegram.notConnected.label')}</p>
                                            <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
                                                {t('accountPage.telegram.notConnected.description')}
                                            </p>
                                            <button
                                                onClick={handleLinkTelegram}
                                                className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg font-medium hover:bg-blue-700 transition-colors shadow-sm"
                                            >
                                                <TelegramIcon sx={{ fontSize: 20 }} />
                                                {t('accountPage.telegram.notConnected.linkButton')}
                                            </button>
                                        </div>
                                    </div>
                                </div>
                            )}
                        </div>
                    </div>

                    <div className="border-t border-gray-100 dark:border-gray-700" />

                    {/* General Settings */}
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">
                        <div>
                            <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">{t('accountPage.form.emailLabel')}</label>
                            <input
                                type="email"
                                value={email}
                                onChange={(e) => setEmail(e.target.value)}
                                onBlur={validateEmail}
                                className="block w-full rounded-xl border-gray-300 dark:border-gray-600 bg-gray-50 dark:bg-gray-800 px-4 py-3 text-gray-900 dark:text-gray-100 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 transition-colors"
                                placeholder={t('accountPage.form.emailPlaceholder')}
                            />
                            {emailError && (
                                <p className="mt-2 text-sm text-red-600 bg-red-50 dark:bg-red-900/20 px-3 py-1 rounded-lg">{emailError}</p>
                            )}
                        </div>

                        <div>
                            <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">{t('accountPage.form.spreadSizeLabel')}</label>
                            <div className="relative rounded-md shadow-sm">
                                <input
                                    type="number"
                                    step="0.01"
                                    value={spreadSize}
                                    onChange={(e) => setSpreadSize(e.target.value)}
                                    className="block w-full rounded-xl border-gray-300 dark:border-gray-600 bg-gray-50 dark:bg-gray-800 px-4 py-3 text-gray-900 dark:text-gray-100 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 transition-colors"
                                    placeholder={t('accountPage.form.spreadSizePlaceholder')}
                                />
                                <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-8">
                                    <span className="text-gray-500 dark:text-gray-400 sm:text-sm">%</span>
                                </div>
                            </div>
                        </div>

                        <div>
                            <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">{t('accountPage.form.positionSizeLabel')}</label>
                            <select
                                value={positionSize as string | number}
                                onChange={(e) => setPositionSize(e.target.value === '' ? '' : Number(e.target.value))}
                                className="block w-full rounded-xl border-gray-300 dark:border-gray-600 bg-gray-50 dark:bg-gray-800 px-4 py-3 text-gray-900 dark:text-gray-100 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 transition-colors"
                            >
                                <option value="">{t('accountPage.form.positionSizeSelectPlaceholder')}</option>
                                <option value={100}>$100</option>
                                <option value={300}>$300</option>
                                <option value={500}>$500</option>
                                <option value={1000}>$1,000</option>
                                <option value={5000}>$5,000</option>
                            </select>
                        </div>
                    </div>

                    <div className="border-t border-gray-100 dark:border-gray-700" />

                    {/* Spread Types */}
                    <div>
                        <h3 className="text-lg font-bold text-gray-900 dark:text-gray-100 mb-6">{t('accountPage.form.monitoringTitle')}</h3>
                        <div className="flex flex-wrap gap-4">
                            {[
                                { key: 'futures', label: t('accountPage.form.futuresSpreads'), state: futuresSpread, setter: setFuturesSpread, color: 'indigo' },
                                { key: 'funding', label: t('accountPage.form.fundingRates'), state: fundingSpread, setter: setFundingSpread, color: 'indigo' },
                                { key: 'spot', label: t('accountPage.form.spotSpreads'), state: spotSpread, setter: setSpotSpread, color: 'indigo' },
                            ].map((item) => (
                                <label key={item.key} className={`
                                    cursor-pointer px-6 py-3 rounded-xl border transition-all duration-200 flex items-center gap-3
                                    ${item.state
                                        ? `bg-${item.color}-50 dark:bg-${item.color}-900/30 border-${item.color}-200 dark:border-${item.color}-700 text-${item.color}-700 dark:text-${item.color}-300 shadow-sm`
                                        : 'bg-white dark:bg-gray-800 border-gray-200 dark:border-gray-600 text-gray-500 dark:text-gray-400 hover:bg-gray-50 dark:hover:bg-gray-700'
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

                    <div className="border-t border-gray-100 dark:border-gray-700" />

                    {/* Exchanges */}
                    <div>
                        <h3 className="text-lg font-bold text-gray-900 dark:text-gray-100 mb-6">{t('accountPage.form.exchangesTitle')}</h3>
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
                                                : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-400 border-gray-200 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700 hover:border-gray-300 dark:hover:border-gray-500'
                                            }
                                        `}
                                    >
                                        {ex}
                                    </button>
                                );
                            })}
                        </div>
                    </div>

                        </div>
                    </div>

                    {/* Action Buttons */}
                    <div className="pt-6 flex items-center justify-end gap-4">
                         {error && (
                            <div className="text-sm font-medium text-red-600 bg-red-50 dark:bg-red-900/20 px-4 py-2 rounded-lg border border-red-100 dark:border-red-800">
                                {error}
                            </div>
                        )}
                        <button
                            onClick={handleSave}
                            disabled={loading}
                            className={`
                                px-8 py-3 bg-indigo-600 text-white rounded-xl font-bold shadow-lg
                                transition-all duration-200 transform hover:-translate-y-0.5 hover:shadow-xl
                                disabled:opacity-50 disabled:cursor-not-allowed disabled:transform-none
                            `}
                        >
                            {loading ? t('accountPage.actions.saving') : t('accountPage.actions.save')}
                        </button>
                    </div>
                </div>
        </>
    );
}

export default AccountPage;
