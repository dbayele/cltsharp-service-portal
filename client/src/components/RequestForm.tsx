import { useMemo, useState } from 'react';
import { useAuth0 } from '@auth0/auth0-react';
import { submitServiceRequest } from '../api';
import { services } from '../forms/definitions';
import type { FormValues, ServiceCode, SubmitResult } from '../models';
import Field from './Field';

type Props = { serviceCode: ServiceCode; onBack: () => void };

type Step = { title: string; content: React.ReactNode };

const yesNo = ['Yes', 'No'];

export default function RequestForm({ serviceCode, onBack }: Props) {
  const [values, setValues] = useState<FormValues>({});
  const [stepIndex, setStepIndex] = useState(0);
  const [result, setResult] = useState<SubmitResult | null>(null);
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const { isAuthenticated, getAccessTokenSilently } = useAuth0();
  const service = services.find((item) => item.code === serviceCode)!;

  const steps = useMemo<Step[]>(() => buildSteps(serviceCode, values, setValues), [serviceCode, values]);
  const last = stepIndex === steps.length - 1;

  async function submit() {
    setError('');
    setSubmitting(true);
    try {
      const token = isAuthenticated ? await getAccessTokenSilently() : undefined;
      setResult(await submitServiceRequest(serviceCode, values, token));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Submission failed.');
    } finally {
      setSubmitting(false);
    }
  }

  if (result) {
    return (
      <main className="form-shell">
        <div className="success-panel">
          <p className="eyebrow">Submitted</p>
          <h1>Thank you</h1>
          <p>Your submission number is <strong>{result.submissionNumber}</strong>.</p>
          {serviceCode === 'crime-report' && <p>This submission may require review before it becomes an official police report.</p>}
          {serviceCode === 'crime-tip' && <p>No official police report is created by this tip submission.</p>}
          <button className="primary" onClick={onBack}>Return to services</button>
        </div>
      </main>
    );
  }

  return (
    <main className="form-shell">
      <button className="back-link" onClick={onBack}>← All services</button>
      <p className="eyebrow">CLT# service request</p>
      <h1>{service.title}</h1>
      <p className="lede">{service.summary}</p>

      {(serviceCode === 'crime-report' || serviceCode === 'crime-tip') && (
        <div className="alert alert-danger" role="alert">
          <strong>Emergency?</strong> Call 911 if anyone is in immediate danger, a crime is happening now, or an immediate police response is needed.
        </div>
      )}

      {serviceCode === 'crime-tip' && (
        <div className="alert">
          Crime tips can be submitted anonymously. Contact information is optional.
        </div>
      )}

      <ol className="stepper" aria-label="Request progress">
        {steps.map((step, index) => (
          <li key={step.title} className={index === stepIndex ? 'active' : index < stepIndex ? 'done' : ''}>
            <span>{index + 1}</span>{step.title}
          </li>
        ))}
      </ol>

      <form onSubmit={(event) => { event.preventDefault(); last ? submit() : setStepIndex(stepIndex + 1); }}>
        <section className="form-card">
          <h2>{steps[stepIndex].title}</h2>
          {steps[stepIndex].content}
          {error && <div className="alert alert-danger">{error}</div>}
          <div className="form-actions">
            {stepIndex > 0 && <button type="button" className="secondary" onClick={() => setStepIndex(stepIndex - 1)}>Back</button>}
            <button type="submit" className="primary" disabled={submitting}>{last ? (submitting ? 'Submitting…' : 'Submit') : 'Continue'}</button>
          </div>
        </section>
      </form>
    </main>
  );
}

function buildSteps(serviceCode: ServiceCode, values: FormValues, setValues: (next: FormValues) => void): Step[] {
  const field = (name: string, label: string, required = false, type: any = 'text', options?: string[], help?: string) =>
    <Field key={name} {...{ name, label, required, type, options, help, values, setValues }} />;

  if (serviceCode === 'crime-report') {
    return [
      { title: 'Eligibility', content: <>
        {field('emergency', 'Is this an emergency or is the crime happening now?', true, 'select', yesNo)}
        {field('jurisdiction', 'Where did the incident occur?', true, 'select', ['Charlotte / Mecklenburg County', 'Online / location unknown', 'Somewhere else'])}
      </> },
      { title: 'Location', content: <>
        {field('incidentAddress', 'Incident address or location', true)}
        {field('locationDetails', 'Location details', false, 'textarea', undefined, 'Apartment, business, parking area, intersection, landmark, or other helpful detail.')}
      </> },
      { title: 'Details', content: <>
        {field('offenseType', 'What are you reporting?', true, 'select', ['Theft / Larceny','Theft from Vehicle','Vehicle Theft','Property Damage / Vandalism','Burglary','Fraud / Scam','Identity Theft','Harassing or Threatening Communications','Lost Property','Other Non-Emergency Crime'])}
        {field('incidentDate', 'Approximate incident date', true, 'date')}
        {field('incidentTime', 'Approximate incident time', false, 'time')}
        {field('involvement', 'What is your involvement?', true, 'select', ['Victim','Business / organization representative','Parent / guardian','Witness','Other'])}
        {field('narrative', 'Tell us what happened', true, 'textarea')}
        {field('suspectKnown', 'Do you have information about a possible suspect?', true, 'select', ['Yes','No','Unknown'])}
        {String(values.suspectKnown) === 'Yes' && field('suspectDescription', 'Suspect name, description, or other identifying information', false, 'textarea')}
        {field('vehicleInvolved', 'Was a vehicle involved?', true, 'select', ['Yes','No','Unknown'])}
        {String(values.vehicleInvolved) === 'Yes' && field('vehicleDescription', 'Vehicle plate, state, year, make, model, color, or distinguishing features', false, 'textarea')}
        {field('propertyInvolved', 'Was property stolen, damaged, lost, or otherwise involved?', true, 'select', yesNo)}
        {String(values.propertyInvolved) === 'Yes' && field('propertyDescription', 'Describe the property and estimated value', false, 'textarea')}
        {field('evidenceAvailable', 'Do you have photos, video, screenshots, documents, or other evidence?', false, 'select', yesNo)}
      </> },
      { title: 'Contact', content: <>
        {field('firstName', 'First name', true)}{field('lastName', 'Last name', true)}
        {field('dateOfBirth', 'Date of birth', true, 'date')}
        {field('phone', 'Phone number', true, 'tel')}{field('email', 'Email address', true, 'email')}
        {field('contactAddress', 'Mailing address', true)}
      </> },
      { title: 'Summary', content: <Review values={values} certification /> }
    ];
  }

  if (serviceCode === 'crime-tip') {
    return [
      { title: 'Tip', content: <>
        {field('emergency', 'Is this an emergency?', true, 'select', yesNo)}
        {field('crimeType', 'What type of crime is this tip about?', true, 'select', ['Violent crime','Robbery','Burglary','Drug activity','Weapons / firearm','Vandalism','Wanted person','Threat of violence','Other serious crime'])}
        {field('tipNarrative', 'What do you know?', true, 'textarea', undefined, 'Include names, descriptions, vehicles, places, dates, or other information that may help investigators.')}
      </> },
      { title: 'Location', content: <>
        {field('tipLocation', 'Where did this occur or where can the person/activity be found?', false)}
        {field('tipDateTime', 'When did this occur?', false)}
      </> },
      { title: 'Optional contact', content: <>
        <p className="help">Leave these fields blank if you want to remain anonymous.</p>
        {field('tipName', 'Name')}{field('tipPhone', 'Phone number', false, 'tel')}{field('tipEmail', 'Email address', false, 'email')}
      </> },
      { title: 'Summary', content: <Review values={values} /> }
    ];
  }

  if (serviceCode === 'officer-commendation') {
    return [
      { title: 'Employee', content: <>
        {field('employeeName', 'Officer / employee name', false)}
        {field('badgeNumber', 'Badge or employee number', false)}
        {field('incidentDate', 'Date of interaction', false, 'date')}
        {field('incidentLocation', 'Location of interaction', false)}
      </> },
      { title: 'Commendation', content: <>{field('commendationNarrative', 'Tell us what the employee did', true, 'textarea')}</> },
      { title: 'Contact', content: <>
        {field('firstName', 'First name', true)}{field('lastName', 'Last name', true)}
        {field('phone', 'Phone number', false, 'tel')}{field('email', 'Email address', true, 'email')}
      </> },
      { title: 'Summary', content: <Review values={values} /> }
    ];
  }

  if (serviceCode === 'officer-misconduct') {
    return [
      { title: 'Incident', content: <>
        {field('employeeName', 'Officer / employee name', false)}
        {field('badgeNumber', 'Badge or employee number', false)}
        {field('incidentDate', 'Date of incident', true, 'date')}
        {field('incidentTime', 'Approximate time', false, 'time')}
        {field('incidentLocation', 'Location', true)}
      </> },
      { title: 'Complaint', content: <>
        {field('complaintType', 'Nature of complaint', true, 'select', ['Conduct / courtesy','Use of force','Bias / discrimination','Search / seizure','Arrest / detention','Driving / vehicle operation','Failure to take action','Other'])}
        {field('complaintNarrative', 'Describe what happened', true, 'textarea')}
        {field('witnesses', 'Witness information', false, 'textarea')}
        {field('evidenceAvailable', 'Do you have supporting evidence?', false, 'select', yesNo)}
      </> },
      { title: 'Contact', content: <>
        {field('firstName', 'First name', true)}{field('lastName', 'Last name', true)}
        {field('phone', 'Phone number', true, 'tel')}{field('email', 'Email address', true, 'email')}
        {field('preferredContact', 'Preferred contact method', false, 'select', ['Phone','Email'])}
      </> },
      { title: 'Summary', content: <Review values={values} certification /> }
    ];
  }

  if (serviceCode !== 'rental-registration') {
    return [
      { title: 'Location', content: <>
        {field('location', 'Address or location', true)}
        {field('locationDetails', 'Location details', false, 'textarea', undefined, 'Include an intersection, unit, landmark, direction of travel, or other helpful information.')}
      </> },
      { title: 'Details', content: <>
        {field('description', 'Describe your request', true, 'textarea')}
        {field('dateObserved', 'Date observed', false, 'date')}
      </> },
      { title: 'Contact', content: <>
        {field('firstName', 'First name', false)}{field('lastName', 'Last name', false)}
        {field('phone', 'Phone number', false, 'tel')}{field('email', 'Email address', false, 'email')}
      </> },
      { title: 'Summary', content: <Review values={values} /> }
    ];
  }

  return [
    { title: 'Property', content: <>
      {field('propertyAddress', 'Rental property address', true)}
      {field('buildingName', 'Building / property name', false)}
      {field('unitCount', 'Number of rental units', true, 'number')}
      {field('propertyType', 'Property type', true, 'select', ['Single-family','Duplex / triplex / quadplex','Multifamily building','Condominium / townhome','Other'])}
    </> },
    { title: 'Owner', content: <>
      {field('ownerType', 'Owner type', true, 'select', ['Individual','Business / LLC','Other organization'])}
      {field('ownerName', 'Owner / legal entity name', true)}
      {field('ownerAddress', 'Owner mailing address', true)}
      {field('ownerPhone', 'Owner phone', true, 'tel')}{field('ownerEmail', 'Owner email', true, 'email')}
    </> },
    { title: 'Responsible party', content: <>
      {field('responsiblePartyName', 'Local responsible party / property manager', true)}
      {field('responsiblePartyCompany', 'Company', false)}
      {field('responsiblePartyPhone', 'Phone', true, 'tel')}{field('responsiblePartyEmail', 'Email', true, 'email')}
      {field('responsiblePartyAddress', 'Mailing address', true)}
    </> },
    { title: 'Summary', content: <Review values={values} certification /> }
  ];
}

function Review({ values, certification = false }: { values: FormValues; certification?: boolean }) {
  return <>
    <p>Review the information below before submitting.</p>
    <dl className="review-list">
      {Object.entries(values).filter(([, value]) => String(value).trim()).map(([key, value]) => <div key={key}><dt>{humanize(key)}</dt><dd>{String(value)}</dd></div>)}
    </dl>
    {certification && <label className="check-row"><input type="checkbox" required /> I certify that the information provided is true and accurate to the best of my knowledge.</label>}
  </>;
}

function humanize(value: string) {
  return value.replace(/([A-Z])/g, ' $1').replace(/^./, (char) => char.toUpperCase());
}
