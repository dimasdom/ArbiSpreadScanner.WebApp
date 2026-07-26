import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ConfirmEmailPage from './ConfirmEmailPage';

const useConfirmEmailFormMock = vi.fn();

vi.mock('./hooks/useConfirmEmailForm', () => ({ useConfirmEmailForm: () => useConfirmEmailFormMock() }));

function baseHookValue(overrides: Partial<ReturnType<typeof useConfirmEmailFormMock>> = {}) {
    return {
        tokenRef: { current: null },
        errors: { token: '', server: '' },
        loading: false,
        resending: false,
        handleTokenInput: vi.fn(),
        handleSubmit: vi.fn((e: React.FormEvent) => e.preventDefault()),
        handleResendCode: vi.fn(),
        ...overrides,
    };
}

describe('ConfirmEmailPage', () => {
    beforeEach(() => {
        useConfirmEmailFormMock.mockReturnValue(baseHookValue());
    });

    it('renders the confirmation form', () => {
        render(<ConfirmEmailPage />);

        expect(screen.getByText('confirmEmail.title')).toBeInTheDocument();
        expect(screen.getByLabelText('confirmEmail.tokenLabel')).toBeInTheDocument();
    });

    it('submits via handleSubmit', async () => {
        const handleSubmit = vi.fn((e: React.FormEvent) => e.preventDefault());
        useConfirmEmailFormMock.mockReturnValue(baseHookValue({ handleSubmit }));
        render(<ConfirmEmailPage />);
        await userEvent.type(screen.getByLabelText('confirmEmail.tokenLabel'), '123456');

        await userEvent.click(screen.getByRole('button', { name: 'confirmEmail.submitButton' }));

        expect(handleSubmit).toHaveBeenCalledOnce();
    });

    it('calls handleTokenInput as the user types', async () => {
        const handleTokenInput = vi.fn();
        useConfirmEmailFormMock.mockReturnValue(baseHookValue({ handleTokenInput }));
        render(<ConfirmEmailPage />);

        await userEvent.type(screen.getByLabelText('confirmEmail.tokenLabel'), '1');

        expect(handleTokenInput).toHaveBeenCalled();
    });

    it('calls handleResendCode when the resend button is clicked', async () => {
        const handleResendCode = vi.fn();
        useConfirmEmailFormMock.mockReturnValue(baseHookValue({ handleResendCode }));
        render(<ConfirmEmailPage />);

        await userEvent.click(screen.getByRole('button', { name: 'confirmEmail.resendButton' }));

        expect(handleResendCode).toHaveBeenCalledOnce();
    });

    it('shows the token error', () => {
        useConfirmEmailFormMock.mockReturnValue(baseHookValue({ errors: { token: 'Token required', server: '' } }));
        render(<ConfirmEmailPage />);

        expect(screen.getByText('Token required')).toBeInTheDocument();
    });

    it('shows the server error', () => {
        useConfirmEmailFormMock.mockReturnValue(baseHookValue({ errors: { token: '', server: 'Confirmation failed' } }));
        render(<ConfirmEmailPage />);

        expect(screen.getByText('Confirmation failed')).toBeInTheDocument();
    });

    it('disables both buttons appropriately while loading/resending', () => {
        useConfirmEmailFormMock.mockReturnValue(baseHookValue({ loading: true, resending: true }));
        render(<ConfirmEmailPage />);

        expect(screen.getByRole('button', { name: 'confirmEmail.confirming' })).toBeDisabled();
        expect(screen.getByRole('button', { name: 'confirmEmail.resending' })).toBeDisabled();
    });
});
