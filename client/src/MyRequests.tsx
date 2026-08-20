import { useEffect, useState } from 'react';
import { useAuth0 } from '@auth0/auth0-react';
import type { MyRequest } from './models';

export default function MyRequests({ onBack }: { onBack: () => void }) {
  const { getAccessTokenSilently } = useAuth0();
  const [items, setItems] = useState<MyRequest[]>([]);
  const [error, setError] = useState('');
  useEffect(() => { (async () => {
    try {
      const token = await getAccessTokenSilently();
      const response = await fetch('/api/my/requests', { headers: { Authorization: `Bearer ${token}` } });
      if (!response.ok) throw new Error('Unable to load your requests.');
      setItems(await response.json());
    } catch (e) { setError(e instanceof Error ? e.message : 'Unable to load your requests.'); }
  })(); }, [getAccessTokenSilently]);
  return <main className="form-shell">
    <button className="back-link" onClick={onBack}>← Back to services</button>
    <p className="eyebrow">Resident account</p><h1>My requests</h1>
    {error && <div className="alert alert-danger">{error}</div>}
    <div className="form-card request-table-wrap"><table className="request-table"><thead><tr><th>Tracking number</th><th>Service</th><th>Submitted</th><th>Status</th></tr></thead><tbody>
      {items.map(i => <tr key={i.submissionNumber}><td>{i.submissionNumber}</td><td>{i.serviceCode}</td><td>{new Date(i.receivedAtUtc).toLocaleString()}</td><td><span className="status-pill">{i.status}</span></td></tr>)}
      {!items.length && !error && <tr><td colSpan={4}>No account-linked requests yet.</td></tr>}
    </tbody></table></div>
  </main>;
}
