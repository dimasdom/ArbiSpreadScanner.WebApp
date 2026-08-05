import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import accountReducer from '../../store/slices/accountSlice';
import { createEmptyAccountModel } from '../../types/accountType';
import { createLocalStorageMock } from '../../test/localStorageMock';

const navigateMock = vi.fn();
const useGetUserDataQueryMock = vi.fn();
const useUpdateAccountDetailsMutationMock = vi.fn();
const useCreateTelegramLinkRequestMutationMock = vi.fn();
const useRemoveTelegramLinkMutationMock = vi.fn();
const useGetUserActiveSubscriptionsQueryMock = vi.fn();
const toastSuccessMock = vi.fn();
const toastErrorMock = vi.fn();

vi.mock('../../i18n/routing', async (importOriginal) => {
    const actual = await importOriginal<typeof import('../../i18n/routing')>();
    return { ...actual, useLocalizedNavigate: () => navigateMock };
});
vi.mock('react-hot-toast', () => ({ default: { success: (...a: unknown[]) => toastSuccessMock(...a), error: (...a: unknown[]) => toastErrorMock(...a) } }));
vi.mock('../../store/services/account', () => ({
    useGetUserDataQuery: (...a: unknown[]) => useGetUserDataQueryMock(...a),
    useUpdateAccountDetailsMutation: () => useUpdateAccountDetailsMutationMock(),
    useCreateTelegramLinkRequestMutation: () => useCreateTelegramLinkRequestMutationMock(),
    useRemoveTelegramLinkMutation: () => useRemoveTelegramLinkMutationMock(),
}));
vi.mock('../../store/services/subscription', () => ({
    useGetUserActiveSubscriptionsQuery: (...a: unknown[]) => useGetUserActiveSubscriptionsQueryMock(...a),
}));

function mockTrigger(result: unknown, shouldThrow = false) {
    return vi.fn(() => ({
        unwrap: () => (shouldThrow ? Promise.reject(result) : Promise.resolve(result)),
    }));
}

function buildAccount(overrides: Partial<ReturnType<typeof createEmptyAccountModel>> = {}) {
    return {
        ...createEmptyAccountModel(),
        id: 'u1',
        email: 'a@b.com',
        userSettings: {
            id: 1, userName: null, chatId: 0, spreadSize: 1.5, positionSize: 500,
            futuresSpread: true, fundingSpread: false, spotSpread: false, haveAccess: true,
            active: true, email: null,
            exchanges: [{ id: 1, userAccountId: 'u1', exchangeId: 1, exchange: { id: 1, name: 'Binance' } }],
        },
        ...overrides,
    };
}

function renderAccountPage(accountOverrides: Partial<ReturnType<typeof buildAccount>> = {}, storeOverrides: Record<string, unknown> = {}) {
    const store = configureStore({
        reducer: { account: accountReducer },
        preloadedState: {
            account: {
                account: buildAccount(accountOverrides),
                isLoggedIn: true,
                loading: false,
                error: null,
                sessionChecked: true,
                ...storeOverrides,
            },
        },
    });
    return import('./AccountPage').then(({ default: AccountPage }) =>
        render(
            <Provider store={store}>
                <AccountPage />
            </Provider>,
        ),
    );
}

describe('AccountPage', () => {
    beforeEach(() => {
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
        navigateMock.mockClear();
        toastSuccessMock.mockClear();
        toastErrorMock.mockClear();
        useGetUserDataQueryMock.mockReturnValue({ data: undefined });
        useGetUserActiveSubscriptionsQueryMock.mockReturnValue({ data: undefined });
        useUpdateAccountDetailsMutationMock.mockReturnValue([mockTrigger({ isSuccess: true }), {}]);
        useCreateTelegramLinkRequestMutationMock.mockReturnValue([mockTrigger({ isSuccess: true, value: { id: 'req-1' } }), {}]);
        useRemoveTelegramLinkMutationMock.mockReturnValue([mockTrigger({ isSuccess: true }), {}]);
    });

    it('shows the account email read-only, with a link to manage it in Keycloak', async () => {
        await renderAccountPage();

        expect(screen.getByDisplayValue('a@b.com')).toBeDisabled();
        expect(screen.getByText('accountPage.form.manageInKeycloak')).toHaveAttribute('href', expect.stringContaining('/account'));
    });

    it('pre-fills the form from the current account settings', async () => {
        await renderAccountPage();

        expect(screen.getByRole('button', { name: 'Binance' })).toHaveClass('bg-indigo-600');
    });

    it('shows the no-active-subscription banner and navigates to /subscriptions', async () => {
        await renderAccountPage();

        await userEvent.click(screen.getByRole('button', { name: 'accountPage.subscription.none.viewButton' }));

        expect(navigateMock).toHaveBeenCalledWith('/subscriptions');
    });

    it('shows the active-subscription banner when the user has one', async () => {
        useGetUserActiveSubscriptionsQueryMock.mockReturnValue({
            data: { isSuccess: true, value: { isActive: true, subscription: { type: 'Pro' }, endDate: '2026-01-01T00:00:00Z' } },
        });

        await renderAccountPage();

        expect(screen.getByText('accountPage.subscription.active.title')).toBeInTheDocument();
        expect(screen.getByText('Pro')).toBeInTheDocument();
    });

    it('saves settings and shows a success toast', async () => {
        const trigger = mockTrigger({ isSuccess: true });
        useUpdateAccountDetailsMutationMock.mockReturnValue([trigger, {}]);
        await renderAccountPage();

        await userEvent.click(screen.getByRole('button', { name: 'accountPage.actions.save' }));

        await waitFor(() => expect(toastSuccessMock).toHaveBeenCalledOnce());
        expect(trigger).toHaveBeenCalledWith(expect.objectContaining({ email: 'a@b.com', spreadSize: 1.5, positionSize: 500 }));
    });

    it('shows an error toast when saving fails', async () => {
        useUpdateAccountDetailsMutationMock.mockReturnValue([mockTrigger(new Error('boom'), true), {}]);
        await renderAccountPage();

        await userEvent.click(screen.getByRole('button', { name: 'accountPage.actions.save' }));

        await waitFor(() => expect(toastErrorMock).toHaveBeenCalledOnce());
    });

    it('links Telegram and opens the modal with the request id', async () => {
        await renderAccountPage({ userSettings: { ...buildAccount().userSettings, userName: null, chatId: 0 } });

        await userEvent.click(screen.getByRole('button', { name: 'accountPage.telegram.notConnected.linkButton' }));

        await waitFor(() => expect(screen.getByText('telegramLink.title')).toBeInTheDocument());
    });

    it('shows the connected state and unlinks Telegram', async () => {
        const removeTrigger = mockTrigger({ isSuccess: true });
        useRemoveTelegramLinkMutationMock.mockReturnValue([removeTrigger, {}]);
        await renderAccountPage({ userSettings: { ...buildAccount().userSettings, userName: 'tguser', chatId: 555 } });
        expect(screen.getByText('accountPage.telegram.connected.label')).toBeInTheDocument();

        await userEvent.click(screen.getByTitle('accountPage.telegram.unlinkAria'));

        await waitFor(() => expect(removeTrigger).toHaveBeenCalledOnce());
    });

    it('shows an unlink error when Telegram removal fails', async () => {
        useRemoveTelegramLinkMutationMock.mockReturnValue([
            mockTrigger({ isSuccess: false, errors: [{ message: 'Unlink failed' }] }), {},
        ]);
        await renderAccountPage({ userSettings: { ...buildAccount().userSettings, userName: 'tguser', chatId: 555 } });

        await userEvent.click(screen.getByTitle('accountPage.telegram.unlinkAria'));

        await waitFor(() => expect(screen.getByText('Unlink failed')).toBeInTheDocument());
    });

    it('toggles an exchange chip on and off', async () => {
        await renderAccountPage();
        const bybitButton = screen.getByRole('button', { name: 'Bybit' });
        expect(bybitButton).not.toHaveClass('bg-indigo-600');

        await userEvent.click(bybitButton);
        expect(bybitButton).toHaveClass('bg-indigo-600');

        await userEvent.click(bybitButton);
        expect(bybitButton).not.toHaveClass('bg-indigo-600');
    });

    it('shows the account-level error banner', async () => {
        await renderAccountPage({}, { error: 'Something went wrong' });

        expect(screen.getByText('Something went wrong')).toBeInTheDocument();
    });
});
