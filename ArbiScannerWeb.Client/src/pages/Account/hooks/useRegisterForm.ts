import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { useSelector } from 'react-redux';
import { clearError } from '../../../store/slices/accountSlice';
import { useRegisterMutation } from '../../../store/services/account';
import { useAppDispatch } from '../../../hooks';
import type { IRootStore } from '../../../store/store';
import { validateEmail, validatePassword } from '../../../utils/validationUtils';

interface RegisterErrors {
    email: string;
    confirmEmail: string;
    password: string;
    confirmPassword: string;
    server: string;
}

export function useRegisterForm() {
    const navigate = useNavigate();
    const dispatch = useAppDispatch();
    const [register] = useRegisterMutation();

    const isLoggedIn = useSelector((state: IRootStore) => state.account.isLoggedIn);
    const loginError = useSelector((state: IRootStore) => state.account.error);

    const emailRef = useRef<HTMLInputElement>(null);
    const confirmEmailRef = useRef<HTMLInputElement>(null);
    const passwordRef = useRef<HTMLInputElement>(null);
    const confirmPasswordRef = useRef<HTMLInputElement>(null);

    const [errors, setErrors] = useState<RegisterErrors>({
        email: '',
        confirmEmail: '',
        password: '',
        confirmPassword: '',
        server: '',
    });
    const [loading, setLoading] = useState(false);
    const [showEulaModal, setShowEulaModal] = useState(false);

    useEffect(() => { dispatch(clearError()); }, [dispatch]);
    useEffect(() => { if (isLoggedIn) navigate('/'); }, [isLoggedIn, navigate]);

    const clearFieldError = (field: keyof RegisterErrors) =>
        setErrors((prev) => ({ ...prev, [field]: '' }));

    const validate = (): boolean => {
        const email = emailRef.current?.value ?? '';
        const confirmEmail = confirmEmailRef.current?.value ?? '';
        const password = passwordRef.current?.value ?? '';
        const confirmPassword = confirmPasswordRef.current?.value ?? '';

        const emailErr = validateEmail(email);
        const confirmEmailErr = email === confirmEmail ? '' : 'Emails do not match.';
        const passwordErr = validatePassword(password);
        const confirmPasswordErr = password === confirmPassword ? '' : 'Passwords do not match.';

        setErrors((prev) => ({
            ...prev,
            email: emailErr,
            confirmEmail: confirmEmailErr,
            password: passwordErr,
            confirmPassword: confirmPasswordErr,
        }));

        return !emailErr && !confirmEmailErr && !passwordErr && !confirmPasswordErr;
    };

    const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        setErrors((prev) => ({ ...prev, server: '' }));
        if (!validate()) return;
        setShowEulaModal(true);
    };

    const handleEulaAgree = async () => {
        setShowEulaModal(false);
        setLoading(true);
        try {
            const email = emailRef.current?.value ?? '';
            const password = passwordRef.current?.value ?? '';
            const res = await register({ login: email, password }).unwrap();
            if (res && (res.isSuccess || !res.isFailed)) {
                navigate('/account/confirmemail?emailConfirmToken=' + res.value.id);
            } else {
                const msg = res?.errors?.[0]?.message || res?.reasons?.[0]?.message || 'Registration failed';
                setErrors((prev) => ({ ...prev, server: msg }));
            }
        } catch (err) {
            const details = err instanceof Error ? err.message : 'Network error. Please try again.';
            setErrors((prev) => ({ ...prev, server: details }));
        } finally {
            setLoading(false);
        }
    };

    const handleEulaCancel = () => setShowEulaModal(false);

    return {
        emailRef,
        confirmEmailRef,
        passwordRef,
        confirmPasswordRef,
        errors,
        loading,
        loginError,
        showEulaModal,
        clearFieldError,
        handleSubmit,
        handleEulaAgree,
        handleEulaCancel,
    };
}
