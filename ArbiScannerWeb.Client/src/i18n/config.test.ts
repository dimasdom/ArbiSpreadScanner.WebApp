import { beforeEach, describe, expect, it, vi } from 'vitest';

const dispatchMock = vi.fn();

vi.mock('../store/store', () => ({ default: { dispatch: dispatchMock } }));
vi.mock('../store/slices/languageSlice', () => ({
    setLanguage: (lng: string) => ({ type: 'language/setLanguage', payload: lng }),
}));
vi.mock('i18next-http-backend', () => ({
    default: class NoopBackend {
        static type = 'backend';
        type = 'backend';
        init() {}
        read(_lng: string, _ns: string, callback: (err: unknown, data: unknown) => void) {
            callback(null, {});
        }
    },
}));

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

describe('i18n config', () => {
    beforeEach(() => {
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
    });

    it('exports the lazily-loaded namespace list', async () => {
        const { NAMESPACES } = await import('./config');

        expect(NAMESPACES).toEqual(['common', 'main', 'account', 'spreads', 'subscription', 'faq', 'mcpToken', 'chat']);
    });

    it('syncs redux and localStorage when the language changes', async () => {
        const { default: i18n } = await import('./config');

        i18n.emit('languageChanged', 'es');

        expect(dispatchMock).toHaveBeenCalledWith({ type: 'language/setLanguage', payload: 'es' });
        expect(localStorage.getItem('preferredLang')).toBe('es');
    });
});
