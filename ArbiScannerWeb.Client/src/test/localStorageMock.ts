export const createLocalStorageMock = (): Storage => {
    const store = new Map<string, string>();
    const methods = {
        getItem: (key: string) => store.get(key) ?? null,
        setItem: (key: string, value: string) => { store.set(key, String(value)); },
        removeItem: (key: string) => { store.delete(key); },
        clear: () => { store.clear(); },
        key: (index: number) => Array.from(store.keys())[index] ?? null,
    };
    const target = Object.create(methods) as Record<string, unknown>;
    Object.defineProperty(target, 'length', { get: () => store.size, enumerable: false, configurable: true });

    // Proxied so `Object.keys(localStorage)` / bracket access reflect stored
    // entries, matching real browser localStorage semantics.
    return new Proxy(target, {
        get(t, prop) {
            if (typeof prop === 'string' && store.has(prop)) return store.get(prop);
            return Reflect.get(t, prop);
        },
        set(t, prop, value) {
            if (typeof prop === 'string' && !(prop in methods) && prop !== 'length') {
                store.set(prop, String(value));
                return true;
            }
            return Reflect.set(t, prop, value);
        },
        has(t, prop) {
            return (typeof prop === 'string' && store.has(prop)) || Reflect.has(t, prop);
        },
        deleteProperty(_t, prop) {
            if (typeof prop === 'string') store.delete(prop);
            return true;
        },
        ownKeys() {
            return Array.from(store.keys());
        },
        getOwnPropertyDescriptor(_t, prop) {
            if (typeof prop === 'string' && store.has(prop)) {
                return { enumerable: true, configurable: true, value: store.get(prop) };
            }
            return undefined;
        },
    }) as unknown as Storage;
};
