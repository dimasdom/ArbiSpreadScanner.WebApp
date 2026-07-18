import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useSelector } from 'react-redux';
import { useTranslation } from 'react-i18next';
import type { IRootStore } from '../../../store/store';
import { useResetPasswordMutation } from '../../../store/services/account';
import { validatePassword } from '../../../utils/validationUtils';
import { useLocalizedNavigate } from '../../../i18n/routing';

interface ResetPasswordErrors {
    password: string;
    server: string;
}

export function useResetPasswordForm() {
    const { t } = useTranslation(['account', 'common']);
    const navigate = useLocalizedNavigate();
    const isLoggedIn = useSelector((state: IRootStore) => state.account.isLoggedIn);
    const [resetPassword] = useResetPasswordMutation();

    const passwordRef = useRef<HTMLInputElement>(null);
    const confirmPasswordRef = useRef<HTMLInputElement>(null);
    const [errors, setErrors] = useState<ResetPasswordErrors>({ password: '', server: '' });
    const [loading, setLoading] = useState(false);
    const [token, setToken] = useState('');

    useEffect(() => {
        if (isLoggedIn) navigate('/');
        const params = new URLSearchParams(globalThis.location.search);
        setToken(params.get('token') ?? '');
    }, [isLoggedIn, navigate]);

    const handlePasswordInput = () => setErrors((prev) => ({ ...prev, password: '' }));

    const validate = (): boolean => {
        if (!token) {
            setErrors((prev) => ({ ...prev, server: t('resetPassword.errors.tokenMissing') }));
            return false;
        }
        const password = passwordRef.current?.value ?? '';
        const confirmPassword = confirmPasswordRef.current?.value ?? '';

        const passwordErrKey = validatePassword(password);
        if (passwordErrKey) {
            setErrors((prev) => ({ ...prev, password: t(`common:${passwordErrKey}`) }));
            return false;
        }
        if (password !== confirmPassword) {
            setErrors((prev) => ({ ...prev, password: t('resetPassword.errors.passwordMismatch') }));
            return false;
        }
        return true;
    };

    const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
        e.preventDefault();
        setErrors({ password: '', server: '' });
        if (!validate()) return;
        setLoading(true);
        try {
            const password = passwordRef.current?.value ?? '';
            const res = await resetPassword({ token, newPassword: password }).unwrap();
            if (res && (res.isSuccess || !res.isFailed)) {
                navigate('/account/login');
            } else {
                const msg = res?.errors?.[0]?.message || res?.reasons?.[0]?.message || t('resetPassword.errors.resetFailed');
                setErrors((prev) => ({ ...prev, server: msg }));
            }
        } catch (err) {
            const details = err instanceof Error ? err.message : t('errors.networkError');
            setErrors((prev) => ({ ...prev, server: details }));
        } finally {
            setLoading(false);
        }
    };

    return { passwordRef, confirmPasswordRef, errors, loading, handlePasswordInput, handleSubmit };
}
