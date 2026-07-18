import { useTranslation } from 'react-i18next';
import SubscriptionElement from "./SubcriptionElement";
import { CircularProgress } from "@mui/material";
import type { SubscriptionModel } from '../../types/accountType';
import { useSubscriptionPage } from '../../hooks/useSubscriptionPage';
import ErrorState from '../../components/ErrorState';

function SubscriptionPage() {
    const { t } = useTranslation('subscription');
    const { subscriptions, isLoading, error } = useSubscriptionPage();

     if (isLoading) {
         return (
             <div className="flex h-[80vh] items-center justify-center">
                 <CircularProgress size={60} thickness={4} className="text-indigo-600" />
             </div>
         );
     }

     if (error) {
         return <ErrorState message={t('subscriptionPage.loadError')} />;
     }

    return (
        <div className="max-w-7xl mx-auto mt-8 px-4 sm:px-6 lg:px-8 pb-20">
            <div className="text-center mb-16 space-y-4">
                <h1 className="text-4xl md:text-5xl font-extrabold text-gray-900 dark:text-gray-100 tracking-tight">
                    {t('subscriptionPage.title')}
                </h1>
                <p className="text-lg md:text-xl text-gray-500 dark:text-gray-400 max-w-2xl mx-auto">
                    {t('subscriptionPage.subtitle')}
                </p>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-8 justify-items-center">
                {subscriptions?.map((sub: SubscriptionModel) => (
                    <div key={sub.id || sub.type} className="w-full max-w-sm transform hover:-translate-y-2 transition-transform duration-300">
                        <SubscriptionElement {...sub}  />
                    </div>
                ))}
            </div>
        </div>
    );
}
export default SubscriptionPage;