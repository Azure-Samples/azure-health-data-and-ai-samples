# Add an App Registration through the Azure Portal

## Register a new application
1. Go to the [Azure portal](https://portal.azure.com/#home)

2. Select Show portal menu
![Show portal](./app-reg-images/2-show-portal.png)


3. Select Azure Active Directory
![Azure active directory](./app-reg-images/3-azure-active-directory.png)


4. Select App registrations
![App registrations](./app-reg-images/4-app-registrations.png)


5. Select + New registration
![New registrations](./app-reg-images/5-new-registration.png)


6. Type a name for the application in the Name field, for example, "rsnaAppReg"
![App name](./app-reg-images/6-app-name.png)


7. Select Register
![Register app](./app-reg-images/7-register-app.png)


8. Copy Application (client) ID to clipboard; save it in a text file
![Copy app ID](./app-reg-images/8-copy-app-id.png)


9. Copy Directory (tenant) ID to clipboard; save it in a text file
![Copy tenant ID](./app-reg-images/9-copy-tenant-id.png)


10. Select Certificates & secrets
![Certificates and secrets](./app-reg-images/10-certs-and-secrets.png)


11. Select + New client secret
![New client secret](./app-reg-images/11-new-client-secret.png)


12. Type a description for the secret, for example, "rsnaSecret"
![Secret description](./app-reg-images/12-secret-description.png)


13. Select the Expires dropdown field
![Secret expiration](./app-reg-images/13-secret-expiration.png)


14. Select 3 months

15. Select Add

16. Copy the secret Value to clipboard; save it in a text file
![Copy secret value](./app-reg-images/16-copy-secret-value.png)

17. Select Home
![Home](./app-reg-images/17-home.png)

## Get the Principal Object ID of the App Registration
1. Open a Command Line interface with Azure CLI support, or use the Cloud Shell in the Azure portal.
2. Type `az login` and log in with the appropriate user.
3. Type `az ad sp list --filter "displayName eq 'your_app_name'"` to get the pricipal object ID of the managed identity that underlies the app registration.
4. In the resulting JSON output copy the ID to a secure place that can be referred to later. 