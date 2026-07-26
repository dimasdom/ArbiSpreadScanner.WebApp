import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ThemeProvider, useTheme } from './ThemeContext';
import { createLocalStorageMock } from '../test/localStorageMock';

function stubMatchMedia(matches: boolean) {
    const listeners: Array<(e: MediaQueryListEvent) => void> = [];
    const mql = {
        matches,
        media: '(prefers-color-scheme: dark)',
        addEventListener: (_event: string, listener: (e: MediaQueryListEvent) => void) => { listeners.push(listener); },
        removeEventListener: (_event: string, listener: (e: MediaQueryListEvent) => void) => {
            const idx = listeners.indexOf(listener);
            if (idx >= 0) listeners.splice(idx, 1);
        },
    };
    Object.defineProperty(window, 'matchMedia', {
        writable: true,
        configurable: true,
        value: vi.fn().mockReturnValue(mql),
    });
    return {
        fire: (nextMatches: boolean) => listeners.forEach((l) => l({ matches: nextMatches } as MediaQueryListEvent)),
    };
}

function TestConsumer() {
    const { theme, toggleTheme } = useTheme();
    return (
        <div>
            <span data-testid="theme">{theme}</span>
            <button type="button" onClick={toggleTheme}>toggle</button>
        </div>
    );
}

describe('ThemeProvider / useTheme', () => {
    let storage: Storage;

    beforeEach(() => {
        storage = createLocalStorageMock();
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(storage);
        document.documentElement.classList.remove('dark');
    });

    it('defaults to light when no stored preference and OS prefers light', () => {
        stubMatchMedia(false);

        render(<TestConsumer />, { wrapper: ThemeProvider });

        expect(screen.getByTestId('theme')).toHaveTextContent('light');
        expect(document.documentElement.classList.contains('dark')).toBe(false);
    });

    it('defaults to dark when the OS prefers dark', () => {
        stubMatchMedia(true);

        render(<TestConsumer />, { wrapper: ThemeProvider });

        expect(screen.getByTestId('theme')).toHaveTextContent('dark');
        expect(document.documentElement.classList.contains('dark')).toBe(true);
    });

    it('uses the stored preference over the OS setting', () => {
        storage.setItem('theme', 'dark');
        stubMatchMedia(false);

        render(<TestConsumer />, { wrapper: ThemeProvider });

        expect(screen.getByTestId('theme')).toHaveTextContent('dark');
    });

    it('toggleTheme flips the theme and persists it', async () => {
        stubMatchMedia(false);
        render(<TestConsumer />, { wrapper: ThemeProvider });
        expect(screen.getByTestId('theme')).toHaveTextContent('light');

        await userEvent.click(screen.getByRole('button', { name: 'toggle' }));

        expect(screen.getByTestId('theme')).toHaveTextContent('dark');
        expect(storage.getItem('theme')).toBe('dark');
        expect(document.documentElement.classList.contains('dark')).toBe(true);
    });

    it('follows an OS preference change while no manual override is stored', () => {
        const mql = stubMatchMedia(false);
        render(<TestConsumer />, { wrapper: ThemeProvider });
        expect(screen.getByTestId('theme')).toHaveTextContent('light');

        act(() => { mql.fire(true); });

        expect(screen.getByTestId('theme')).toHaveTextContent('dark');
    });

    it('ignores an OS preference change once a manual override is stored', async () => {
        const mql = stubMatchMedia(false);
        render(<TestConsumer />, { wrapper: ThemeProvider });
        await userEvent.click(screen.getByRole('button', { name: 'toggle' }));
        expect(screen.getByTestId('theme')).toHaveTextContent('dark');

        act(() => { mql.fire(false); });

        expect(screen.getByTestId('theme')).toHaveTextContent('dark');
    });
});
