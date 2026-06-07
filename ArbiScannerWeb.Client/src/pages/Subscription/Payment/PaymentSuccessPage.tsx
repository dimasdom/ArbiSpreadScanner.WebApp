import { useNavigate } from "react-router";
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import AutoGraphIcon from '@mui/icons-material/AutoGraph';
import SecurityIcon from '@mui/icons-material/Security';
import BoltIcon from '@mui/icons-material/Bolt';

function PaymentSuccessPage() {
    const navigate = useNavigate();

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
                        Thank You for Your Purchase!
                    </h1>
                    <p className="text-xl text-green-700 dark:text-green-400 font-medium">
                        Your subscription is now active.
                    </p>
                </div>

                <div className="p-12 space-y-8">
                    <p className="text-gray-600 dark:text-gray-400 text-lg max-w-xl mx-auto leading-relaxed">
                        You've successfully unlocked user access.
                        Get ready to elevate your trading game with our powerful tools.
                    </p>

                    <div className="grid grid-cols-1 md:grid-cols-3 gap-6 text-left">
                        <div className="p-6 bg-gray-50 dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700">
                            <div className="text-indigo-600 dark:text-indigo-400 mb-3"><AutoGraphIcon fontSize="large" /></div>
                            <h3 className="font-bold text-gray-900 dark:text-gray-100">Real-Time Data</h3>
                            <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">Live arbitrage opportunities.</p>
                        </div>
                        <div className="p-6 bg-gray-50 dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700">
                            <div className="text-blue-600 dark:text-blue-400 mb-3"><SecurityIcon fontSize="large" /></div>
                            <h3 className="font-bold text-gray-900 dark:text-gray-100">Risk Tools</h3>
                            <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">Advanced position sizing.</p>
                        </div>
                        <div className="p-6 bg-gray-50 dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700">
                            <div className="text-yellow-600 dark:text-yellow-400 mb-3"><BoltIcon fontSize="large" /></div>
                            <h3 className="font-bold text-gray-900 dark:text-gray-100">Instant Alerts</h3>
                            <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">Never miss a trade.</p>
                        </div>
                    </div>

                    <button
                        onClick={handleGoHome}
                        className="w-full md:w-auto px-10 py-4 bg-indigo-600 hover:bg-indigo-700 text-white font-bold rounded-xl shadow-lg hover:shadow-indigo-200 transform hover:-translate-y-0.5 transition-all duration-200 text-lg"
                    >
                        Start Trading Now
                    </button>
                </div>
            </div>
        </div>
    );
}

export default PaymentSuccessPage;
