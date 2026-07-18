import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useSearchParams } from 'react-router';
import { useSelector } from 'react-redux';
import { useTranslation } from 'react-i18next';
import { clearError } from '../../../store/slices/accountSlice';
import { useLoginMutation } from '../../../store/services/account';
import { useAppDispatch } from '../../../hooks';
import type { IRootStore } from '../../../store/store';
import { validateEmail } from '../../../utils/validationUtils';
import { useLocalizedNavigate } from '../../../i18n/routing';

interface LoginErrors {
    email: string;
    password: string;
}

export function useLoginForm() {
    const { t } = useTranslation(['account', 'common']);
    const navigate = useLocalizedNavigate();
    const [searchParams] = useSearchParams();
    const dispatch = useAppDispatch();
    const [login] = useLoginMutation();

    const isLoggedIn = useSelector((state: IRootStore) => state.account.isLoggedIn);
    const loginError = useSelector((state: IRootStore) => state.account.error);
    const needsEmailConfirmation = useSelector((state: IRootStore) => state.account.needsEmailConfirmation);
    const emailConfirmToken = useSelector((state: IRootStore) => state.account.emailConfirmToken);

    const emailRef = useRef<HTMLInputElement>(null);
    const passwordRef = useRef<HTMLInputElement>(null);
    const [errors, setErrors] = useState<LoginErrors>({ email: '', password: '' });
    const [loading, setLoading] = useState(false);

    useEffect(() => { dispatch(clearError()); }, [dispatch]);
    useEffect(() => {
        if (!isLoggedIn) return;
        const redirect = searchParams.get('redirect');
        navigate(redirect?.startsWith('/') ? redirect : '/spreads');
    }, [isLoggedIn, navigate, searchParams]);
    useEffect(() => {
        if (needsEmailConfirmation) {
            const url = emailConfirmToken
                ? `/account/confirmemail?emailConfirmToken=${emailConfirmToken}`
                : '/account/confirmemail';
            navigate(url);
        }
    }, [needsEmailConfirmation, emailConfirmToken, navigate]);

    const clearFieldError = (field: keyof LoginErrors) =>
        setErrors((prev) => ({ ...prev, [field]: '' }));

    const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const emailVal = emailRef.current?.value ?? '';
        const passwordVal = passwordRef.current?.value ?? '';

        const emailErrKey = validateEmail(emailVal);
        const emailErr = emailErrKey ? t(`common:${emailErrKey}`) : '';
        const passwordErr = !passwordVal || passwordVal.length < 12
            ? t('common:validation.password.tooShort')
            : '';

        if (emailErr || passwordErr) {
            setErrors({ email: emailErr, password: passwordErr });
            return;
        }

        setLoading(true);
        login({ login: emailVal, password: passwordVal }).finally(() => setLoading(false));
    };

    return { emailRef, passwordRef, errors, loading, loginError, clearFieldError, handleSubmit };
}
