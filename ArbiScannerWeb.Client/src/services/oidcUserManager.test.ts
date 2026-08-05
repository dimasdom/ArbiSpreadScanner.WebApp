import { describe, expect, it, vi } from 'vitest';

const getUserMock = vi.fn();

vi.mock('oidc-client-ts', () => ({
    UserManager: vi.fn().mockImplementation(function UserManagerMock() {
        return { getUser: getUserMock, settings: {} };
    }),
    WebStorageStateStore: vi.fn(),
}));

describe('oidcUserManager', () => {
    it('getAccessToken returns the current user access_token', async () => {
        getUserMock.mockResolvedValue({ access_token: 'tok-123' });
        const { getAccessToken } = await import('./oidcUserManager');

        await expect(getAccessToken()).resolves.toBe('tok-123');
    });

    it('getAccessToken returns undefined when there is no user', async () => {
        getUserMock.mockResolvedValue(null);
        const { getAccessToken } = await import('./oidcUserManager');

        await expect(getAccessToken()).resolves.toBeUndefined();
    });
});
