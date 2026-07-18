import { describe, it, expect } from 'vitest';
import { validateEmail, validatePassword } from './validationUtils';

describe('validateEmail', () => {
    it('returns error key for empty string', () => {
        expect(validateEmail('')).toBe('validation.email.invalid');
    });

    it('returns error key for string without @', () => {
        expect(validateEmail('notanemail')).toBe('validation.email.invalid');
    });

    it('returns error key for string without domain extension', () => {
        expect(validateEmail('user@domain')).toBe('validation.email.invalid');
    });

    it('returns error key for string with spaces', () => {
        expect(validateEmail('user @domain.com')).toBe('validation.email.invalid');
    });

    it('returns empty string for valid email', () => {
        expect(validateEmail('user@example.com')).toBe('');
    });

    it('returns empty string for email with subdomains', () => {
        expect(validateEmail('user@mail.example.co.uk')).toBe('');
    });

    it('returns empty string for email with plus tag', () => {
        expect(validateEmail('user+tag@example.com')).toBe('');
    });
});

describe('validatePassword', () => {
    it('returns error key for empty string', () => {
        expect(validatePassword('')).toBe('validation.password.tooShort');
    });

    it('returns error key when shorter than 12 characters', () => {
        expect(validatePassword('Short1@')).toBe('validation.password.tooShort');
    });

    it('returns error key when no digit present', () => {
        expect(validatePassword('NoDigitHere!!AA')).toBe('validation.password.needsDigit');
    });

    it('returns error key when no special character present', () => {
        expect(validatePassword('NoSpecialChar12A')).toBe('validation.password.needsSymbol');
    });

    it('returns error key when no uppercase letter present', () => {
        expect(validatePassword('nouppercase12@!')).toBe('validation.password.needsUpper');
    });

    it('returns empty string for a fully valid password', () => {
        expect(validatePassword('ValidPass1@!')).toBe('');
    });

    it('returns empty string when exactly 12 chars meeting all rules', () => {
        expect(validatePassword('Abcdefgh1@!!')).toBe('');
    });
});
