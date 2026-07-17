import { describe, it, expect } from 'vitest';
import { validateEmail, validatePassword } from './validationUtils';

describe('validateEmail', () => {
    it('returns error for empty string', () => {
        expect(validateEmail('')).toBe('Please enter a valid email address.');
    });

    it('returns error for string without @', () => {
        expect(validateEmail('notanemail')).toBe('Please enter a valid email address.');
    });

    it('returns error for string without domain extension', () => {
        expect(validateEmail('user@domain')).toBe('Please enter a valid email address.');
    });

    it('returns error for string with spaces', () => {
        expect(validateEmail('user @domain.com')).toBe('Please enter a valid email address.');
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
    it('returns error for empty string', () => {
        expect(validatePassword('')).toBe('Password must be at least 12 characters long.');
    });

    it('returns error when shorter than 12 characters', () => {
        expect(validatePassword('Short1@')).toBe('Password must be at least 12 characters long.');
    });

    it('returns error when no digit present', () => {
        expect(validatePassword('NoDigitHere!!AA')).toBe('Password must include at least one numeric character.');
    });

    it('returns error when no special character present', () => {
        expect(validatePassword('NoSpecialChar12A')).toBe('Password must include at least one special symbol like @.');
    });

    it('returns error when no uppercase letter present', () => {
        expect(validatePassword('nouppercase12@!')).toBe('Password must include at least one uppercase letter.');
    });

    it('returns empty string for a fully valid password', () => {
        expect(validatePassword('ValidPass1@!')).toBe('');
    });

    it('returns empty string when exactly 12 chars meeting all rules', () => {
        expect(validatePassword('Abcdefgh1@!!')).toBe('');
    });
});
