import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { MemoryRouter } from 'react-router';
import LanguageSwitcher from './LanguageSwitcher';

const navigateMock = vi.fn();

vi.mock('react-router', async (importOriginal) => {
    const actual = await importOriginal<typeof import('react-router')>();
    return { ...actual, useNavigate: () => navigateMock };
});

function renderWithLanguage(language: string, initialPath = '/en/faq') {
    const store = configureStore({
        reducer: { language: (state = { language }) => state },
    });
    return render(
        <Provider store={store}>
            <MemoryRouter initialEntries={[initialPath]}>
                <LanguageSwitcher />
            </MemoryRouter>
        </Provider>,
    );
}

describe('LanguageSwitcher', () => {
    beforeEach(() => {
        navigateMock.mockClear();
    });

    it('shows the current language code and flag on the trigger button', () => {
        renderWithLanguage('fr');

        expect(screen.getByRole('button', { name: 'language.switchAria' })).toHaveTextContent('fr');
    });

    it('opens the language list when the trigger is clicked', async () => {
        renderWithLanguage('en');

        await userEvent.click(screen.getByRole('button', { name: 'language.switchAria' }));

        expect(screen.getByRole('button', { name: /de/i })).toBeInTheDocument();
    });

    it('navigates to the localized path and closes the menu when a language is selected', async () => {
        const onSelect = vi.fn();
        const store = configureStore({ reducer: { language: (state = { language: 'en' }) => state } });
        render(
            <Provider store={store}>
                <MemoryRouter initialEntries={['/en/faq']}>
                    <LanguageSwitcher onSelect={onSelect} />
                </MemoryRouter>
            </Provider>,
        );
        await userEvent.click(screen.getByRole('button', { name: 'language.switchAria' }));

        await userEvent.click(screen.getByRole('button', { name: /de/i }));

        expect(navigateMock).toHaveBeenCalledWith('/de/faq');
        expect(onSelect).toHaveBeenCalledOnce();
        expect(screen.queryByRole('button', { name: /de/i })).not.toBeInTheDocument();
    });

    it('closes the menu when the backdrop is clicked', async () => {
        renderWithLanguage('en');
        await userEvent.click(screen.getByRole('button', { name: 'language.switchAria' }));
        expect(screen.getByRole('button', { name: /de/i })).toBeInTheDocument();

        const backdrop = document.querySelector('.fixed.inset-0.z-40');
        await userEvent.click(backdrop!);

        expect(screen.queryByRole('button', { name: /de/i })).not.toBeInTheDocument();
    });
});
