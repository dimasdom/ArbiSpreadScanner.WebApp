import { beforeEach, describe, expect, it, vi } from 'vitest';

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

describe('languageSlice', () => {
    beforeEach(() => {
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
    });

    it('initializes from the detected preferred language', async () => {
        vi.spyOn(navigator, 'languages', 'get').mockReturnValue(['de-DE']);
        const { default: reducer } = await import('./languageSlice');

        expect(reducer(undefined, { type: '@@INIT' })).toEqual({ language: 'de' });
    });

    it('setLanguage overwrites the current language', async () => {
        const { default: reducer, setLanguage } = await import('./languageSlice');

        const state = reducer({ language: 'en' }, setLanguage('fr'));

        expect(state.language).toBe('fr');
    });
});
