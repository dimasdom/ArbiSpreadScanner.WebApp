import { useEffect } from 'react';
import { useNavigate } from 'react-router';
import { useSelector } from 'react-redux';
import type { IRootStore } from '../store/store';

export function useAuthRedirect(redirectTo = '/account/login') {
    const navigate = useNavigate();
    const isLoggedIn = useSelector((state: IRootStore) => state.account.isLoggedIn);

    useEffect(() => {
        if (!isLoggedIn) {
            navigate(redirectTo);
        }
    }, [isLoggedIn, navigate, redirectTo]);
}
