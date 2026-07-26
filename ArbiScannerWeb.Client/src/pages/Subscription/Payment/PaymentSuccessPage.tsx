import { useTranslation } from 'react-i18next';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import AutoGraphIcon from '@mui/icons-material/AutoGraph';
import SecurityIcon from '@mui/icons-material/Security';
import BoltIcon from '@mui/icons-material/Bolt';
import { useLocalizedNavigate } from '../../../i18n/routing';

function PaymentSuccessPage() {
    const { t } = useTranslation('subscription');
    const navigate = useLocalizedNavigate();

    const handleGoHome = () => {
        navigate('/');
    };

    return (
        <div className="max-w-3xl mx-auto mt-12 px-4 sm:px-6 lg:px-8 pb-20">
            <div className="bg-white dark:bg-gray-900 rounded-3xl shadow-xl overflow-hidden border border-gray-100 dark:border-gray-700 text-center transition-colors duration-200">
                <div className="bg-green-50 dark:bg-green-900/20 p-12 border-b border-green-100 dark:border-green-800">
                    <div className="w-24 h-24 bg-green-100 dark:bg-green-900/40 rounded-full flex items-center justify-center mx-auto mb-6 text-green-600 shadow-sm animate-bounce">
                        <CheckCircleIcon style={{ fontSize: 60 }} />
                    </div>
                    <h1 className="text-4xl font-extrabold text-gray-900 dark:text-gray-100 mb-4 tracking-tight">
                        {t('payment.success.heading')}
                    </h1>
                    <p className="text-xl text-green-700 dark:text-green-400 font-medium">
                        {t('payment.success.subheading')}
                    </p>
                </div>

                <div className="p-12 space-y-8">
                    <p className="text-gray-600 dark:text-gray-400 text-lg max-w-xl mx-auto leading-relaxed">
                        {t('payment.success.body')}
                    </p>

                    <div className="grid grid-cols-1 md:grid-cols-3 gap-6 text-left">
                        <div className="p-6 bg-gray-50 dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700">
                            <div className="text-indigo-600 dark:text-indigo-400 mb-3"><AutoGraphIcon fontSize="large" /></div>
                            <h3 className="font-bold text-gray-900 dark:text-gray-100">{t('payment.success.features.realTimeTitle')}</h3>
                            <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">{t('payment.success.features.realTimeDesc')}</p>
                        </div>
                        <div className="p-6 bg-gray-50 dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700">
                            <div className="text-blue-600 dark:text-blue-400 mb-3"><SecurityIcon fontSize="large" /></div>
                            <h3 className="font-bold text-gray-900 dark:text-gray-100">{t('payment.success.features.riskTitle')}</h3>
                            <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">{t('payment.success.features.riskDesc')}</p>
                        </div>
                        <div className="p-6 bg-gray-50 dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700">
                            <div className="text-yellow-600 dark:text-yellow-400 mb-3"><BoltIcon fontSize="large" /></div>
                            <h3 className="font-bold text-gray-900 dark:text-gray-100">{t('payment.success.features.alertsTitle')}</h3>
                            <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">{t('payment.success.features.alertsDesc')}</p>
                        </div>
                    </div>

                    <button type="button"
                        onClick={handleGoHome}
                        className="w-full md:w-auto px-10 py-4 bg-indigo-600 hover:bg-indigo-700 text-white font-bold rounded-xl shadow-lg hover:shadow-indigo-200 transform hover:-translate-y-0.5 transition-all duration-200 text-lg"
                    >
                        {t('payment.success.startTradingButton')}
                    </button>
                </div>
            </div>
        </div>
    );
}

export default PaymentSuccessPage;
