# Set fhirUser Claim Mapping

> [!TIP]
> If you encounter any issues during configuration, deployment, or testing, please refer to the [Troubleshooting Guide](../troubleshooting.md).

The `fhirUser` claim mapping informs the SMART client of the user's FHIR identity through the ID token, and informs the FHIR Service of the user's FHIR identity through the access token. The FHIR Service uses this claim to enforce SMART scope evaluation and compartment-based access control.

This claim mapping needs to be set up on the **FHIR Resource App** so that the directory extension (`extension_<appId>_fhirUser`) is mapped to a top-level claim called `fhirUser`.

---

## A. Configure the `fhirUser` mapping on the Enterprise Application

1. In the Azure Portal, open **Microsoft Entra ID → Enterprise applications**.
2. Search for the app you created in previous step (or click **Managed application in local directory** from the App Registration to jump there).
3. Select **Single sign-on** in the left menu.
4. Open the **Attributes & Claims** section and click **Edit**.
5. Click **Add new claim**.
6. Enter the following:
   - **Name**: `fhirUser`
   - **Source**: *Directory schema extension*
   - **Source attribute**: select your **FHIR Resource Application** in the application selector, then choose the `user.fhirUser` directory extension you created.
7. Click **Add**, then **Save**.

After this is applied, any access token issued for the FHIR Resource App (via `aud`=`FhirAudience`) will include a `fhirUser` claim populated from the user's directory extension value.

---

## B. Allow the application to use mapped claims

For the FHIR Resource App to honor the mapping, its manifest must allow mapped claims:

1. Open the **App Registration** for the FHIR Resource App.
2. Select **Manifest** in the left menu.
3. Locate the `acceptMappedClaims` property and set it to `true`. If it is already `true`, no change is needed.
4. Click **Save**.

---

## Outcome

When you finish:

- Each user's `fhirUser` directory extension value is emitted as a top-level `fhirUser` claim in access tokens issued for the FHIR Resource App.
- The FHIR Service can perform SMART scope evaluation against the mapped claim.

You can verify by acquiring an access token for one of your test users and decoding it at [jwt.ms](https://jwt.ms/) — the token should contain a `fhirUser` claim with the FHIR URL you configured (e.g. `https://<FhirUrl>/Patient/PatientA`).

[Back to FHIR Resource App Registration](./fhir-resource-app-registration.md) · [Back to Microsoft Entra ID Deployment](../deployment-entra.md)
