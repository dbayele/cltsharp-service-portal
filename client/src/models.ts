export type Audience = 'resident' | 'business';

export type ServiceCode = string;

export interface ServiceDefinition {
  code: ServiceCode;
  audience: Audience | 'both';
  category: string;
  title: string;
  summary: string;
  badge?: string;
  publicSafety?: boolean;
  customForm?: boolean;
}

export interface SubmitResult {
  submissionNumber: string;
  receivedAtUtc: string;
  status: string;
}

export interface MyRequest {
  submissionNumber: string;
  serviceCode: string;
  receivedAtUtc: string;
  updatedAtUtc: string;
  status: string;
}

export type FormValues = Record<string, string | boolean | string[]>;
