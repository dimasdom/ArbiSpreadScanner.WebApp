import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { useSelector } from 'react-redux';
import type { IRootStore } from '../../../store/store';
import { useForgotPasswordMutation } from '../../../store/services/account';
import { validateEmail } from '../../../utils/validationUtils';

interface ForgotPasswordErrors {
    email: string;
    server: string;
}

export function useForgotPasswordForm() {
    const navigate = useNavigate();
    const isLoggedIn = useSelector((state: IRootStore) => state.account.isLoggedIn);
    const [forgotPassword] = useForgotPasswordMutation();

    const emailRef = useRef<HTMLInputElement>(null);
    const [errors, setErrors] = useState<ForgotPasswordErrors>({ email: '', server: '' });
    const [loading, setLoading] = useState(false);
    const [isSuccess, setIsSuccess] = useState(false);
    const [submittedEmail, setSubmittedEmail] = useState('');

    useEffect(() => { if (isLoggedIn) navigate('/'); }, [isLoggedIn, navigate]);

    const handleEmailInput = () => setErrors((prev) => ({ ...prev, email: '' }));

    const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        setErrors({ email: '', server: '' });

        const email = emailRef.current?.value ?? '';
        const emailErr = validateEmail(email);
        if (emailErr) {
            setErrors((prev) => ({ ...prev, email: emailErr }));
            return;
        }

        setLoading(true);
        try {
            const res = await forgotPassword({ email }).unwrap();
            if (res && (res.isSuccess || !res.isFailed)) {
                setSubmittedEmail(email);
                setIsSuccess(true);
            } else {
                const msg = res?.errors?.[0]?.message || 'Unable to send reset link.';
                setErrors((prev) => ({ ...prev, server: msg }));
            }
        } catch (err) {
            const details = err instanceof Error ? err.message : 'Network error. Please try again.';
            setErrors((prev) => ({ ...prev, server: details }));
        } finally {
            setLoading(false);
        }
    };

    return { emailRef, errors, loading, isSuccess, submittedEmail, handleEmailInput, handleSubmit };
}
