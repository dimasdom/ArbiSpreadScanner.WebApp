import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import RegisterPage from './RegisterPage';

const useRegisterFormMock = vi.fn();

vi.mock('./hooks/useRegisterForm', () => ({ useRegisterForm: () => useRegisterFormMock() }));

function baseHookValue(overrides: Partial<ReturnType<typeof useRegisterFormMock>> = {}) {
    return {
        emailRef: { current: null },
        confirmEmailRef: { current: null },
        passwordRef: { current: null },
        confirmPasswordRef: { current: null },
        errors: { email: '', confirmEmail: '', password: '', confirmPassword: '', server: '' },
        loading: false,
        loginError: null,
        showEulaModal: false,
        clearFieldError: vi.fn(),
        handleSubmit: vi.fn((e: React.FormEvent) => e.preventDefault()),
        handleEulaAgree: vi.fn(),
        handleEulaCancel: vi.fn(),
        ...overrides,
    };
}

async function fillAllFields() {
    await userEvent.type(screen.getByLabelText('register.emailLabel'), 'a@b.com');
    await userEvent.type(screen.getByLabelText('register.confirmEmailLabel'), 'a@b.com');
    await userEvent.type(screen.getByLabelText('register.passwordLabel'), 'Str0ng!Passw0rd');
    await userEvent.type(screen.getByLabelText('register.confirmPasswordLabel'), 'Str0ng!Passw0rd');
}

describe('RegisterPage', () => {
    beforeEach(() => {
        useRegisterFormMock.mockReturnValue(baseHookValue());
    });

    it('renders all form fields', () => {
        render(<RegisterPage />, { wrapper: MemoryRouter });

        expect(screen.getByText('register.title')).toBeInTheDocument();
        expect(screen.getByLabelText('register.emailLabel')).toBeInTheDocument();
        expect(screen.getByLabelText('register.confirmEmailLabel')).toBeInTheDocument();
        expect(screen.getByLabelText('register.passwordLabel')).toBeInTheDocument();
        expect(screen.getByLabelText('register.confirmPasswordLabel')).toBeInTheDocument();
    });

    it('submits via handleSubmit', async () => {
        const handleSubmit = vi.fn((e: React.FormEvent) => e.preventDefault());
        useRegisterFormMock.mockReturnValue(baseHookValue({ handleSubmit }));
        render(<RegisterPage />, { wrapper: MemoryRouter });
        await fillAllFields();

        await userEvent.click(screen.getByRole('button', { name: 'register.submitButton' }));

        expect(handleSubmit).toHaveBeenCalledOnce();
    });

    it('shows validation errors for each field', () => {
        useRegisterFormMock.mockReturnValue(baseHookValue({
            errors: { email: 'e1', confirmEmail: 'e2', password: 'e3', confirmPassword: 'e4', server: '' },
        }));
        render(<RegisterPage />, { wrapper: MemoryRouter });

        expect(screen.getByText('e1')).toBeInTheDocument();
        expect(screen.getByText('e2')).toBeInTheDocument();
        expect(screen.getByText('e3')).toBeInTheDocument();
        expect(screen.getByText('e4')).toBeInTheDocument();
    });

    it('shows the server error banner', () => {
        useRegisterFormMock.mockReturnValue(baseHookValue({ errors: { email: '', confirmEmail: '', password: '', confirmPassword: '', server: 'Email taken' } }));
        render(<RegisterPage />, { wrapper: MemoryRouter });

        expect(screen.getByText('Email taken')).toBeInTheDocument();
    });

    it('renders the EULA modal when showEulaModal is true and wires agree/cancel', async () => {
        const handleEulaAgree = vi.fn();
        const handleEulaCancel = vi.fn();
        useRegisterFormMock.mockReturnValue(baseHookValue({ showEulaModal: true, handleEulaAgree, handleEulaCancel }));
        render(<RegisterPage />, { wrapper: MemoryRouter });

        await userEvent.click(screen.getByRole('button', { name: 'eula.agree' }));
        expect(handleEulaAgree).toHaveBeenCalledOnce();

        await userEvent.click(screen.getByRole('button', { name: 'actions.cancel' }));
        expect(handleEulaCancel).toHaveBeenCalledOnce();
    });

    it('disables the submit button while loading', () => {
        useRegisterFormMock.mockReturnValue(baseHookValue({ loading: true }));
        render(<RegisterPage />, { wrapper: MemoryRouter });

        expect(screen.getByRole('button', { name: 'register.submitting' })).toBeDisabled();
    });
});
