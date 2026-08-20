import { useState } from 'react';
import ServiceCatalog from './components/ServiceCatalog';
import RequestForm from './components/RequestForm';
import AuthButtons from './AuthButtons';
import MyRequests from './MyRequests';
import WaterBillPayment from './WaterBillPayment';
import AboutPage from './AboutPage';
import type { ServiceCode } from './models';

export default function App() {
  const [selected, setSelected] = useState<ServiceCode | null>(null);
  const [showMyRequests, setShowMyRequests] = useState(false);
  const [showWaterPayment, setShowWaterPayment] = useState(false);
  const [showAbout, setShowAbout] = useState(false);
  if (selected) return <RequestForm serviceCode={selected} onBack={() => setSelected(null)} />;
  if (showMyRequests) return <MyRequests onBack={() => setShowMyRequests(false)} />;
  if (showWaterPayment) return <WaterBillPayment onBack={() => setShowWaterPayment(false)} />;
  if (showAbout) return <AboutPage onBack={() => setShowAbout(false)} />;
  return <>
    <header className="site-header">
      <div className="brand"><span className="crown">♛</span><span><strong>CLT#</strong><small>Charlotte Service Portal</small></span></div>
      <div className="header-tools"><button className="header-button" onClick={() => setShowAbout(true)}>About CLT#</button><AuthButtons onMyRequests={() => setShowMyRequests(true)} /></div>
    </header>
    <main className="home-shell">
      <section className="hero"><p className="eyebrow">City of Charlotte, North Carolina</p><h1>How can we help?</h1><p>Choose the type of request you want to submit. Police, fire, medical, and airport emergencies should always be reported through the appropriate emergency channel; call 911 when someone is in immediate danger.</p></section>
      <section className="payment-callout"><div><p className="eyebrow">Charlotte Water</p><h2>Pay your water bill</h2><p>Securely pay by credit card or Apple Pay and optionally save cards to your resident profile.</p></div><button className="primary" onClick={() => setShowWaterPayment(true)}>Pay water bill</button></section>
      <ServiceCatalog audience="resident" onSelect={setSelected} />
      <ServiceCatalog audience="business" onSelect={setSelected} />
    </main>
    <footer>CLT# · City of Charlotte, North Carolina · Prototype service platform</footer>
  </>;
}
