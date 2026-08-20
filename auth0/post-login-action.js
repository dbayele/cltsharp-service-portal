/**
 * Auth0 Post-Login Action.
 * Required secrets: LOGIN_AUDIT_URL, LOGIN_AUDIT_SECRET.
 * Deploy at the tenant level so it applies to resident and employee applications.
 */
exports.onExecutePostLogin = async (event, api) => {
  const country = (event.request?.geoip?.countryCode || '').toUpperCase();
  const ip = event.request?.ip || null;
  const allowed = country === 'US';
  const body = {
    subject: event.user?.user_id || null,
    email: event.user?.email || null,
    ipAddress: ip,
    countryCode: country || null,
    userAgent: event.request?.user_agent || null,
    auth0ClientId: event.client?.client_id || null,
    sessionId: event.session?.id || null,
    allowed,
    denyReason: allowed ? null : 'Non-US login blocked'
  };
  try {
    await fetch(event.secrets.LOGIN_AUDIT_URL, {
      method: 'POST', headers: {'content-type':'application/json','x-auth-event-secret':event.secrets.LOGIN_AUDIT_SECRET}, body: JSON.stringify(body)
    });
  } catch (e) { console.log('Login audit delivery failed', e.message); }
  if (!allowed) api.access.deny('This service is available only from the United States.');
};
