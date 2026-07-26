import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import PaymentCryptoPage from './PaymentCryptoPage';

const useAuthRedirectMock = vi.fn();
const usePaymentCryptoMock = vi.fn();

vi.mock('../../../hooks/useAuthRedirect', () => ({ useAuthRedirect: () => useAuthRedirectMock() }));
vi.mock('./hooks/usePaymentCrypto', () => ({ usePaymentCrypto: () => usePaymentCryptoMock() }));

function baseHookValue(overrides: Partial<ReturnType<typeof usePaymentCryptoMock>> = {}) {
    return {
        subscriptionDetails: { type: 'Pro', price: 19.99, durationInDays: 30 },
        isLoading: false,
        error: null,
        paymentUrl: 'https://pay.example/x',
        showThankYouModal: false,
        handleCloseThankYouModal: vi.fn(),
        handleCancel: vi.fn(),
        ...overrides,
    };
}

describe('PaymentCryptoPage', () => {
    beforeEach(() => {
        usePaymentCryptoMock.mockReturnValue(baseHookValue());
    });

    it('shows a loading spinner while data is loading', () => {
        usePaymentCryptoMock.mockReturnValue(baseHookValue({ isLoading: true, subscriptionDetails: undefined }));

        render(<PaymentCryptoPage />);

        expect(document.querySelector('.MuiCircularProgress-root')).not.toBeNull();
    });

    it('shows an error state when the query fails', () => {
        usePaymentCryptoMock.mockReturnValue(baseHookValue({ error: 'boom', subscriptionDetails: undefined }));

        render(<PaymentCryptoPage />);

        expect(screen.getByText('payment.crypto.loadError')).toBeInTheDocument();
    });

    it('renders the plan details and a payment link once available', () => {
        render(<PaymentCryptoPage />);

        expect(screen.getByText('$19.99')).toBeInTheDocument();
        expect(screen.getByRole('link', { name: 'payment.crypto.payButton' })).toHaveAttribute('href', 'https://pay.example/x');
    });

    it('shows a generating-link placeholder when no payment URL is available yet', () => {
        usePaymentCryptoMock.mockReturnValue(baseHookValue({ paymentUrl: undefined }));

        render(<PaymentCryptoPage />);

        expect(screen.getByText('payment.crypto.generatingLink')).toBeInTheDocument();
        expect(screen.queryByRole('link', { name: 'payment.crypto.payButton' })).not.toBeInTheDocument();
    });

    it('calls handleCancel when the cancel button is clicked', async () => {
        const handleCancel = vi.fn();
        usePaymentCryptoMock.mockReturnValue(baseHookValue({ handleCancel }));

        render(<PaymentCryptoPage />);
        await userEvent.click(screen.getByRole('button', { name: /payment.crypto.cancelButton/ }));

        expect(handleCancel).toHaveBeenCalledOnce();
    });

    it('shows the thank-you modal and closes it', async () => {
        const handleCloseThankYouModal = vi.fn();
        usePaymentCryptoMock.mockReturnValue(baseHookValue({ showThankYouModal: true, handleCloseThankYouModal }));

        render(<PaymentCryptoPage />);
        expect(screen.getByText('payment.crypto.thankYouHeading')).toBeInTheDocument();

        await userEvent.click(screen.getByRole('button', { name: 'payment.crypto.continueButton' }));

        expect(handleCloseThankYouModal).toHaveBeenCalledOnce();
    });
});
