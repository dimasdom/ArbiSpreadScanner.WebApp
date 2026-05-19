import { CircularProgress } from '@mui/material';
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome';
import { useAuthRedirect } from '../../../hooks/useAuthRedirect';
import { usePaymentInfo } from './hooks/usePaymentInfo';

function PaymentInfoPage() {
    useAuthRedirect();
    const { subscriptionDetails, isLoading, error, handlePayClick } = usePaymentInfo();

    if (isLoading) {
        return (
            <div className="flex h-[60vh] items-center justify-center">
                <CircularProgress size={60} thickness={4} className="text-indigo-600" />
            </div>
        );
    }

    if (error || !subscriptionDetails) {
        return (
            <div className="flex h-[60vh] items-center justify-center">
                <div className="text-xl text-red-500 font-semibold bg-red-50 px-6 py-4 rounded-xl border border-red-200">
                    Failed to load subscription details.
                </div>
            </div>
        );
    }

    const { type, price, durationInDays } = subscriptionDetails;

    return (
        <div className="max-w-3xl mx-auto mt-12 px-4 sm:px-6 lg:px-8 pb-20">
            <div className="bg-white rounded-3xl shadow-xl overflow-hidden border border-gray-100">
                <div className="bg-gradient-to-r from-indigo-900 via-purple-800 to-indigo-900 py-12 text-center text-white relative">
                    <AutoAwesomeIcon className="text-purple-300 absolute top-4 right-4 opacity-50" fontSize="large" />
                    <h1 className="text-4xl font-extrabold tracking-tight mb-2">
                        {type} Plan
                    </h1>
                    <p className="text-indigo-100 text-lg opacity-90">Confirm your subscription details</p>
                </div>

                <div className="p-8 md:p-12 space-y-8">
                    <div className="space-y-4">
                        <div className="flex justify-between items-center py-4 border-b border-gray-100">
                            <span className="text-gray-500 text-lg">Billing Cycle</span>
                            <span className="text-gray-900 font-semibold text-lg">{durationInDays} Days</span>
                        </div>
                        <div className="flex justify-between items-center py-4 border-b border-gray-100">
                            <span className="text-gray-500 text-lg">Price</span>
                            <span className="text-3xl font-bold text-indigo-600">${price}</span>
                        </div>
                        <div className="bg-indigo-50 rounded-xl p-4 text-sm text-indigo-700 mt-4">
                            Includes access to real-time analytics, risk management tools, and unlimited alerts.
                        </div>
                    </div>

                    <button
                        onClick={handlePayClick}
                        className="w-full py-4 px-6 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xl font-bold transition-all duration-200 shadow-lg hover:shadow-indigo-200 transform hover:-translate-y-0.5"
                    >
                        Proceed to Payment
                    </button>
                </div>
            </div>
        </div>
    );
}

export default PaymentInfoPage;