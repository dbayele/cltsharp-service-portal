import { services } from '../forms/definitions';
import type { Audience, ServiceCode } from '../models';

type Props = { audience: Audience; onSelect: (code: ServiceCode) => void };

export default function ServiceCatalog({ audience, onSelect }: Props) {
  const items = services.filter((service) => service.audience === audience || service.audience === 'both');
  const categories = Array.from(new Set(items.map((item) => item.category)));

  return (
    <section aria-labelledby={`${audience}-heading`}>
      <div className="section-heading">
        <p className="eyebrow">{audience === 'resident' ? 'For residents' : 'For businesses & property owners'}</p>
        <h2 id={`${audience}-heading`}>{audience === 'resident' ? 'Resident requests' : 'Business requests'}</h2>
      </div>
      {categories.map((category) => (
        <section key={category} className="catalog-group">
          <h3>{category}</h3>
          <div className="service-grid">
            {items.filter((service) => service.category === category).map((service) => (
              <button className="service-card" key={service.code} onClick={() => onSelect(service.code)}>
                {service.badge && <span className="badge">{service.badge}</span>}
                <strong>{service.title}</strong>
                <span>{service.summary}</span>
                <span className="card-link">Start request →</span>
              </button>
            ))}
          </div>
        </section>
      ))}
    </section>
  );
}
