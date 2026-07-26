import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import CookieConsentModal from './CookieConsentModal';
import { createLocalStorageMock } from '../test/localStorageMock';

describe('CookieConsentModal', () => {
    let storage: Storage;

    beforeEach(() => {
        storage = createLocalStorageMock();
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(storage);
    });

    it('shows the modal when consent has not been recorded', () => {
        render(<CookieConsentModal />);

        expect(screen.getByText('cookieConsent.title')).toBeInTheDocument();
    });

    it('does not show the modal when consent was already accepted', () => {
        storage.setItem('cookie_consent_accepted', 'true');

        render(<CookieConsentModal />);

        expect(screen.queryByText('cookieConsent.title')).not.toBeInTheDocument();
    });

    it('hides the modal and records consent when accepted', async () => {
        render(<CookieConsentModal />);

        await userEvent.click(screen.getByRole('button', { name: 'actions.ok' }));

        expect(screen.queryByText('cookieConsent.title')).not.toBeInTheDocument();
        expect(storage.getItem('cookie_consent_accepted')).toBe('true');
    });
});
