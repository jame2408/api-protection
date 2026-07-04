# api-protection

Multi-tenant API key management backend (.NET 10, DDD bounded contexts, BDD-driven).

## Local Development

The development connection string in `backend/src/Host/appsettings.Development.json` intentionally omits the database password. Provide it once per machine via User Secrets:

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Database=apikeymgmt_dev;Username=postgres;Password=<你的本機密碼>" --project backend/src/Host
```

Functional tests are unaffected — they spin up their own database via Testcontainers.
