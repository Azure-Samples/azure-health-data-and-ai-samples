# Set Postman Configuration

- Import the collection [fhir-proxy-smart-client-calls-sample-v2.postman_collection.json](./postman-collection/fhir-proxy-smart-client-calls-sample-v2.postman_collection.json) and environment [apim-smart-env-sample.postman_environment.json](./postman-collection/apim-smart-env-sample.postman_environment.json) file in Postman.
- Update postman enviroment variables according to the smart on fhir deployment.
  - tenantId : Azure tenant where the FHIR service is deployed in. It's located from the Application registration overview menu option.
  - clientId: Application client registration ID.
  - clientSecret: Application client registration secret.
  - fhirurl: `{{apimurl}}/smart`
  - scope: Pass the requested Scopes
  - apimurl: `{{apimurl}}`
  - callbackurl: `https://oauth.pstmn.io/v1/callback`
- To generate a token, navigate to the Authorization tab in the 'fhir-proxy-smart-client-calls-sample-v2' collection and click on the `Get New Access Token`.  
- Use this token to access FHIR resources based on the scopes specified in the environment variable.
- Queries for patient and observation resources are currently added.
