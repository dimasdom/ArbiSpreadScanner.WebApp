import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { useSelector } from 'react-redux';
import type { IRootStore } from '../../../store/store';
import { useResetPasswordMutation } from '../../../store/services/account';
import { validatePassword } from '../../../utils/validationUtils';

interface ResetPasswordFields {
    password: string;
    confirmPassword: string;
}

interface ResetPasswordErrors {
    password: string;
    server: string;
}

export function useResetPasswordForm() {
    const navigate = useNavigate();
    const isLoggedIn = useSelector((state: IRootStore) => state.account.isLoggedIn);
    const [resetPassword] = useResetPasswordMutation();

    const [fields, setFields] = useState<ResetPasswordFields>({ password: '', confirmPassword: '' });
    const [errors, setErrors] = useState<ResetPasswordErrors>({ password: '', server: '' });
    const [loading, setLoading] = useState(false);
    const [token, setToken] = useState('');

    useEffect(() => {
        if (isLoggedIn) navigate('/');
        const params = new URLSearchParams(globalThis.location.search);
        setToken(params.get('token') ?? '');
    }, [isLoggedIn, navigate]);

    const setField = (field: keyof ResetPasswordFields, value: string) => {
        setFields((prev) => ({ ...prev, [field]: value }));
        setErrors((prev) => ({ ...prev, password: '' }));
    };

    const validate = (): boolean => {
        if (!token) {
            setErrors((prev) => ({ ...prev, server: 'Confirmation token is missing from URL.' }));
            return false;
        }
        const passwordErr = validatePassword(fields.password);
        if (passwordErr) {
            setErrors((prev) => ({ ...prev, password: passwordErr }));
            return false;
        }
        if (fields.password !== fields.confirmPassword) {
            setErrors((prev) => ({ ...prev, password: 'Password and confirmation do not match.' }));
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
            const res = await resetPassword({ token, newPassword: fields.password }).unwrap();
            if (res && (res.isSuccess || !res.isFailed)) {
                navigate('/account/login');
            } else {
                const msg = res?.errors?.[0]?.message || res?.reasons?.[0]?.message || 'Failed to reset password';
                setErrors((prev) => ({ ...prev, server: msg }));
            }
        } catch (err) {
            const details = err instanceof Error ? err.message : 'Network error. Please try again.';
            setErrors((prev) => ({ ...prev, server: details }));
        } finally {
            setLoading(false);
        }
    };

    return { fields, errors, loading, setField, handleSubmit };
}
