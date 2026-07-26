import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import ForgotPasswordPage from './ForgotPasswordPage';

const useForgotPasswordFormMock = vi.fn();

vi.mock('./hooks/useForgotPasswordForm', () => ({ useForgotPasswordForm: () => useForgotPasswordFormMock() }));

function baseHookValue(overrides: Partial<ReturnType<typeof useForgotPasswordFormMock>> = {}) {
    return {
        emailRef: { current: null },
        errors: { email: '', server: '' },
        loading: false,
        isSuccess: false,
        submittedEmail: '',
        handleEmailInput: vi.fn(),
        handleSubmit: vi.fn((e: React.FormEvent) => e.preventDefault()),
        ...overrides,
    };
}

describe('ForgotPasswordPage', () => {
    beforeEach(() => {
        useForgotPasswordFormMock.mockReturnValue(baseHookValue());
    });

    it('renders the form', () => {
        render(<ForgotPasswordPage />, { wrapper: MemoryRouter });

        expect(screen.getByText('forgotPassword.title')).toBeInTheDocument();
    });

    it('submits via handleSubmit', async () => {
        const handleSubmit = vi.fn((e: React.FormEvent) => e.preventDefault());
        useForgotPasswordFormMock.mockReturnValue(baseHookValue({ handleSubmit }));
        render(<ForgotPasswordPage />, { wrapper: MemoryRouter });
        await userEvent.type(screen.getByLabelText('forgotPassword.emailLabel'), 'a@b.com');

        await userEvent.click(screen.getByRole('button', { name: 'Send code' }));

        expect(handleSubmit).toHaveBeenCalledOnce();
    });

    it('shows the email error', () => {
        useForgotPasswordFormMock.mockReturnValue(baseHookValue({ errors: { email: 'Bad email', server: '' } }));
        render(<ForgotPasswordPage />, { wrapper: MemoryRouter });

        expect(screen.getByText('Bad email')).toBeInTheDocument();
    });

    it('shows the server error', () => {
        useForgotPasswordFormMock.mockReturnValue(baseHookValue({ errors: { email: '', server: 'Send failed' } }));
        render(<ForgotPasswordPage />, { wrapper: MemoryRouter });

        expect(screen.getByText('Send failed')).toBeInTheDocument();
    });

    it('renders a success message instead of the form once submitted', () => {
        useForgotPasswordFormMock.mockReturnValue(baseHookValue({ isSuccess: true, submittedEmail: 'a@b.com' }));
        render(<ForgotPasswordPage />, { wrapper: MemoryRouter });

        expect(screen.getByText('forgotPassword.successTitle')).toBeInTheDocument();
        expect(screen.queryByLabelText('forgotPassword.emailLabel')).not.toBeInTheDocument();
    });
});
