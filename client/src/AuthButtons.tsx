import { useAuth0 } from '@auth0/auth0-react';

type Props = { onMyRequests: () => void };

export default function AuthButtons({ onMyRequests }: Props) {
  const { isAuthenticated, isLoading, loginWithRedirect, logout, user } = useAuth0();
  if (isLoading) return <span className="auth-status">Checking account…</span>;
  if (!isAuthenticated) return <div className="auth-actions">
    <button className="header-button" onClick={() => loginWithRedirect()}>Sign in</button>
    <button className="header-button header-button-light" onClick={() => loginWithRedirect({ authorizationParams: { screen_hint: 'signup' } })}>Create account</button>
  </div>;
  return <div className="auth-actions">
    <span className="auth-status">{user?.name ?? user?.email ?? 'Signed in'}</span>
    <button className="header-button" onClick={onMyRequests}>My requests</button>
    <button className="header-button header-button-light" onClick={() => logout({ logoutParams: { returnTo: window.location.origin } })}>Sign out</button>
  </div>;
}
