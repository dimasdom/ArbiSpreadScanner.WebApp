import { describe, expect, it } from 'vitest';
import { DEFAULT_LANG, SUPPORTED_LANGUAGES, SUPPORTED_LANG_CODES, getLanguageMeta, isSupportedLang } from './languages';

describe('languages', () => {
    it('derives SUPPORTED_LANG_CODES from SUPPORTED_LANGUAGES', () => {
        expect(SUPPORTED_LANG_CODES).toEqual(SUPPORTED_LANGUAGES.map((l) => l.code));
    });

    it('DEFAULT_LANG is a supported language', () => {
        expect(SUPPORTED_LANG_CODES).toContain(DEFAULT_LANG);
    });

    describe('isSupportedLang', () => {
        it('returns true for a supported code', () => {
            expect(isSupportedLang('en')).toBe(true);
        });

        it('returns false for an unsupported code', () => {
            expect(isSupportedLang('xx')).toBe(false);
        });

        it('returns false for null', () => {
            expect(isSupportedLang(null)).toBe(false);
        });

        it('returns false for undefined', () => {
            expect(isSupportedLang(undefined)).toBe(false);
        });

        it('returns false for empty string', () => {
            expect(isSupportedLang('')).toBe(false);
        });
    });

    describe('getLanguageMeta', () => {
        it('returns the matching language metadata', () => {
            expect(getLanguageMeta('fr')).toEqual({ code: 'fr', label: 'FR', flag: '🇫🇷' });
        });

        it('falls back to the first language for an unknown code', () => {
            expect(getLanguageMeta('xx')).toEqual(SUPPORTED_LANGUAGES[0]);
        });
    });
});
