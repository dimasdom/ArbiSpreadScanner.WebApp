import { useEffect, useState } from 'react';
import { useSearchParams, useNavigate } from 'react-router';
import {
    subscriptionsAPI,
    useCancelPaymentMutation,
    useGetPaymentStatusQuery,
    useGetSubscriptionDetailsQuery,
    useGetUserActivePaymentsQuery,
} from '../../../../store/services/subscription';
import { PaymentStatus } from '../../../../types/accountType';
import { useAppDispatch } from '../../../../hooks';
import { logger } from '../../../../services/loggerService';

export function usePaymentCrypto() {
    const [searchParams] = useSearchParams();
    const navigate = useNavigate();
    const dispatch = useAppDispatch();

    const subscriptionId = Number(searchParams.get('id')) || 0;
    const [showThankYouModal, setShowThankYouModal] = useState(false);
    const [paymentId, setPaymentId] = useState<number | null>(null);

    const { data, isLoading, error } = useGetSubscriptionDetailsQuery(subscriptionId);
    const { data: paymentStatusData } = useGetPaymentStatusQuery(paymentId!, {
        pollingInterval: 15000,
        skip: showThankYouModal || !paymentId,
    });
    const { data: paymentStatusDataOld } = useGetUserActivePaymentsQuery();
    const [cancelPayment] = useCancelPaymentMutation();

    useEffect(() => {
        setPaymentId(paymentStatusDataOld?.value?.id || null);
    }, [paymentStatusDataOld]);

    useEffect(() => {
        if (paymentStatusData?.value && paymentStatusData.value.payment?.status === PaymentStatus.Completed) {
            dispatch(subscriptionsAPI.util.invalidateTags(['ActiveSubscription']));
            setShowThankYouModal(true);
        }
    }, [paymentStatusData, dispatch]);

    const handleCloseThankYouModal = () => {
        setShowThankYouModal(false);
        navigate('/account');
    };

    const handleCancel = async () => {
        try {
            const result = await cancelPayment(paymentStatusData?.value.id || 0).unwrap();
            if (result.isSuccess) {
                navigate(-1);
            }
        } catch (err) {
            const details = err instanceof Error ? err.message : 'Unknown cancel payment error';
            logger.error('Cancel payment failed', 'PaymentCryptoPage', details);
        }
    };

    const paymentUrl = paymentStatusData?.value.payment?.paymentUrl;

    return {
        subscriptionDetails: data?.value,
        isLoading,
        error,
        paymentUrl,
        showThankYouModal,
        handleCloseThankYouModal,
        handleCancel,
    };
}
