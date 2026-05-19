export const EMAIL_REGEX = /^\S+@\S+\.\S+$/;

export function validateEmail(email: string): string {
    if (!email || !EMAIL_REGEX.test(email)) return 'Please enter a valid email address.';
    return '';
}

export function validatePassword(password: string): string {
    if (!password || password.length < 12) return 'Password must be at least 12 characters long.';
    if (!/\d/.test(password)) return 'Password must include at least one numeric character.';
    if (!/[^A-Za-z0-9]/.test(password)) return 'Password must include at least one special symbol like @.';
    if (!/[A-Z]/.test(password)) return 'Password must include at least one uppercase letter.';
    return '';
}
