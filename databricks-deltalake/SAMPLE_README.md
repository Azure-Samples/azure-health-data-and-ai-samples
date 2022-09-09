# FHIR Service Analytics with Azure Databricks Delta Lake Analytics

## Overview

Data Lakehouse is an open data architecture that combines existing features from traditional data lakes and data warehouses. Delta Lake has emerged as the leading storage framework that enables building a Lakehouse architecture on top of existing data lake technologies. Azure Health Data Services enables Lakehouse architectures by exporting parquet files of FHIR data which align to the open [SQL on FHIR](https://github.com/FHIR/sql-on-fhir/blob/master/sql-on-fhir.md) standard.

Building a Lakehouse for FHIR data has these advantages:

- Combining your FHIR data with other datasets.
- Having a consistent location of enterprise ready data enabling more self-service across your organization.
- Metadata management and versioning of data simplifing data that is often updated.

## Scenario Overview

For this sample, we will be building a simple Lakehouse exploring a single use case: a hospital admission report. Here, we've been asked to provide some simple data to enable trend analysis for hospital admissions. We'll only focus on Patient, Encounter, and Observation information, but the same approach can be expanded for other entities.

## Setup the sample

- deploy infrastructure
- setup azure databricks

## Looking around at what we deployed

## Load data to test the sample

- Fhir loader w/ Synthea?
- Showing graph on databricks?

## Watch data flow through the sample

## Hook up PowerBI

## Explain the sample

## CTA

- Do this in your own env

## Moving this to production

- https://github.com/Azure/AzureDatabricksBestPractices/blob/master/toc.md
- https://github.com/Azure-Samples/modern-data-warehouse-dataops/tree/main/single_tech_samples/databricks/sample2_enterprise_azure_databricks_environment