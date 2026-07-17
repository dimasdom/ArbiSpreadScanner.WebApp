import { useGetUserActiveSubscriptionsQuery } from "../../store/services/subscription";
import VerifiedUserIcon from '@mui/icons-material/VerifiedUser';
import HighlightOffIcon from '@mui/icons-material/HighlightOff';
import ErrorState from '../../components/ErrorState';

function UserSubscriptionElement() {
    const { data, isLoading, isError } = useGetUserActiveSubscriptionsQuery();

    if (isLoading) {
        return <div className="w-full h-64 bg-gray-50 rounded-2xl animate-pulse"></div>;
    }

    if (isError) {
        return <ErrorState message="Failed to load your subscription status. Please try again later." />;
    }

    if (data?.value) {
        return (
            <div className="w-full bg-green-50 rounded-2xl p-8 hover:shadow-lg transition-shadow duration-300 border border-green-100 text-center">
                <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-6 text-green-600">
                    <VerifiedUserIcon fontSize="large" />
                </div>
                <h3 className="text-2xl font-bold text-gray-900 mb-4">
                    {data.value.subscription?.type} Plan Active
                </h3>
                <p className="text-gray-600 text-lg">
                    Your subscription is active until <span className="font-semibold text-green-700">{new Date(data.value.endDate).toLocaleDateString()}</span>.
                </p>
            </div>
        );
    } else {
        return (
            <div className="w-full bg-gray-50 rounded-2xl p-8 hover:shadow-lg transition-shadow duration-300 border border-gray-100 text-center">
                <div className="w-16 h-16 bg-gray-200 rounded-full flex items-center justify-center mx-auto mb-6 text-gray-500">
                    <HighlightOffIcon fontSize="large" />
                </div>
                <h3 className="text-2xl font-bold text-gray-900 mb-4">No Active Subscription</h3>
                <p className="text-gray-600">
                    You do not have an active subscription. Select a plan below to get started.
                </p>
            </div>
        );
    }
}

export default UserSubscriptionElement;