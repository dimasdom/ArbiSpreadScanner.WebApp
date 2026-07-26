import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import LoginPage from './LoginPage';

const useLoginFormMock = vi.fn();

vi.mock('./hooks/useLoginForm', () => ({ useLoginForm: () => useLoginFormMock() }));

function baseHookValue(overrides: Partial<ReturnType<typeof useLoginFormMock>> = {}) {
    return {
        emailRef: { current: null },
        passwordRef: { current: null },
        errors: { email: '', password: '' },
        loading: false,
        loginError: null,
        clearFieldError: vi.fn(),
        handleSubmit: vi.fn((e: React.FormEvent) => e.preventDefault()),
        ...overrides,
    };
}

describe('LoginPage', () => {
    beforeEach(() => {
        useLoginFormMock.mockReturnValue(baseHookValue());
    });

    it('renders the login form', () => {
        render(<LoginPage />, { wrapper: MemoryRouter });

        expect(screen.getByText('login.title')).toBeInTheDocument();
        expect(screen.getByLabelText('login.emailLabel')).toBeInTheDocument();
        expect(screen.getByLabelText('login.passwordLabel')).toBeInTheDocument();
    });

    it('submits the form via handleSubmit', async () => {
        const handleSubmit = vi.fn((e: React.FormEvent) => e.preventDefault());
        useLoginFormMock.mockReturnValue(baseHookValue({ handleSubmit }));
        render(<LoginPage />, { wrapper: MemoryRouter });
        await userEvent.type(screen.getByLabelText('login.emailLabel'), 'a@b.com');
        await userEvent.type(screen.getByLabelText('login.passwordLabel'), 'password123456');

        await userEvent.click(screen.getByRole('button', { name: 'login.submitButton' }));

        expect(handleSubmit).toHaveBeenCalledOnce();
    });

    it('clears the field error as the user types', async () => {
        const clearFieldError = vi.fn();
        useLoginFormMock.mockReturnValue(baseHookValue({ clearFieldError }));
        render(<LoginPage />, { wrapper: MemoryRouter });

        await userEvent.type(screen.getByLabelText('login.emailLabel'), 'a');

        expect(clearFieldError).toHaveBeenCalledWith('email');
    });

    it('shows field errors', () => {
        useLoginFormMock.mockReturnValue(baseHookValue({ errors: { email: 'Bad email', password: 'Too short' } }));
        render(<LoginPage />, { wrapper: MemoryRouter });

        expect(screen.getByText('Bad email')).toBeInTheDocument();
        expect(screen.getByText('Too short')).toBeInTheDocument();
    });

    it('disables the submit button and shows a loading label while submitting', () => {
        useLoginFormMock.mockReturnValue(baseHookValue({ loading: true }));
        render(<LoginPage />, { wrapper: MemoryRouter });

        expect(screen.getByRole('button', { name: 'login.submitting' })).toBeDisabled();
    });

    it('shows the login error banner', () => {
        useLoginFormMock.mockReturnValue(baseHookValue({ loginError: 'Invalid credentials' }));
        render(<LoginPage />, { wrapper: MemoryRouter });

        expect(screen.getByText('Invalid credentials')).toBeInTheDocument();
    });
});
