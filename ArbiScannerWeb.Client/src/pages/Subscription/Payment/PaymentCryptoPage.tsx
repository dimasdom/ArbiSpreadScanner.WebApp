import { CircularProgress, Dialog, DialogContent, DialogTitle } from '@mui/material';
import CancelIcon from '@mui/icons-material/Cancel';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import { useAuthRedirect } from '../../../hooks/useAuthRedirect';
import { usePaymentCrypto } from './hooks/usePaymentCrypto';

function PaymentCryptoPage() {
    useAuthRedirect();
    const { subscriptionDetails, isLoading, error, paymentUrl, showThankYouModal, handleCloseThankYouModal, handleCancel } = usePaymentCrypto();

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

    const { type, durationInDays, price } = subscriptionDetails;

    return (
        <div className="max-w-3xl mx-auto mt-12 px-4 sm:px-6 lg:px-8 pb-20">
            <div className="bg-white rounded-3xl shadow-xl overflow-hidden border border-gray-100">
                <div className="p-8 text-center border-b border-gray-100 bg-gray-50">
                    <h1 className="text-3xl font-extrabold text-gray-900 mb-2">{type} Subscription</h1>
                    <div className="flex justify-center items-baseline gap-2 mt-4">
                        <span className="text-5xl font-extrabold text-indigo-600">${price}</span>
                        <span className="text-xl text-gray-500 font-medium">/ {durationInDays} days</span>
                    </div>
                </div>

                <div className="p-12 bg-white flex flex-col items-center justify-center min-h-[300px]">
                    <div className="text-center space-y-6 max-w-md">
                        <div>
                            <h3 className="text-xl font-bold text-gray-800 mb-2">OxaPay Secure Payment</h3>
                            <p className="text-gray-500 mx-auto">
                                Use the link below to complete your payment.
                            </p>
                        </div>

                        {paymentUrl ? (
                            <a
                                href={paymentUrl}
                                target="_blank"
                                rel="noopener noreferrer"
                                className="w-full inline-flex items-center justify-center gap-2 py-4 px-6 rounded-xl bg-indigo-600 text-white font-bold text-lg hover:bg-indigo-700 transition-all duration-200 shadow-md"
                            >
                                Pay with OxaPay
                            </a>
                        ) : (
                            <div className="w-full rounded-xl border border-gray-200 bg-gray-50 px-6 py-4 text-gray-500 text-sm">
                                Payment link is being generated. Please wait a moment.
                            </div>
                        )}
                    </div>
                </div>

                <div className="p-8 bg-gray-50 border-t border-gray-100">
                    <button 
                        onClick={handleCancel}
                        className="w-full flex items-center justify-center gap-2 py-4 px-6 rounded-xl border border-gray-300 bg-white text-gray-700 font-bold text-lg hover:bg-gray-50 hover:border-gray-400 transition-all duration-200 shadow-sm"
                    >
                        <CancelIcon />
                        Cancel Payment
                    </button>
                </div>
            </div>
            
            <p className="text-center text-gray-400 text-sm mt-8">
                Payments are processed securely throughout Cryptomus.
            </p>

            {/* Thank You Modal */}
            <Dialog 
                open={showThankYouModal} 
                onClose={handleCloseThankYouModal}
                maxWidth="sm"
                fullWidth
            >
                <DialogTitle className="text-center bg-green-50 border-b-2 border-green-200">
                    <div className="flex justify-center mb-2">
                        <CheckCircleIcon sx={{ fontSize: 60, color: '#10b981' }} />
                    </div>
                </DialogTitle>
                <DialogContent className="text-center py-8">
                    <h2 className="text-3xl font-bold text-gray-900 mb-4">
                        Thank You for Your Purchase!
                    </h2>
                    <p className="text-lg text-gray-600 mb-4">
                        Your subscription has been activated successfully.
                    </p>
                    <p className="text-sm text-gray-500 mb-8">
                        You can now enjoy all the features of your subscription.
                    </p>
                    <button 
                        onClick={handleCloseThankYouModal}
                        className="w-full py-3 px-6 rounded-xl bg-green-600 text-white font-bold text-lg hover:bg-green-700 transition-all duration-200 shadow-md"
                    >
                        Continue
                    </button>
                </DialogContent>
            </Dialog>
        </div>
    );
}

export default PaymentCryptoPage;