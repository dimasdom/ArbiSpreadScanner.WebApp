import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { useSelector } from 'react-redux';
import type { IRootStore } from '../../../store/store';
import { useResetPasswordMutation } from '../../../store/services/account';
import { validatePassword } from '../../../utils/validationUtils';

interface ResetPasswordErrors {
    password: string;
    server: string;
}

export function useResetPasswordForm() {
    const navigate = useNavigate();
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
            setErrors((prev) => ({ ...prev, server: 'Confirmation token is missing from URL.' }));
            return false;
        }
        const password = passwordRef.current?.value ?? '';
        const confirmPassword = confirmPasswordRef.current?.value ?? '';

        const passwordErr = validatePassword(password);
        if (passwordErr) {
            setErrors((prev) => ({ ...prev, password: passwordErr }));
            return false;
        }
        if (password !== confirmPassword) {
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
            const password = passwordRef.current?.value ?? '';
            const res = await resetPassword({ token, newPassword: password }).unwrap();
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

    return { passwordRef, confirmPasswordRef, errors, loading, handlePasswordInput, handleSubmit };
}
