import type { FormValues, ServiceCode, SubmitResult } from './models';

export async function submitServiceRequest(serviceCode: ServiceCode, data: FormValues, accessToken?: string): Promise<SubmitResult> {
  const response = await fetch('/api/service-requests', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}) },
    body: JSON.stringify({ serviceCode, data })
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new Error(problem?.detail ?? 'Unable to submit your request. Please review your information and try again.');
  }
  return response.json();
}
