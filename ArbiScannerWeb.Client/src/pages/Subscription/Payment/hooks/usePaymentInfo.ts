import { useSearchParams } from 'react-router';
import { useCreatePaymentMutation, useGetSubscriptionDetailsQuery } from '../../../../store/services/subscription';
import { logger } from '../../../../services/loggerService';
import { useLocalizedNavigate } from '../../../../i18n/routing';

export function usePaymentInfo() {
    const [searchParams] = useSearchParams();
    const navigate = useLocalizedNavigate();

    const subscriptionId = Number(searchParams.get('id')) || 0;
    const { data, isLoading, error } = useGetSubscriptionDetailsQuery(subscriptionId);
    const [createPayment] = useCreatePaymentMutation();

    const handlePayClick = async () => {
        try {
            await createPayment(subscriptionId).unwrap();
        } catch (err) {
            const details = err instanceof Error ? err.message : 'Unknown create payment error';
            logger.error('Create payment failed', 'PaymentInfoPage', details);
        }
        navigate(`/payment/pay?id=${subscriptionId}`);
    };

    return {
        subscriptionDetails: data?.value,
        isLoading,
        error,
        handlePayClick,
    };
}
