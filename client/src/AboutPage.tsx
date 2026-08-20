type Props = { onBack: () => void };

export default function AboutPage({ onBack }: Props) {
  return (
    <main className="form-shell about-page">
      <button className="back-link" onClick={onBack}>← Back to CLT# services</button>
      <p className="eyebrow">About CLT#</p>
      <h1>How the Charlotte service platform is designed</h1>
      <p className="lede">CLT# is a prototype digital service platform for the City of Charlotte, North Carolina. It separates public-facing intake from employee processing and isolates higher-sensitivity Police, Fire, and Airport workloads at both the API and database layers.</p>

      <section className="about-card">
        <h2>Application architecture</h2>
        <div className="architecture-grid">
          <article><h3>Resident & business portal</h3><p>A React + TypeScript single-page application provides the service catalog, structured forms, Auth0 resident accounts, My Requests, and Charlotte Water payments through Stripe.</p></article>
          <article><h3>Employee operations</h3><p>A completely separate React application gives authorized City staff All, General, Police, Fire, and Airport queues with assignment, status, internal notes, and request-detail tools.</p></article>
          <article><h3>Resident API</h3><p>An ASP.NET Core API accepts service submissions, captures trusted network metadata, exposes account-owned request history, and performs server-side Stripe and billing integration.</p></article>
          <article><h3>Employee API</h3><p>A separate ASP.NET Core API serves only workforce processing functions. Auth0 permissions determine which department databases an employee may read or update.</p></article>
        </div>
      </section>

      <section className="about-card">
        <h2>Data separation</h2>
        <p>CLT# routes requests server-side. Browsers never choose a database. The production design uses four isolated PostgreSQL data stores:</p>
        <div className="db-grid">
          <article><strong>General</strong><span>Streets, solid waste, zoning, neighborhoods, rental registration, resident profiles, login audits, and water-payment audit data.</span></article>
          <article><strong>Police</strong><span>Crime reports and tips, commendations, complaints, records, alarms, traffic concerns, and other CMPD workflows.</span></article>
          <article><strong>Fire</strong><span>Fire hazards, inspections, permits, plan review, hydrants, system impairments, reports, and prevention workflows.</span></article>
          <article><strong>Airport</strong><span>CLT Airport passenger, community, parking, accessibility, business, badging, vendor, and Aviation Department requests.</span></article>
        </div>
        <p>Crime tips remain intentionally anonymous and are not associated with an Auth0 resident subject, even when a user is already signed in.</p>
      </section>

      <section className="about-card">
        <h2>Recommended AWS deployment</h2>
        <pre className="architecture-diagram" aria-label="AWS architecture diagram">{`Internet (U.S. only)
        |
Route 53 + ACM
        |
CloudFront + AWS WAF
   |                 |
Resident UI       Employee UI
S3 private origin S3 private origin
   |                 |
   +------ HTTPS / API ------+
              |
      Application Load Balancers
         |                 |
 Resident API         Employee API
 ECS Fargate          ECS Fargate
         \\               /
          \\ private VPC /
           RDS PostgreSQL
 General | Police | Fire | Airport

External trust services: Auth0 identity + Stripe payments`}</pre>
        <h3>Edge and DNS</h3>
        <p><strong>Amazon Route 53</strong> provides DNS and <strong>AWS Certificate Manager</strong> provides TLS certificates. Separate <strong>CloudFront</strong> distributions serve the resident and employee applications from private <strong>Amazon S3</strong> origins using origin access control.</p>
        <h3>U.S.-only access</h3>
        <p><strong>AWS WAF</strong> is attached at the edge with a geographic allow rule for the United States, managed threat rules, and rate limits. The .NET APIs retain their own country/IP validation as defense in depth. Only trusted edge/load-balancer forwarding headers are accepted for audit IP capture.</p>
        <h3>APIs and networking</h3>
        <p>The two .NET 8 APIs are packaged as independent containers in <strong>Amazon ECR</strong> and run as separate <strong>Amazon ECS on AWS Fargate</strong> services across multiple Availability Zones. Application Load Balancers terminate application traffic; tasks and databases stay in private subnets inside a VPC. Security groups allow database traffic only from the API services that require it.</p>
        <h3>PostgreSQL</h3>
        <p>Use separate <strong>Amazon RDS for PostgreSQL</strong> deployments for General, Police, Fire, and Airport data, with Multi-AZ high availability, encryption at rest, automated backups, point-in-time recovery, and SSL/TLS database connections. Database users are distinct for resident and employee APIs and should receive least-privilege grants.</p>
        <h3>Identity and payments</h3>
        <p><strong>Auth0</strong> remains the identity provider. Resident and employee SPAs use separate Auth0 applications and the two APIs use separate audiences. <strong>Stripe</strong> handles card and Apple Pay data; CLT# stores Stripe identifiers and payment audit metadata rather than card numbers or CVC values.</p>
        <h3>Secrets, audit, and observability</h3>
        <p><strong>AWS Secrets Manager</strong> stores database credentials and API secrets, with <strong>AWS KMS</strong> encryption. Application and infrastructure logs flow to <strong>Amazon CloudWatch</strong>; <strong>AWS CloudTrail</strong> records AWS control-plane activity. Production deployments should add alarms, centralized retention policies, and security monitoring appropriate to each department.</p>
      </section>

      <section className="about-card">
        <h2>Deployment boundaries</h2>
        <ul className="about-list">
          <li>The resident UI, employee UI, resident API, and employee API are independently deployable.</li>
          <li>Police, Fire, and Airport data are not stored in the General service-request database.</li>
          <li>Employee permissions are evaluated by department before a database is queried.</li>
          <li>Production infrastructure should be defined as code (for example AWS CDK, CloudFormation, or Terraform) and reviewed under City security, records-retention, PCI, and public-safety requirements.</li>
        </ul>
      </section>
    </main>
  );
}
