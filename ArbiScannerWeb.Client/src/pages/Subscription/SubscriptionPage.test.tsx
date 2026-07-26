import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import SubscriptionPage from './SubscriptionPage';

const useSubscriptionPageMock = vi.fn();

vi.mock('../../hooks/useSubscriptionPage', () => ({ useSubscriptionPage: () => useSubscriptionPageMock() }));

describe('SubscriptionPage', () => {
    beforeEach(() => {
        useSubscriptionPageMock.mockReturnValue({ subscriptions: [], isLoading: false, error: null });
    });

    it('shows a loading spinner while data is loading', () => {
        useSubscriptionPageMock.mockReturnValue({ subscriptions: undefined, isLoading: true, error: null });

        render(<SubscriptionPage />, { wrapper: MemoryRouter });

        expect(document.querySelector('.MuiCircularProgress-root')).not.toBeNull();
    });

    it('shows an error state when the query fails', () => {
        useSubscriptionPageMock.mockReturnValue({ subscriptions: undefined, isLoading: false, error: 'boom' });

        render(<SubscriptionPage />, { wrapper: MemoryRouter });

        expect(screen.getByText('subscriptionPage.loadError')).toBeInTheDocument();
    });

    it('renders a card for each subscription plan', () => {
        useSubscriptionPageMock.mockReturnValue({
            subscriptions: [
                { id: 1, type: 'Basic', price: 9.99, durationInDays: 30 },
                { id: 2, type: 'Pro', price: 19.99, durationInDays: 30 },
            ],
            isLoading: false,
            error: null,
        });

        render(<SubscriptionPage />, { wrapper: MemoryRouter });

        expect(screen.getByText('Basic')).toBeInTheDocument();
        expect(screen.getByText('Pro')).toBeInTheDocument();
    });
});
