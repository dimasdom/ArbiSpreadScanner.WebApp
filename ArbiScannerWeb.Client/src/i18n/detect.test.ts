import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PREFERRED_LANG_KEY, getPreferredLanguage } from './detect';

const createLocalStorageMock = (): Storage => {
    const store = new Map<string, string>();
    return {
        getItem: (key: string) => store.get(key) ?? null,
        setItem: (key: string, value: string) => { store.set(key, value); },
        removeItem: (key: string) => { store.delete(key); },
        clear: () => { store.clear(); },
        key: (index: number) => Array.from(store.keys())[index] ?? null,
        get length() { return store.size; },
    } as Storage;
};

describe('getPreferredLanguage', () => {
    beforeEach(() => {
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
    });

    afterEach(() => {
        vi.restoreAllMocks();
    });

    it('returns the stored preference when it is a supported language', () => {
        localStorage.setItem(PREFERRED_LANG_KEY, 'fr');

        expect(getPreferredLanguage()).toBe('fr');
    });

    it('ignores a stored preference that is not supported', () => {
        localStorage.setItem(PREFERRED_LANG_KEY, 'xx');
        vi.spyOn(navigator, 'languages', 'get').mockReturnValue(['xx-XX']);
        vi.spyOn(navigator, 'language', 'get').mockReturnValue('xx-XX');

        expect(getPreferredLanguage()).toBe('en');
    });

    it('falls back to a supported browser language when nothing is stored', () => {
        vi.spyOn(navigator, 'languages', 'get').mockReturnValue(['de-DE', 'en-US']);

        expect(getPreferredLanguage()).toBe('de');
    });

    it('matches on the primary subtag of a browser language', () => {
        vi.spyOn(navigator, 'languages', 'get').mockReturnValue(['ES-mx']);

        expect(getPreferredLanguage()).toBe('es');
    });

    it('falls back to DEFAULT_LANG when nothing matches', () => {
        vi.spyOn(navigator, 'languages', 'get').mockReturnValue(['xx-XX', 'yy-YY']);

        expect(getPreferredLanguage()).toBe('en');
    });
});
