import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router';
import { clearUserData } from '../../../store/slices/accountSlice';
import { useAppDispatch } from '../../../hooks';
import { useConfirmEmailMutation, useResendEmailRequestMutation } from '../../../store/services/account';

interface ConfirmEmailErrors {
    token: string;
    server: string;
}

export function useConfirmEmailForm() {
    const navigate = useNavigate();
    const dispatch = useAppDispatch();
    const [searchParams] = useSearchParams();
    const [confirmEmail] = useConfirmEmailMutation();
    const [resendEmailRequest] = useResendEmailRequestMutation();

    const tokenRef = useRef<HTMLInputElement>(null);
    const emailConfirmTokenRef = useRef('');
    const [errors, setErrors] = useState<ConfirmEmailErrors>({ token: '', server: '' });
    const [loading, setLoading] = useState(false);
    const [resending, setResending] = useState(false);

    useEffect(() => {
        const tokenFromUrl = searchParams.get('emailConfirmToken');
        if (tokenFromUrl) emailConfirmTokenRef.current = tokenFromUrl;
    }, [searchParams]);

    const handleTokenInput = () => setErrors({ token: '', server: '' });

    const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
        e.preventDefault();
        const emailConfirmToken = emailConfirmTokenRef.current;
        if (!emailConfirmToken) {
            setErrors((prev) => ({ ...prev, token: 'Confirmation token is required.' }));
            return;
        }
        setErrors({ token: '', server: '' });
        setLoading(true);
        try {
            const token = tokenRef.current?.value ?? '';
            const res = await confirmEmail({ emailConfirmToken, token }).unwrap();
            if (res && (res.isSuccess || !res.isFailed)) {
                dispatch(clearUserData());
                navigate('/account/login');
            } else {
                const msg = res?.errors?.[0]?.message || res?.reasons?.[0]?.message || 'Confirmation failed';
                setErrors((prev) => ({ ...prev, server: msg }));
            }
        } catch (err) {
            const details = err instanceof Error ? err.message : 'Network error. Please try again.';
            setErrors((prev) => ({ ...prev, server: details }));
        } finally {
            setLoading(false);
        }
    };

    const handleResendCode = async () => {
        const emailConfirmToken = emailConfirmTokenRef.current;
        if (!emailConfirmToken) {
            setErrors((prev) => ({ ...prev, server: 'Email confirmation token is required to resend confirmation code.' }));
            return;
        }
        setResending(true);
        setErrors((prev) => ({ ...prev, server: '' }));
        try {
            const result = await resendEmailRequest({ emailConfirmToken }).unwrap();
            if (!result.isSuccess) {
                const msg = result.errors?.[0]?.message || result.reasons?.[0]?.message || 'Failed to resend confirmation code.';
                setErrors((prev) => ({ ...prev, server: msg }));
            }
        } catch (err) {
            const details = err instanceof Error ? err.message : 'Network error. Please try again.';
            setErrors((prev) => ({ ...prev, server: details }));
        } finally {
            setResending(false);
        }
    };

    return { tokenRef, errors, loading, resending, handleTokenInput, handleSubmit, handleResendCode };
}
