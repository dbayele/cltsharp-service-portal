import React from 'react';
import ReactDOM from 'react-dom/client';
import { Auth0Provider } from '@auth0/auth0-react';
import App from './App';
import './styles.css';
const domain=import.meta.env.VITE_AUTH0_DOMAIN??''; const clientId=import.meta.env.VITE_AUTH0_CLIENT_ID??''; const audience=import.meta.env.VITE_AUTH0_AUDIENCE??'';
const root=ReactDOM.createRoot(document.getElementById('root')!); if(!domain||!clientId){root.render(<main style={{fontFamily:'Arial',padding:40}}><h1>Auth0 configuration required</h1><p>Configure the employee SPA using .env.example.</p></main>);}else{root.render(<React.StrictMode><Auth0Provider domain={domain} clientId={clientId} authorizationParams={{redirect_uri:window.location.origin,audience,scope:'openid profile email read:requests write:requests read:police write:police read:fire write:fire read:airport write:airport'}}><App/></Auth0Provider></React.StrictMode>);}