import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { stripLangPrefix, useLocalizedNavigate, withLang } from './routing';

const navigateMock = vi.fn();

vi.mock('react-router', async (importOriginal) => {
    const actual = await importOriginal<typeof import('react-router')>();
    return { ...actual, useNavigate: () => navigateMock };
});

describe('withLang', () => {
    it('prepends the language segment to an absolute path', () => {
        expect(withLang('/faq', 'de')).toBe('/de/faq');
    });

    it('leaves a relative path unchanged', () => {
        expect(withLang('faq', 'de')).toBe('faq');
    });

    it('leaves a query-only string unchanged', () => {
        expect(withLang('?tab=1', 'de')).toBe('?tab=1');
    });
});

describe('stripLangPrefix', () => {
    it('strips a supported language segment', () => {
        expect(stripLangPrefix('/uk/faq')).toBe('/faq');
    });

    it('reduces a bare language segment to the root path', () => {
        expect(stripLangPrefix('/uk')).toBe('/');
    });

    it('leaves paths without a language segment unchanged', () => {
        expect(stripLangPrefix('/faq')).toBe('/faq');
    });

    it('does not strip an unsupported two-letter segment', () => {
        expect(stripLangPrefix('/xx/faq')).toBe('/xx/faq');
    });
});

describe('useLocalizedNavigate', () => {
    beforeEach(() => {
        navigateMock.mockClear();
    });

    it('navigates with the language segment prepended for a string path', () => {
        const { result } = renderHook(() => useLocalizedNavigate());

        result.current('/faq');

        expect(navigateMock).toHaveBeenCalledWith('/en/faq', undefined);
    });

    it('navigates with the language segment prepended for a To object', () => {
        const { result } = renderHook(() => useLocalizedNavigate());

        result.current({ pathname: '/faq', search: '?x=1' }, { replace: true });

        expect(navigateMock).toHaveBeenCalledWith({ pathname: '/en/faq', search: '?x=1' }, { replace: true });
    });

    it('passes a numeric delta straight through without modification', () => {
        const { result } = renderHook(() => useLocalizedNavigate());

        result.current(-1);

        expect(navigateMock).toHaveBeenCalledWith(-1);
    });
});
