import { UserManager, WebStorageStateStore } from 'oidc-client-ts';
import toast from 'react-hot-toast';

// A single shared UserManager instance, used both by react-oidc-context's
// <AuthProvider userManager={...}> and by non-component code (RTK Query's
// prepareHeaders, signalrService) that can't use the useAuth() hook.
export const oidcUserManager = new UserManager({
    authority: import.meta.env.VITE_OIDC_AUTHORITY ?? '',
    client_id: import.meta.env.VITE_OIDC_CLIENT_ID ?? '',
    redirect_uri: import.meta.env.VITE_OIDC_REDIRECT_URI ?? '',
    // Without this, signoutRedirect() sends Keycloak's end-session endpoint no
    // post_logout_redirect_uri, so it has nowhere to send the browser back to
    // and just shows its own logged-out page instead. Matches the wildcard
    // already whitelisted in post.logout.redirect.uris (realm-export).
    post_logout_redirect_uri: window.location.origin + import.meta.env.BASE_URL,
    response_type: 'code',
    scope: 'openid profile email',
    automaticSilentRenew: true,
    userStore: new WebStorageStateStore({ store: window.sessionStorage }),
});

// automaticSilentRenew fires its refresh_token request ~60s before the
// access token expires, but that timer is just a setTimeout against wall
// time — if the tab is backgrounded or the machine sleeps through the
// renewal window, the refresh token itself can age past Keycloak's SSO
// idle timeout before the (now-overdue) timer finally fires. Keycloak then
// rejects it (invalid_grant / "Token is not active"), the failed attempt is
// never retried, and without this, the stale user + dead access token would
// just sit there until something else notices — API calls would keep going
// out and failing silently in the meantime. Force a clean re-login instead.
let reauthing = false;
async function forceReauth() {
    if (reauthing) return;
    reauthing = true;
    await oidcUserManager.removeUser();
    toast.error('Your session has expired. Please sign in again.');
    await oidcUserManager.signinRedirect();
}
oidcUserManager.events.addSilentRenewError(() => { void forceReauth(); });
oidcUserManager.events.addAccessTokenExpired(() => { void forceReauth(); });

export async function getAccessToken(): Promise<string | undefined> {
    const user = await oidcUserManager.getUser();
    return user?.access_token;
}
