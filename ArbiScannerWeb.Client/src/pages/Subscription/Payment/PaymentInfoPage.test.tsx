import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import PaymentInfoPage from './PaymentInfoPage';

const useAuthRedirectMock = vi.fn();
const usePaymentInfoMock = vi.fn();

vi.mock('../../../hooks/useAuthRedirect', () => ({ useAuthRedirect: () => useAuthRedirectMock() }));
vi.mock('./hooks/usePaymentInfo', () => ({ usePaymentInfo: () => usePaymentInfoMock() }));

function baseHookValue(overrides: Partial<ReturnType<typeof usePaymentInfoMock>> = {}) {
    return {
        subscriptionDetails: { type: 'Pro', price: 19.99, durationInDays: 30 },
        isLoading: false,
        error: null,
        handlePayClick: vi.fn(),
        ...overrides,
    };
}

describe('PaymentInfoPage', () => {
    beforeEach(() => {
        usePaymentInfoMock.mockReturnValue(baseHookValue());
    });

    it('shows a loading spinner while data is loading', () => {
        usePaymentInfoMock.mockReturnValue(baseHookValue({ isLoading: true, subscriptionDetails: undefined }));

        render(<PaymentInfoPage />);

        expect(document.querySelector('.MuiCircularProgress-root')).not.toBeNull();
    });

    it('shows an error state when the query fails', () => {
        usePaymentInfoMock.mockReturnValue(baseHookValue({ error: 'boom', subscriptionDetails: undefined }));

        render(<PaymentInfoPage />);

        expect(screen.getByText('payment.info.loadError')).toBeInTheDocument();
    });

    it('renders the plan details', () => {
        render(<PaymentInfoPage />);

        expect(screen.getByText('$19.99')).toBeInTheDocument();
    });

    it('calls handlePayClick when the proceed button is clicked', async () => {
        const handlePayClick = vi.fn();
        usePaymentInfoMock.mockReturnValue(baseHookValue({ handlePayClick }));

        render(<PaymentInfoPage />);
        await userEvent.click(screen.getByRole('button', { name: 'payment.info.proceedButton' }));

        expect(handlePayClick).toHaveBeenCalledOnce();
    });
});
