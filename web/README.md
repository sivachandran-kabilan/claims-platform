# APAC Claims web app

Angular 22 (standalone components, signals, lazy routes). Run steps, accounts and design notes are in the root `README.md`.

```bash
npm ci
npx ng serve            # http://localhost:4200, API at environment.apiBaseUrl (http://localhost:5000)
npm run gen:api         # regenerate src/app/api from ../docs/openapi.json (committed; do not edit by hand)
npx ng build            # production build into dist/
```
