import type { FormValues } from '../models';

type Props = {
  name: string;
  label: string;
  values: FormValues;
  setValues: (next: FormValues) => void;
  required?: boolean;
  type?: 'text' | 'email' | 'tel' | 'date' | 'time' | 'number' | 'textarea' | 'select';
  options?: string[];
  help?: string;
  placeholder?: string;
};

export default function Field({
  name,
  label,
  values,
  setValues,
  required,
  type = 'text',
  options = [],
  help,
  placeholder
}: Props) {
  const value = String(values[name] ?? '');
  const common = {
    id: name,
    name,
    required,
    value,
    onChange: (event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) =>
      setValues({ ...values, [name]: event.target.value })
  };

  return (
    <div className="field">
      <label htmlFor={name}>
        {label} {required && <span className="required">*</span>}
      </label>
      {help && <p className="help">{help}</p>}
      {type === 'textarea' ? (
        <textarea {...common} rows={6} placeholder={placeholder} />
      ) : type === 'select' ? (
        <select {...common}>
          <option value="">Select one</option>
          {options.map((option) => <option key={option}>{option}</option>)}
        </select>
      ) : (
        <input {...common} type={type} placeholder={placeholder} />
      )}
    </div>
  );
}
