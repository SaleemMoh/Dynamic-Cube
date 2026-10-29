import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { User, UserManager, WebStorageStateStore } from 'oidc-client-ts';
import { RUNTIME_CONFIG } from '../config/runtime-config';

export interface AuthUser {
  subject: string;
  userName: string;
  name: string;
  email?: string;
}

export const AUTH_CALLBACK_PATH = 'auth/callback';

/**
 * OpenID Connect sign-in with authorization code + PKCE (oidc-client-ts). Tokens are kept in session storage (local
 * storage when configured) and renewed with the refresh token before they expire.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly config = inject(RUNTIME_CONFIG);
  private readonly router = inject(Router);
  private readonly current = signal<User | null>(null);
  private readonly manager = new UserManager({
    authority: this.config.auth.authority,
    client_id: this.config.auth.clientId,
    redirect_uri: `${location.origin}/${AUTH_CALLBACK_PATH}`,
    post_logout_redirect_uri: `${location.origin}/`,
    response_type: 'code',
    scope: this.config.auth.scope ?? 'openid profile email',
    automaticSilentRenew: true,
    userStore: new WebStorageStateStore({ store: this.config.auth.storage === 'local' ? localStorage : sessionStorage }),
  });

  readonly user = computed<AuthUser | null>(() => {
    const user = this.current();
    if (!user || user.expired) {
      return null;
    }
    const profile = user.profile;
    return {
      subject: profile.sub,
      userName: profile.preferred_username ?? profile.sub,
      name: profile.name ?? profile.preferred_username ?? profile.sub,
      email: profile.email,
    };
  });

  readonly isAuthenticated = computed(() => this.user() !== null);

  constructor() {
    this.manager.events.addUserLoaded((user) => this.current.set(user));
    this.manager.events.addUserUnloaded(() => this.current.set(null));
    this.manager.events.addSilentRenewError(() => this.current.set(null));
  }

  /** Restores the session from storage; called once at start-up. */
  async initialize(): Promise<void> {
    const user = await this.manager.getUser();
    if (user && user.expired && user.refresh_token) {
      try {
        this.current.set(await this.manager.signinSilent());
        return;
      } catch {
        // Offline, the session is kept to be renewed later; online, the refresh token was refused.
        if (navigator.onLine) await this.manager.removeUser();
      }
    }
    this.current.set(user && !user.expired ? user : null);
  }

  accessToken(): string | null {
    const user = this.current();
    return user && !user.expired ? user.access_token : null;
  }

  /** Redirects to the identity provider; the user comes back to `returnUrl`. */
  login(returnUrl: string = this.router.url): Promise<void> {
    return this.manager.signinRedirect({ state: { returnUrl } });
  }

  /** Completes the redirect from the identity provider and returns the URL to continue with. */
  async completeLogin(): Promise<string> {
    const user = await this.manager.signinRedirectCallback();
    this.current.set(user);
    const state = user.state as { returnUrl?: string } | undefined;
    return state?.returnUrl && state.returnUrl.startsWith('/') && !state.returnUrl.startsWith(`/${AUTH_CALLBACK_PATH}`) ? state.returnUrl : '/';
  }

  async logout(): Promise<void> {
    const user = this.current();
    await this.manager.removeUser();
    this.current.set(null);
    await this.manager.signoutRedirect({ id_token_hint: user?.id_token });
  }
}
