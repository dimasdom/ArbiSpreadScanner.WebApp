import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';

const rawBaseQueryMock = vi.fn();

vi.mock('@reduxjs/toolkit/query/react', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@reduxjs/toolkit/query/react')>();
    return { ...actual, fetchBaseQuery: () => rawBaseQueryMock };
});

async function renderWithStore() {
    const { subscriptionsAPI } = await import('../../store/services/subscription');
    const { default: UserSubscriptionElement } = await import('./UserSubscriptionElement');
    const store = configureStore({
        reducer: { [subscriptionsAPI.reducerPath]: subscriptionsAPI.reducer },
        middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(subscriptionsAPI.middleware),
    });
    return render(
        <Provider store={store}>
            <UserSubscriptionElement />
        </Provider>,
    );
}

describe('UserSubscriptionElement', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
    });

    it('shows a loading placeholder while the query is in flight', async () => {
        rawBaseQueryMock.mockReturnValue(new Promise(() => {}));
        const { container } = await renderWithStore();

        expect(container.querySelector('.animate-pulse')).not.toBeNull();
    });

    it('shows an error state when the query fails', async () => {
        rawBaseQueryMock.mockResolvedValue({ error: { status: 500, data: 'boom' } });

        await renderWithStore();

        expect(await screen.findByText('userSubscription.loadError')).toBeInTheDocument();
    });

    it('shows the active plan when a subscription is present', async () => {
        rawBaseQueryMock.mockResolvedValue({
            data: {
                isSuccess: true,
                value: { endDate: '2026-01-01T00:00:00Z', subscription: { type: 'Pro' } },
            },
        });

        await renderWithStore();

        expect(await screen.findByText('userSubscription.activePlanHeading')).toBeInTheDocument();
    });

    it('shows the no-active-subscription state when there is no value', async () => {
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true, value: null } });

        await renderWithStore();

        expect(await screen.findByText('userSubscription.noActiveHeading')).toBeInTheDocument();
    });
});
