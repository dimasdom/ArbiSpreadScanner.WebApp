import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useSelector } from 'react-redux';
import { useTranslation } from 'react-i18next';
import type { FetchBaseQueryError } from '@reduxjs/toolkit/query';
import type { SerializedError } from '@reduxjs/toolkit';
import { clearError } from '../../../store/slices/accountSlice';
import { useRegisterMutation } from '../../../store/services/account';
import { useAppDispatch } from '../../../hooks';
import type { IRootStore } from '../../../store/store';
import { validateEmail, validatePassword } from '../../../utils/validationUtils';
import { useLocalizedNavigate } from '../../../i18n/routing';
import { normalizeApiError } from '../../../utils/normalizeApiError';

interface RegisterErrors {
    email: string;
    confirmEmail: string;
    password: string;
    confirmPassword: string;
    server: string;
}

export function useRegisterForm() {
    const { t } = useTranslation(['account', 'common']);
    const navigate = useLocalizedNavigate();
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
    useEffect(() => { if (isLoggedIn) navigate('/spreads'); }, [isLoggedIn, navigate]);

    const clearFieldError = (field: keyof RegisterErrors) =>
        setErrors((prev) => ({ ...prev, [field]: '' }));

    const validate = (): boolean => {
        const email = emailRef.current?.value ?? '';
        const confirmEmail = confirmEmailRef.current?.value ?? '';
        const password = passwordRef.current?.value ?? '';
        const confirmPassword = confirmPasswordRef.current?.value ?? '';

        const emailErrKey = validateEmail(email);
        const emailErr = emailErrKey ? t(`common:${emailErrKey}`) : '';
        const confirmEmailErr = email === confirmEmail ? '' : t('register.errors.emailMismatch');
        const passwordErrKey = validatePassword(password);
        const passwordErr = passwordErrKey ? t(`common:${passwordErrKey}`) : '';
        const confirmPasswordErr = password === confirmPassword ? '' : t('register.errors.passwordMismatch');

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
                const msg = res?.errors?.[0]?.message || res?.reasons?.[0]?.message || t('register.errors.registrationFailed');
                setErrors((prev) => ({ ...prev, server: msg }));
            }
        } catch (err) {
            const details = normalizeApiError(err as FetchBaseQueryError | SerializedError | undefined).message;
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
