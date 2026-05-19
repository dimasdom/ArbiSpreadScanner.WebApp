import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { useSelector } from 'react-redux';
import { clearError } from '../../../store/slices/accountSlice';
import { useLoginMutation } from '../../../store/services/account';
import { useAppDispatch } from '../../../hooks';
import type { IRootStore } from '../../../store/store';
import { validateEmail } from '../../../utils/validationUtils';

interface LoginErrors {
    email: string;
    password: string;
}

export function useLoginForm() {
    const navigate = useNavigate();
    const dispatch = useAppDispatch();
    const [login] = useLoginMutation();

    const isLoggedIn = useSelector((state: IRootStore) => state.account.isLoggedIn);
    const loginError = useSelector((state: IRootStore) => state.account.error);
    const needsEmailConfirmation = useSelector((state: IRootStore) => state.account.needsEmailConfirmation);
    const emailConfirmToken = useSelector((state: IRootStore) => state.account.emailConfirmToken);

    const [errors, setErrors] = useState<LoginErrors>({ email: '', password: '' });
    const [loading, setLoading] = useState(false);

    useEffect(() => { dispatch(clearError()); }, [dispatch]);
    useEffect(() => { if (isLoggedIn) navigate('/'); }, [isLoggedIn, navigate]);
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
        const data = new FormData(event.currentTarget);
        const emailVal = (data.get('email') as string) ?? '';
        const passwordVal = (data.get('password') as string) ?? '';

        const emailErr = validateEmail(emailVal);
        const passwordErr = !passwordVal || passwordVal.length < 12
            ? 'Password must be at least 12 characters long.'
            : '';

        if (emailErr || passwordErr) {
            setErrors({ email: emailErr, password: passwordErr });
            return;
        }

        setLoading(true);
        login({ login: emailVal, password: passwordVal }).finally(() => setLoading(false));
    };

    return { errors, loading, loginError, clearFieldError, handleSubmit };
}
