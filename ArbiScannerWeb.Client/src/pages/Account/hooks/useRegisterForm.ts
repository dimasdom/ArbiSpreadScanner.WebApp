import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { useSelector } from 'react-redux';
import { clearError } from '../../../store/slices/accountSlice';
import { useRegisterMutation } from '../../../store/services/account';
import { useAppDispatch } from '../../../hooks';
import type { IRootStore } from '../../../store/store';
import { validateEmail, validatePassword } from '../../../utils/validationUtils';

interface RegisterFields {
    email: string;
    password: string;
    confirmEmail: string;
    confirmPassword: string;
}

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

    const [fields, setFields] = useState<RegisterFields>({
        email: '',
        password: '',
        confirmEmail: '',
        confirmPassword: '',
    });
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

    const setField = (field: keyof RegisterFields, value: string) => {
        setFields((prev) => ({ ...prev, [field]: value }));
        setErrors((prev) => ({ ...prev, [field]: '' }));
    };

    const validate = (): boolean => {
        const emailErr = validateEmail(fields.email);
        const confirmEmailErr = fields.email !== fields.confirmEmail ? 'Emails do not match.' : '';
        const passwordErr = validatePassword(fields.password);
        const confirmPasswordErr = fields.password !== fields.confirmPassword ? 'Passwords do not match.' : '';

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
            const res = await register({ login: fields.email, password: fields.password }).unwrap();
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
        fields,
        errors,
        loading,
        loginError,
        showEulaModal,
        setField,
        handleSubmit,
        handleEulaAgree,
        handleEulaCancel,
    };
}
