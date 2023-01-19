# Postman collection of FHIR queries 

This sample includes collection of FHIR queries which would be helpful to build better understanding of FHIR resources.

The collection could be downloaded and imported into postman.

The queries are categorised into folders below:
- AuthToken (Request to create an authentication token which is used in all other queries)
- Create Starter Bundle (Here we create a multiple resources in one bundle, these resources would be used in queries further)
- Common Queries (This folder has some of the most frequently used queries)
- Common Operations (This folder has queries for operations like convert, valicate, export and import)
- Chained and Reverse Chained Search (This folder has queries to use chaining and reverse chaining for fetching resources, more details about chaining could be found [here](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/overview-of-search#chained--reverse-chained-searching).)
- Include and Reverse Include Search (This folder has queries with _include and _revinclude parameters, more details about [here](https://www.hl7.org/fhir/search.html#return).)
- Custom Search (Create and Use SearchParameter) (This folder has queries related to custom search, here we create new SearchParameter, run reindex and usethe newly create SearchParameter. More details about custom search could be found [here](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/how-to-do-custom-search).)
- List of alphabetically sorted resource specific folders for resource specific queries for CRUD operations

