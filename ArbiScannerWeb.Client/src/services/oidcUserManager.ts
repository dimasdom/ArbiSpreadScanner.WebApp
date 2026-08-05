import { UserManager, WebStorageStateStore } from 'oidc-client-ts';

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

export async function getAccessToken(): Promise<string | undefined> {
    const user = await oidcUserManager.getUser();
    return user?.access_token;
}
