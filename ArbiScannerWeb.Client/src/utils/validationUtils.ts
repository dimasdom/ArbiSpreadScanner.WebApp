export const EMAIL_REGEX = /^[^\s@]+@[^\s@.]+\.[^\s@]+$/;

// Returns an i18n key (under common:validation.*), not a message, so
// callers translate it with the user's current language.
export function validateEmail(email: string): string {
    if (!email || !EMAIL_REGEX.test(email)) return 'validation.email.invalid';
    return '';
}

export function validatePassword(password: string): string {
    if (!password || password.length < 12) return 'validation.password.tooShort';
    if (!/\d/.test(password)) return 'validation.password.needsDigit';
    if (!/[^A-Za-z0-9]/.test(password)) return 'validation.password.needsSymbol';
    if (!/[A-Z]/.test(password)) return 'validation.password.needsUpper';
    return '';
}
