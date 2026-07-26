import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ResetPasswordPage from './ResetPasswordPage';

const useResetPasswordFormMock = vi.fn();

vi.mock('./hooks/useResetPasswordForm', () => ({ useResetPasswordForm: () => useResetPasswordFormMock() }));

function baseHookValue(overrides: Partial<ReturnType<typeof useResetPasswordFormMock>> = {}) {
    return {
        passwordRef: { current: null },
        confirmPasswordRef: { current: null },
        errors: { password: '', server: '' },
        loading: false,
        handlePasswordInput: vi.fn(),
        handleSubmit: vi.fn((e: React.FormEvent) => e.preventDefault()),
        ...overrides,
    };
}

describe('ResetPasswordPage', () => {
    beforeEach(() => {
        useResetPasswordFormMock.mockReturnValue(baseHookValue());
    });

    it('renders the form', () => {
        render(<ResetPasswordPage />);

        expect(screen.getByText('resetPassword.title')).toBeInTheDocument();
        expect(screen.getByLabelText('resetPassword.newPasswordLabel')).toBeInTheDocument();
        expect(screen.getByLabelText('resetPassword.confirmPasswordLabel')).toBeInTheDocument();
    });

    it('submits via handleSubmit', async () => {
        const handleSubmit = vi.fn((e: React.FormEvent) => e.preventDefault());
        useResetPasswordFormMock.mockReturnValue(baseHookValue({ handleSubmit }));
        render(<ResetPasswordPage />);
        await userEvent.type(screen.getByLabelText('resetPassword.newPasswordLabel'), 'Str0ng!Passw0rd');
        await userEvent.type(screen.getByLabelText('resetPassword.confirmPasswordLabel'), 'Str0ng!Passw0rd');

        await userEvent.click(screen.getByRole('button', { name: 'resetPassword.submitButton' }));

        expect(handleSubmit).toHaveBeenCalledOnce();
    });

    it('calls handlePasswordInput as the user types', async () => {
        const handlePasswordInput = vi.fn();
        useResetPasswordFormMock.mockReturnValue(baseHookValue({ handlePasswordInput }));
        render(<ResetPasswordPage />);

        await userEvent.type(screen.getByLabelText('resetPassword.newPasswordLabel'), 'x');

        expect(handlePasswordInput).toHaveBeenCalled();
    });

    it('shows the password error', () => {
        useResetPasswordFormMock.mockReturnValue(baseHookValue({ errors: { password: 'Too weak', server: '' } }));
        render(<ResetPasswordPage />);

        expect(screen.getByText('Too weak')).toBeInTheDocument();
    });

    it('shows the server error', () => {
        useResetPasswordFormMock.mockReturnValue(baseHookValue({ errors: { password: '', server: 'Token expired' } }));
        render(<ResetPasswordPage />);

        expect(screen.getByText('Token expired')).toBeInTheDocument();
    });

    it('disables the submit button while loading', () => {
        useResetPasswordFormMock.mockReturnValue(baseHookValue({ loading: true }));
        render(<ResetPasswordPage />);

        expect(screen.getByRole('button', { name: 'resetPassword.submitting' })).toBeDisabled();
    });
});
