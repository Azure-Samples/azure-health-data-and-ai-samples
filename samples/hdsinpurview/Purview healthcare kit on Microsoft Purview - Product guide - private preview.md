> MICROSOFT CLOUD FOR HEALTHCARE - PURVIEW HEALTHCARE KIT
>
> 
>
> Product guide/onboarding document for private preview (August 2024 release)  
> 
>
>   
> *Content is Microsoft confidential*
- [Scope and Purpose ](#scope-and-purpose)
- [Target audience](#target-audience)
- [Customer onboarding process ](#customer-onboarding-process)
- [Overview ](#overview)
  - [Sensitive information types ](#sensitive-information-types)
  - [Glossaries for healthcare in the Microsoft Purview data catalog
    ](#glossaries-for-healthcare-in-the-microsoft-purview-data-catalog)
- [Architecture overview ](#architecture-overview)
- [Deployment guide ](#deployment-guide)
  - [Overview ](#overview-1)
  - [Deployment mechanism](#deployment-mechanism)
  - [Post installation instructions](#post-installation-instructions)
- [Limitations of the Purview healthcare
  kit](#limitations-of-the-purview-healthcare-kit)
- [Product deployment support ](#product-deployment-support)
- [Troubleshooting ](#troubleshooting)
  - [Common Azure deployment errors](#common-azure-deployment-errors)
  - [Common pre-deployment script
    errors](#common-pre-deployment-script-errors)
  - [Common purview healthcare kit installation
    errors:](#common-purview-healthcare-kit-installation-errors)
  - [View deployment outcome ](#view-deployment-outcome)
  - [Contact Microsoft support team](#contact-microsoft-support-team)
- [ Appendix](#appendix)
  - [Resources created as part of
    pre-deployment](#resources-created-as-part-of-pre-deployment)
  - [Manual pre-deployment steps](#manual-pre-deployment-steps)
  - [ Understanding permissions in Microsoft
    Purview](#understanding-permissions-in-microsoft-purview)
  - [ Understanding common concepts in Microsoft
    Purview](#understanding-common-concepts-in-microsoft-purview)

# Scope and Purpose 

> The scope of the document is to provide details about the Purview
> healthcare kit and to guide the reader through the installation and
> use of the product.
>
> In accordance with Private Preview requirements, this document will
> not be published on Microsoft’s public documentation site but will be
> made available to Private Preview participants as a PDF document.
>
> This document is to be solely used by the participating organizations
> for the purposes of installing and using the Purview healthcare kit,
> and to provide feedback to the Microsoft Cloud for Healthcare Product
> team on the features of the product, functionality, ease of use and
> opportunities to improve the current offering.
>
> **Do not use this private preview in production environments.** The
> included features are exclusively for private preview use in
> non-production environments and are only supported for such usage.

# Target audience

> This user-guide is intended for use by customers and partners
> participating in the private preview of the Purview healthcare kit.
> The private preview includes Sensitive Information Type based
> classifications and the new Glossary Terms experience.

# Customer onboarding process 

The Purview healthcare kit is available in private preview starting
September 1st, 2024. To install the Purview healthcare kit, please
complete the [onboarding
form](https://forms.office.com/Pages/ResponsePage.aspx?id=v4j5cvGGr0GRqy180BHbR4BVOvi5iVJJlHOO_p0-r0FUQ1g2NkdCREFHOVowRzJCV0pPSk9OSUhNTS4u)
shared by the Microsoft team during the onboarding process. The
information requested will include your "Subscription ID", "Tenant ID",
and "Purview Account Name" to enable your subscription to access the
Azure Marketplace offer in your tenant.

# Overview 

Purview healthcare kit is a collection of healthcare-focused templates
built on top of the new unified Microsoft Purview, that aims to offer
comprehensive healthcare data governance and compliance solutions. With
these capabilities, healthcare organizations can better enable data
discovery, data cataloguing and data understanding. The private preview
includes components for sensitive information types and glossaries,
detailed in the sections below.

## Sensitive information types 

Sensitive information types (SITs) allow customers to identify and
classify data based on predefined rules and patterns. The private
preview offers ready-to-run SITs built specifically for healthcare,
curated based on the HIPAA safe harbor guidelines. With support for
identifiers like date of admission/discharge, US vehicle identification
number, and non-US identifiers such as European health insurance card
number, healthcare organizations can effectively recognize Protected
Health Information (PHI) from their data estate and ensure that
sensitive information is properly classified and catalogued.

## Glossaries for healthcare in the Microsoft Purview data catalog 

In healthcare, terms and definitions vary between departments,
potentially leading to misunderstandings and errors. Glossaries in
Purview ensures that all departments, users, and stakeholders in an
organization have a consistent understanding about the meaning of
different data and business terms.

The glossaries discussed in the following sections are available
exclusively in English.

### FHIR glossary 

The private preview includes a comprehensive FHIR glossary that resides
within the ‘Clinical’ business domain. It is curated based on the HL7
FHIR R4 version.

### DICOM glossary 

The private preview includes two comprehensive Digital Imaging and
Communications in Medicine (DICOM) glossaries:

1.  DICOM Data elements, residing within the ‘Imaging – DICOM Tags’
    business domain

2.  DICOM Value representations (VR), residing in the ‘Imaging – DICOM
    VR’ business domain

These glossaries equip the user with a detailed understanding of the
definitions of various DICOM data elements.

# Architecture overview 

<img src="media/image3.png" style="width:5.47152in;height:3.57851in" />

After the Purview healthcare kit deployment is complete, users can use
the SITs and Glossary terms as follows:

1.  Data Map - users can set up Purview to register different data
    sources to run data scans and classify their data with the
    healthcare SITs. The healthcare SITs utilize regular expression and
    word list matching for data classification. The SIT configurations
    can be changed to meet business specific needs. Refer to [Scans and
    ingestion](https://learn.microsoft.com/en-us/purview/concept-scans-and-ingestion)
    for more details.

2.  Data Catalog - users can also use the deployed FHIR glossary and
    DICOM glossary to associate the terms with the relevant data assets.
    They can be searched using SITs, terms or labels as needed for
    easier data discovery. Refer to [business
    glossary](https://learn.microsoft.com/en-us/purview/concept-business-glossary)
    for more details.

# Deployment guide 

> The following sections of this document will detail the deployment
> process for the Purview healthcare kit.

## Overview 

The Purview healthcare kit is available to private preview customers as
a private offer in the Azure Marketplace. This section details the
necessary prerequisites and procedures for deploying and beginning to
use the Purview healthcare kit.

## Deployment mechanism

The deployment of the Purview healthcare kit involves two primary steps:

1.  Pre-deployment steps need to be followed to set up the necessary
    Azure resources. These steps can be done manually or automatically
    using the provided PowerShell script, which will be detailed in
    subsequent sections.

2.  Following the pre-deployment steps, the Purview healthcare kit is
    deployed through an Azure Marketplace offer.

> The Purview healthcare kit is installed via an Azure Marketplace
> Offer. Upon deployment, an authentication token is acquired using
> Entra ID, followed by the import of Sensitive Information Types (SITs)
> and Glossary terms.

When the Purview healthcare kit is deployed, it creates healthcare
specific Sensitive Information Types (SITs) using Security & Compliance
PowerShell and creates FHIR and DICOM glossary terms using Purview REST
APIs. Certificate-based authentication is used during the offer
installation for both scenarios.

The following diagram illustrates the deployment mechanism:

<img src="media/image4.png" style="width:6.29392in;height:4.49902in"
alt="A diagram of a process Description automatically generated" />

### Pre-deployment

> Before deploying the Azure Marketplace offer, certain Azure
> dependencies must be established to enable app-only authentication for
> unattended scripts and automation. Detailed information about these
> dependencies can be found in [App-only authentication in Exchange
> Online PowerShell and Security & Compliance PowerShell \| Microsoft
> Learn](https://learn.microsoft.com/en-us/powershell/exchange/app-only-auth-powershell-v2?view=exchange-ps).
> To streamline this setup, a ‘pre-deployment’ PowerShell script is
> provided and will be explained in the [subsequent
> sections](#_Pre-deployment_process_(deployment).
>
> Should you choose not to utilize the pre-deployment script, please
> consult the manual steps provided in the
> [appendix](#_10.1_Manual_pre-deployment) for guidance on manually
> configuring the pre-deployment resources.

####  Pre-deployment process (administrative user)

> Prior to beginning, an administrator should verify that the deployment
> user executing the pre-deployment script has been granted the
> necessary minimal privileges in Azure, and that Azure subscription has
> the necessary resource providers enabled. This ensures that the script
> can carry out the required actions under the user's account. Use the
> following steps to verify these privileges.

1.  The user will need to be able to register an application and assign
    permissions to it. This can be configured using either of the
    following approaches:

    1.  Assign the ‘Cloud Application Administrator’ role to the user.
        This option restricts registering applications to only the users
        assigned to this role.

    2.  In your Entra ID tenant, enable the "**Users can register
        applications**" setting in the "User settings" section. This
        option is less restrictive and allows all users to register
        applications.<img src="media/image5.png" style="width:5.63542in;height:1.19792in" />

2.  The user will need to be able to create a user assigned managed
    identity in a resource group. This can be configured using either of
    the following approaches:

    1.  Assign the ‘Contributor’ role to the user in an existing
        resource group. This option restricts the user to only manage
        resources inside the specific resource group.

    2.  Assign the ‘Contributor’ role to the user at the subscription
        level to allow them to create their own resource group. This
        option is less restrictive and allows the user to create their
        own resource groups and access other resources in the
        subscription.

3.  The user will need to be able to assign roles to the user assigned
    managed identity and app registration, and grant admin consent to
    the permissions assigned to the application. To allow for this:

    1.  Assign the ‘Privileged Role Administrator’ role to the user

4.  The Azure subscription will need to have the
    ‘Microsoft.ManagedIdentity’ resource provider registered. Please
    follow the steps at [Resource provider registration errors - Azure
    Resource Manager \| Microsoft
    Learn](https://learn.microsoft.com/en-us/azure/azure-resource-manager/troubleshooting/error-register-resource-provider?tabs=azure-portal)
    to verify the registration status of this resource provider and
    register it if it is not registered.

#### Pre-deployment process (deployment user)

> With the dependencies in place, the pre-deployment script can be run
> by following these steps:
>
> Reminder: Should you choose not to utilize the pre-deployment script
> as described in the following instructions, please consult the manual
> steps provided in the [appendix](#_10.1_Manual_pre-deployment) for
> guidance on manually configuring the pre-deployment resources.

1.  Download the pre-deployment PowerShell script from the GitHub
    repository.  
    <img src="media/image6.png" style="width:2.18781in;height:0.47923in" />

2.  Locate the file in File Explorer and right-click on it. Select
    Properties, navigate to the General tab, and within the security
    section, unblock the file by ticking the Unblock checkbox.

<img src="media/image7.png" style="width:3.61509in;height:1.57314in" />

3.  Open Windows PowerShell  
    <img src="media/image8.png" style="width:3.59425in;height:0.71885in" />

4.  Use Windows PowerShell 5.1. You can check your version using the
    command:

\$PSVersionTable.PSVersion

Note: PowerShell 6 and above is not compatible with the PowerShell
cmdlets used in the script.

5.  New PowerShell users should configure the execution policy to allow
    running local scripts by using the following command:

Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

6.  Navigate to the folder location where the PowerShell script is
    downloaded and run it with the command:

.\preDeploymentScript.ps1

7.  Enter the required parameters to create the necessary Azure
    resources. If a parameter refers to a new resource, the resource
    will be created with that name. If a parameter value refers to an
    existing resource, the existing resource will be reused.

<table>
<colgroup>
<col style="width: 28%" />
<col style="width: 71%" />
</colgroup>
<thead>
<tr>
<th><strong>Parameter</strong></th>
<th><strong>Description</strong></th>
</tr>
</thead>
<tbody>
<tr>
<td>ResourceGroupName</td>
<td><p>The resource group name for creating deployment-dependent
resources.<br />
<br />
example: rg-purview-deploy</p>
<p>Refer to the <strong>resourcegroups</strong> entity for naming
restrictions at <a
href="https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/resource-name-rules#microsoftresources">https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/resource-name-rules#microsoftresources</a>.</p></td>
</tr>
<tr>
<td>AppRegistrationName</td>
<td><p>The display name of the app registration designated for Purview
authentication</p>
<p>example: app-purview-deploy</p></td>
</tr>
<tr>
<td>ManagedIdentityName</td>
<td><p>The name of the user assigned managed identity for running
PowerShell deployment scripts<br />
<br />
example: mi-purview-deploy</p>
<p>Refer to the <strong>userAssignedIdentities</strong> entity for
naming restrictions at <a
href="https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/resource-name-rules#microsoftmanagedidentity">https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/resource-name-rules#microsoftmanagedidentity</a>.</p></td>
</tr>
<tr>
<td>Location</td>
<td><p>A valid Azure location where resources will be created.<br />
<br />
Use the following command to get the list of allowed values:</p>
<ul>
<li><p>Get-AzLocation | select location</p></li>
</ul>
<p>example: westus</p></td>
</tr>
<tr>
<td>SubscriptionId</td>
<td><p>The ID for the subscription in which resources will be
created.</p>
<p>You can find this ID in the Azure portal on the Subscription’s
overview page.</p>
<p>Important: Ensure that the deployment user has access to the
subscription. You can check this by verifying they can see the
subscription ID in the Azure portal.</p>
<p><img src="media/image9.png"
style="width:4.30694in;height:0.96319in" /></p></td>
</tr>
</tbody>
</table>

8.  Enter login credentials when prompted. This may occur multiple times
    due to the use of multiple PowerShell modules.

9.  Once the script execution is complete, it will display the resource
    names required for deploying the Azure Marketplace offer. Keep a
    copy of this output for future reference.

> If you encounter any issues, please refer to the [troubleshooting
> section](#_Common_pre-deployment_script).

### Azure Marketplace offer installation

> To install the Purview healthcare kit in your tenant, follow the steps
> in the upcoming sections.
>
> Currently, in this private preview, only fresh installations of the
> Purview healthcare kit for Microsoft Purview (SaaS) are supported.
> Update and upgrade scenarios will be supported during our public
> preview phase.

#### Prerequisites for Azure Marketplace offer installation (administrative user)

> To successfully deploy the Azure Marketplace offer and manage user
> data after deployment, specific permissions must be granted in
> advance. Please make sure these prerequisites are met in order to
> complete the steps in this section.

1.  An existing Microsoft Purview account is available at
    <https://purview.microsoft.com>

2.  The administrative user has access to the Data Catalog’s Roles and
    permissions page

**App Registration: Business Domain Creator role**

To ensure the app registration created by the pre-deployment script can
import glossary terms, follow these steps to manually grant it
privileges.

In the [Purview portal](https://purview.microsoft.com), as a user with
the Data Governance Administrator role:

1.  Navigate to Data Catalog \> Roles and permissions

2.  Add the app registration to the Business Domain Creator role

<img src="media/image10.png" style="width:6.30208in;height:3.46875in" />

> To allow the user responsible for managing the SITs and glossaries to
> manage their roles in the newly created business domains, add the user
> to the Data Governance role group in order to join the tenant-level
> Data Governance Administrator role.

1.  Navigate to Settings \> Roles and scopes \> Role groups

2.  Add the user to the Data Governance group.

<img src="media/image11.png" style="width:6.30486in;height:3.33681in"
alt="A screenshot of a computer Description automatically generated" />

> **Register resource providers**
>
> The Azure subscription will need to have the following resource
> providers registered:

1.  Microsoft.Compute

2.  Microsoft.ContainerInstance

3.  Microsoft.KeyVault

4.  Microsoft.Network

> Please follow the steps at [Resource provider registration errors -
> Azure Resource Manager \| Microsoft
> Learn](https://learn.microsoft.com/en-us/azure/azure-resource-manager/troubleshooting/error-register-resource-provider?tabs=azure-portal)
> to verify the registration status of these resource providers and
> register them if they are not registered.

#### Resources created as part of the deployment

> In the following sections, when the deployment user starts the Azure
> Marketplace offer deployment, new resources will be added to the
> specified resource group to facilitate Purview authentication for
> importing SITs and glossaries. These resources are needed solely
> during the deployment phase and can be manually deleted afterward. We
> intend to automate this cleanup process in a future release.

| **Resource** | **Purpose** | **Managed Identity Permissions** |
|----|----|----|
| Key Vault | For the secure storage of a self-signed certificate. | Contributor role at the resource group |
| Self-signed Certificate | To be used for certificate-based authentication during the import of Sensitive Information Types and Glossary terms. | Cloud application Administrator to upload the self-signed certificate in the app registration. |

#### Deployment of the Purview healthcare kit (deployment user)

The Purview healthcare kit will be accessible in the Azure Marketplace
as a private offer, once the customer’s Azure subscription is
allow-listed by the Purview healthcare kit team.

Follow the steps below to deploy the marketplace offer. The deployment
process should take around 15 minutes to complete.

1.  Launch the [<u>Azure portal</u>.](https://portal.azure.com/)

2.  Go to the Marketplace section.

3.  Click on “View private plans”

> <img src="media/image12.png" style="width:4.875in;height:3.65021in" />

4.  Select the “Purview healthcare kit (preview)” tile.

5.  When the product tile has opened, click on the drop down in the
    plans and click on “Purview healthcare kit (preview) on Microsoft
    Purview” and the “Create” button.

6.  Follow the on-screen instructions to complete the required fields
    and start the deployment (see the table below). Note: remember to
    refer to the pre-deployment script's output for the necessary
    values.

| **Parameter** | **Description** |
|----|----|
| Resource group | Resource group from the pre-deployment script. |
| User assigned managed identity | User assigned managed identity name from the pre-deployment script. |
| App registration name | App registration name from the pre-deployment script. This field will auto-populate the dropdown options in the next field. |
| Application (client) ID | App registration client id from the pre-deployment script. |
| Business domain owner name | Optional – supply the user who will manage the glossary terms in the business domains. This field will auto-populate the dropdown options in the next field. |
| Business domain owner ID | Business domain owner id. |

The deployment progress will be visible via the Azure Portal
notifications.

If you run into any issues, please refer to the [Purview healthcare kit
troubleshooting
section](#common-purview-healthcare-kit-installation-errors).

After the deployment is finished, the Purview healthcare kit artifacts
will be accessible in your Purview account.

## Post installation instructions

This section offers guidelines on how to begin using the features
included in the Purview healthcare kit.

### Administrator's guide for providing user access to the Purview portal

An administrator with the necessary privileges in the corresponding
Purview solutions will need to provide access to users who will utilize
those specific Purview features.

**Feature: Information Protection**

To allow users to verify the installation of the Sensitive Information
Types, an administrator will need to add the users to the following
roles by navigating to Settings \> Roles and scopes \> Role groups:

1.  Information Protection Admins

**Feature: Data Map**

To allow users to perform scans, an administrator with the Domain Admin
role will need to add the users to the following roles by navigating to
Data Map \> Domains \> Role assignments:

1.  Collection admins

2.  Data source admins

To learn more about permissions, please refer to the
[appendix](#_10.2_Understanding_permissions).

### User's guide to getting started with the Purview portal

These instructions will guide new users in locating the features of the
Purview healthcare kit.

1.  Open [Microsoft Purview](https://purview.microsoft.com) and login.

2.  After logging in successfully, you'll see various modules in
    Purview, such as the Data Map, Data Catalog, and Information
    Protection.

3.  Go to Information Protection \> Classifiers \> Sensitive information
    types to locate the new artifacts. The new Sensitive information
    types have the publisher 'Purview healthcare kit'.

4.  Go to Data Catalog \> Data Management \> Business Domains to locate
    the new domains: Clinical, Imaging - DICOM VR, and Imaging - DICOM
    Tags for glossary terms.

> Users can confirm the installation of the Glossaries by assigning
> themselves to the following roles within each Business Domain. This
> self-assignment is possible because they were previously included in
> the Data Governance role at the tenant level.  
>   
> Note: Refresh the Business domains web page after completing
> self-assignments to view the glossary terms.

1.  Data Steward

    - Required to see Glossary terms

2.  Data Product owner

    - Required to create data products and assign glossary terms to them

# Limitations of the Purview healthcare kit

Being in a private preview phase, some features have known limitations
and bugs, which will be fixed in upcoming releases. The current issues
to note are:

1.  After the Purview healthcare kit is installed, any additional
    Sensitive Information types created manually will have the Purview
    healthcare kit listed as the publisher, rather than the publisher
    associated with the local tenant.

2.  When you install the Purview healthcare kit, remove all Sensitive
    Information Types, reinstall the kit, and then conduct a data scan,
    the reinstalled sensitive information types will display as GUIDs
    rather than their display names.

3.  When redeploying, ensure you use the same app registration as in the
    original deployment; using different app registrations is not
    allowed and will cause an error.

4.  Due to product limitation, only 200 business domains are allowed in
    Data Catalog. Ensure you have fewer than 196 domains to guarantee a
    successful installation.

# Product deployment support 

> If you face any problems, discover bugs, or need assistance with the
> artifacts given in this private preview, please email
> <hdspurviewsupport@microsoft.com>. Note that this procedure is meant
> to address issues specific to the Purview healthcare kit private
> preview. For general issues with Microsoft Purview, please use the
> official Microsoft Purview support channels. We will notify you of any
> updates or changes to this support system.
>
> When contacting the Purview healthcare kit team for support, please
> supply as many of the following pieces of information as part of your
> request:

1.  A description of what step during the deployment process the error
    was encountered.

2.  Copy the error message logs from the pre-deployment script.

3.  Copy the error message from the deployment failure page. Please see
    the below screen shot for the location of the error message.

<img src="media/image13.png" style="width:6.30486in;height:2.66736in" />

4.  The Azure Subscription ID used when performing the deployment.

5.  The number of attempts that have been performed.

6.  Troubleshooting efforts that have already been taken.

# Troubleshooting 

If your deployment from Azure Marketplace encounters issues, this
troubleshooting section is here to assist you in pinpointing and
addressing typical problems that might occur during the implementation
of Azure Marketplace offers for Sensitive Information Types and
Glossaries.

## Common Azure deployment errors

If the next sections don't resolve your issue, please check these
resources on common deployment errors and error codes:

1.  [Deployment
    Errors](https://learn.microsoft.com/en-us/azure/azure-resource-manager/troubleshooting/common-deployment-errors)

2.  [Error
    Codes](https://learn.microsoft.com/en-us/azure/azure-resource-manager/troubleshooting/find-error-code?tabs=azure-portal#deployment-errors)

## Common pre-deployment script errors

While executing the pre-deployment script, you might encounter some
typical misconfiguration issues. Check the output logs for any error
messages and refer to the table below for recommended solutions.

Please be aware that the names of resource groups, users, and similar
identifiers have been substituted with generic terms in the items listed
below.

<table>
<colgroup>
<col style="width: 44%" />
<col style="width: 25%" />
<col style="width: 29%" />
</colgroup>
<thead>
<tr>
<th><strong>Error Details</strong></th>
<th><strong>Meaning</strong></th>
<th><strong>Mitigation</strong></th>
</tr>
</thead>
<tbody>
<tr>
<td><p>New-AzResourceGroup : The client '<a
href="mailto:user@contoso.com">user@contoso.com</a>' with object id
<em>'guid'</em> does not have authorization to perform action
'Microsoft.Resources/subscriptions/resourcegroups/write' over scope
'/subscriptions/<em>subscriptionId</em>/resourcegroups/<em>resourcegroupname'</em>
or the scope is invalid. If access was recently granted, please refresh
your credentials.</p>
<p>StatusCode: 403</p>
<p>ReasonPhrase: Forbidden</p></td>
<td>User cannot create resource group in the subscription</td>
<td>Add the user as the contributor to the resource group or
subscription, depending on the intended level of access to grant the
user.</td>
</tr>
<tr>
<td><p>Add-AzureADDirectoryRoleMember : Error occurred while executing
AddDirectoryRoleMember</p>
<p>Code: Authorization_RequestDenied</p>
<p>Message: Insufficient privileges to complete the operation.</p></td>
<td>User does not have permissions to add app registration to the roles
in Microsoft Entra ID</td>
<td>Add the user to the role “Privileged Role Administrator”</td>
</tr>
</tbody>
</table>

## Common purview healthcare kit installation errors:

While running the Azure Marketplace offer deployment you might encounter
some typical misconfiguration issues. Check the output logs for any
error messages and refer to the table below for recommended solutions.

Please be aware that the names of resource groups, users, and similar
identifiers have been substituted with generic terms in the items listed
below.

<table>
<colgroup>
<col style="width: 43%" />
<col style="width: 27%" />
<col style="width: 29%" />
</colgroup>
<thead>
<tr>
<th><strong>Error Details</strong></th>
<th><strong>Meaning</strong></th>
<th><strong>Mitigation</strong></th>
</tr>
</thead>
<tbody>
<tr>
<td>The term ‘Get-DlpSensitiveInformationTypeRulePackage’ is not
recognized as a name of a cmdlet, function, script file, or executable
program. \n Check the spelling of the name, or if a path was included,
verify that the path is correct and try again.</td>
<td>The App Registration does not have the correct permissions.</td>
<td>The App Registration needs to have Compliance Admin and Exchange
Admin role in Microsoft Entra ID.</td>
</tr>
<tr>
<td>Object reference not set to an instance of an object.\n at
Microsoft.Exchange.Managemnet.ExoPowershellSnapin.GetEXOBanner.ProcessRecord()\r\nat
Connect-ExchangeOnline&lt;Process&gt;,
/root/.local/share/powershell/Modules/ExchangeOnline
Management/3.5.0/netcore/ExchangeOnlineManagement.psm1</td>
<td>Admin consent not granted to the App registration</td>
<td><p>The user who performed the pre-deployment steps needs to grant
admin consent to the permissions on the app registration.</p>
<p>Users with the ‘Privileged Role Administrator’ role can grant consent
to the App Registration.</p></td>
</tr>
<tr>
<td>First
error:\r\nMicrosoft.Azure.KeyVault.Models.KeyVaultErrorException:
Operation returned an invalid status code ‘Forbidden\nMessage: Operation
get is not allowed on a disabled secret.</td>
<td>Managed Identity does not have the appropriate role associated to
it.</td>
<td>The user who performed the pre-deployment steps needs to assign the
“Cloud Application Admin” role to the managed Identity.<br />
<br />
Users with the ‘Privileged Role Administrator’ can manage this role
assignment.</td>
</tr>
<tr>
<td>The resource
‘Microsoft.ManagedIdentity/userAssignedIdentities/&lt;user-assigned-managed-identity-name&gt;’
user the resource group &lt;resource group&gt; was not found</td>
<td>The user assigned managed identity selected in the marketplace offer
does not exist in the resource group that was provided.</td>
<td>Ensure the user assigned managed identity is located inside the
resource group selected during the marketplace offer.</td>
</tr>
<tr>
<td>Forbidden 403</td>
<td>Business Domain Creator role is not assigned to the App
Registration</td>
<td>In the Purview portal, assign the business domain creator role to
the app registration specified in the input parameters.</td>
</tr>
</tbody>
</table>

## View deployment outcome 

Deployment errors that occur during the deployment process can be found
by viewing the deployment's progress in the Azure portal.

The steps to view the deployment’s result are as follows:

1.  Sign in to the [<u>Azure portal</u>.](https://portal.azure.com/)

2.  Go to the **Resource group** that was given as part of the input
    parameters.

3.  Select **Settings** \> **Deployments**.

4.  Select **Error details** for the deployment.

5.  The error message and error code are shown.

## Contact Microsoft support team

In case of any other issues that prevent a successful deployment, please
try to redeploy the solution once using the same app registration to
rule out an intermittent issue. If the problem continues you can contact
the Microsoft Support Team by raising a [product support
request.](#product-deployment-support)

#  Appendix

## Resources created as part of pre-deployment

This table describes the resources that are created in the
pre-deployment process, the purpose of creating the resource, and the
permissions needed by the user to create the resource.

<table>
<colgroup>
<col style="width: 24%" />
<col style="width: 38%" />
<col style="width: 37%" />
</colgroup>
<thead>
<tr>
<th><strong>Resource</strong></th>
<th><strong>Purpose</strong></th>
<th><strong>Permissions</strong></th>
</tr>
</thead>
<tbody>
<tr>
<td>Resource Group</td>
<td>The resource group acts as the container where the Azure Marketplace
Offer’s ARM template deployment takes place, and where deployment
progress and logs can be monitored.</td>
<td>Contributor role on the subscription or resource group</td>
</tr>
<tr>
<td>Managed Identity</td>
<td>This resource is created in the above resource group. This identity
connects to Azure and runs PowerShell deployment scripts as part of the
deployment inside the container.</td>
<td>Contributor role on the resource group</td>
</tr>
<tr>
<td>App Registration</td>
<td>A self-signed certificate will be uploaded to the App Registration
during the Azure Marketplace deployment to facilitate certificate-based
authentication.</td>
<td>"Users can register applications" setting in the "User settings"
section of Entra ID in the Azure portal.<br />
<br />
or,<br />
<br />
Assign the ‘Cloud Application Administrator’ role to the user.</td>
</tr>
</tbody>
</table>

## Manual pre-deployment steps

If you prefer not to use the provided pre-deployment script, follow
these steps before deploying the Purview healthcare kit.

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 53%" />
<col style="width: 27%" />
</colgroup>
<thead>
<tr>
<th><strong>Resource</strong></th>
<th><strong>Steps to create</strong></th>
<th><strong>Minimum Privilege</strong></th>
</tr>
</thead>
<tbody>
<tr>
<td>Resource group</td>
<td><ol type="1">
<li><p>Go to <a href="http://portal.azure.com">portal.azure.com</a> and
navigate to resource groups and navigate to resource groups</p></li>
<li><p>Input a new resource group name</p></li>
<li><p>Select an appropriate region</p></li>
<li><p>Click review + create</p></li>
<li><p>Click create</p></li>
</ol></td>
<td>Contributor to the existing resource group or Subscription to create
a resource group</td>
</tr>
<tr>
<td>Managed Identity</td>
<td><ol type="1">
<li><p>Go to <a href="http://portal.azure.com">portal.azure.com</a> and
navigate to Managed Identitiesportal.azure.com and navigate to Managed
Identities</p></li>
<li><p>Select the resource group you created above</p></li>
<li><p>Input region of the above created resource group</p></li>
<li><p>Insert a name for your managed identity</p></li>
<li><p>Click review + create</p></li>
</ol></td>
<td>Contributor to the existing resource group or Subscription to create
a resource group</td>
</tr>
<tr>
<td>Create app registration and add Exchange.ManageAsApp API
permissions</td>
<td><ol type="1">
<li><p>Go to <a href="http://portal.azure.com">portal.azure.com</a> and
navigate to Microsoft Entra ID</p></li>
<li><p>Go into app registrations and click new registration</p></li>
<li><p>Enter a name for your app registration and click
register</p></li>
<li><p>Navigate to API permissions</p></li>
<li><p>Click “Add a permission”</p></li>
<li><p>Click “APIs my organization uses”</p></li>
<li><p>Search for “office365 Exchange online”</p></li>
<li><p>Application permissions -&gt; Exchange -&gt; click the checkbox
and add permissions</p></li>
</ol></td>
<td style="text-align: left;">"Users can register applications" setting
in the "User settings" section of the Azure AD portal</td>
</tr>
<tr>
<td>Grant Admin Consent to the App Registration</td>
<td><ol type="1">
<li><p>Go to <a href="http://portal.azure.com">portal.azure.com</a> and
navigate to Microsoft Entra ID and App Registrations</p></li>
<li><p>Select on the app created above and navigate to API Permissions
and Click on “Grant Admin consent”</p></li>
</ol></td>
<td>Privilege Role Administrator</td>
</tr>
<tr>
<td>Add Cloud application administrator role to MI</td>
<td><ol type="1">
<li><p>Go to <a href="http://portal.azure.com">portal.azure.com</a>,
navigate to Microsoft Entra ID</p></li>
<li><p>Click roles and admins</p></li>
<li><p>Search for Cloud application administrator</p></li>
<li><p>Add assignments -&gt; select members -&gt; select the managed
identity that you created</p></li>
<li><p>Fill in required fields and click assign</p></li>
</ol></td>
<td>Privilege Role Administrator</td>
</tr>
<tr>
<td>Add Exchange Administrator and Compliance Administrator role to the
app registration</td>
<td><ol type="1">
<li><p>Go to <a href="http://portal.azure.com">portal.azure.com</a>,
navigate to Microsoft Entra ID</p></li>
<li><p>Click roles and admins</p></li>
<li><p>Search for Exchange Administrator and Compliance
administrator</p></li>
<li><p>Add assignments -&gt; select members -&gt; select the App
Registration that you created</p></li>
<li><p>Fill in required fields and click assign</p></li>
</ol></td>
<td>Privilege Role Administrator</td>
</tr>
</tbody>
</table>

##  Understanding permissions in Microsoft Purview

Please refer to the following resources on the different permissions in
Microsoft Purview:

- [Permissions in the Microsoft Purview portal \| Microsoft
  Learn](https://learn.microsoft.com/en-us/purview/purview-permissions)

- [Understand access and permissions in the classic Microsoft Purview
  governance portal \| Microsoft
  Learn](https://learn.microsoft.com/en-us/purview/classic-data-governance-permissions)

- [Permissions in the Microsoft Purview compliance portal \| Microsoft
  Learn](https://learn.microsoft.com/en-us/purview/purview-compliance-portal-permissions)

- [Roles and role groups in Microsoft Defender for Office 365 and
  Microsoft Purview - Microsoft Defender for Office 365 \| Microsoft
  Learn](https://learn.microsoft.com/en-us/defender-office-365/scc-permissions?toc=%2Fpurview%2Ftoc.json&bc=%2Fpurview%2Fbreadcrumb%2Ftoc.json)

##  Understanding common concepts in Microsoft Purview

Please refer to the following resources on the different permissions in
Microsoft Purview:

- [Scans and ingestion \| Microsoft
  Learn](https://learn.microsoft.com/en-us/purview/concept-scans-and-ingestion)

- [Understand business glossary features in the classic Microsoft
  Purview governance portal \| Microsoft
  Learn](https://learn.microsoft.com/en-us/purview/concept-business-glossary)

