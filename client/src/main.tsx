import React from 'react';
import ReactDOM from 'react-dom/client';
import { Auth0Provider } from '@auth0/auth0-react';
import App from './App';
import './styles.css';

const domain = import.meta.env.VITE_AUTH0_DOMAIN ?? '';
const clientId = import.meta.env.VITE_AUTH0_CLIENT_ID ?? '';
const audience = import.meta.env.VITE_AUTH0_AUDIENCE ?? '';

const root = ReactDOM.createRoot(document.getElementById('root')!);
if (!domain || !clientId) {
  root.render(<main style={{fontFamily:'Arial',padding:40}}><h1>Auth0 configuration required</h1><p>Copy .env.example to .env and set the resident Auth0 domain, client ID, and API audience.</p></main>);
} else {
  root.render(<React.StrictMode><Auth0Provider domain={domain} clientId={clientId} authorizationParams={{ redirect_uri: window.location.origin, audience }}><App /></Auth0Provider></React.StrictMode>);
}
