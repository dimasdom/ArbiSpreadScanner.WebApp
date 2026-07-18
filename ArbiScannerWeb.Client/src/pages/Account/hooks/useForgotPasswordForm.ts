import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useSelector } from 'react-redux';
import { useTranslation } from 'react-i18next';
import type { IRootStore } from '../../../store/store';
import { useForgotPasswordMutation } from '../../../store/services/account';
import { validateEmail } from '../../../utils/validationUtils';
import { useLocalizedNavigate } from '../../../i18n/routing';

interface ForgotPasswordErrors {
    email: string;
    server: string;
}

export function useForgotPasswordForm() {
    const { t } = useTranslation(['account', 'common']);
    const navigate = useLocalizedNavigate();
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
        const emailErrKey = validateEmail(email);
        if (emailErrKey) {
            setErrors((prev) => ({ ...prev, email: t(`common:${emailErrKey}`) }));
            return;
        }

        setLoading(true);
        try {
            const res = await forgotPassword({ email }).unwrap();
            if (res && (res.isSuccess || !res.isFailed)) {
                setSubmittedEmail(email);
                setIsSuccess(true);
            } else {
                const msg = res?.errors?.[0]?.message || t('forgotPassword.errors.sendFailed');
                setErrors((prev) => ({ ...prev, server: msg }));
            }
        } catch (err) {
            const details = err instanceof Error ? err.message : t('errors.networkError');
            setErrors((prev) => ({ ...prev, server: details }));
        } finally {
            setLoading(false);
        }
    };

    return { emailRef, errors, loading, isSuccess, submittedEmail, handleEmailInput, handleSubmit };
}
