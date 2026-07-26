import type { SubscriptionModel } from "../../types/accountType";
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome';
import { useTranslation } from 'react-i18next';
import { useLocalizedNavigate } from '../../i18n/routing';

function SubscriptionElement(subscription: Readonly<SubscriptionModel>) {
    const { t } = useTranslation('subscription');
    const navigate = useLocalizedNavigate();

    const handleSubscribe = () => {
        navigate(`/payment?id=${subscription.id}`);
    };

    return (
        <div className="bg-gradient-to-br from-indigo-950 via-purple-900 to-black rounded-2xl p-8 hover:shadow-2xl hover:shadow-purple-500/20 transition-all duration-300 border border-white/10 text-center text-white relative overflow-hidden">
            <div className="w-16 h-16 bg-white/10 backdrop-blur-lg rounded-full flex items-center justify-center mx-auto mb-6 text-purple-300 shadow-inner border border-white/20">
                <AutoAwesomeIcon fontSize="large" />
            </div>
            
            <h3 className="text-2xl font-bold mb-2 tracking-wide text-transparent bg-clip-text bg-gradient-to-r from-white to-purple-200">
                {subscription.type}
            </h3>

            <div className="text-gray-300 mb-8 space-y-3 font-light">
                <ul className="space-y-2">
                    <li className="flex items-center justify-center gap-2">
                        <span className="w-1.5 h-1.5 rounded-full bg-purple-400"></span>
                        {t('plan.featureAccess')}
                    </li>
                    <li className="flex items-center justify-center gap-2">
                        <span className="w-1.5 h-1.5 rounded-full bg-purple-400"></span>
                        {t('plan.featureSupport')}
                    </li>
                    <li className="flex items-center justify-center gap-2">
                        <span className="w-1.5 h-1.5 rounded-full bg-purple-400"></span>
                        {t('plan.featureDuration', { days: subscription.durationInDays })}
                    </li>
                </ul>
            </div>

            <button type="button" 
                onClick={handleSubscribe} 
                className="w-full py-3 px-6 rounded-xl bg-white/10 backdrop-blur-md border border-white/20 text-white font-semibold hover:bg-white/20 transition-all duration-200 shadow-lg group"
            >
                <span className="mr-1 text-lg">$</span>
                <span className="text-xl">{subscription.price}</span>
            </button>
        </div>
    );
}

export default SubscriptionElement;