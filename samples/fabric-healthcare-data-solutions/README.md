# Microsoft Fabric Healthcare Data Solutions Git Integration Helper

## Overview 

Healthcare data solutions in Microsoft Fabric help you accelerate time to value by addressing the critical need to efficiently transform healthcare data into a suitable format for analysis. With these solutions, you can conduct exploratory analysis, run large-scale analytics, and power generative AI with your healthcare data. More information about Healthcare data solutions on Fabric can be found [here](https://learn.microsoft.com/en-us/industry/healthcare/healthcare-data-solutions/overview).

This sample provides a notebook for customers who have already deployed their Healthcare data solutions item in Fabric and want to take advantage of Application Lifecycle Management (ALM). ALM allows developers who are developing in Fabric to apply the capabilities of familiar source control tools to manage Fabric items.

There are some additional, manual steps to be able to run Helathcare data solutions capabilities after syncing a workspace with ALM. The `git_integration_helper` attempts to streamline these manual steps. 

## Prerequisites

For this sample, it is assumed that you have already deployed an instance of Healthcare data solutions and one or more capabilities following [these steps](https://learn.microsoft.com/en-us/industry/healthcare/healthcare-data-solutions/deploy?toc=%2Findustry%2Fhealthcare%2Ftoc.json&bc=%2Findustry%2Fbreadcrumb%2Ftoc.json). It is also assumed that you have connected your Workspace to Git and commited those changes to a branch following [these steps](https://learn.microsoft.com/en-us/fabric/cicd/git-integration/git-get-started?tabs=azure-devops%2CAzure%2Ccommit-to-git). Now, you want to create a new workspace and synchronize your Healthcare data solutions item. After creating the workspace, connecting to the branch (following the same steps detailed above), and waiting for your Fabric items to sync (see branch steps above), you are ready to run the sample notebook.

## What does the sample notebook do?

The `git_integration_helper` notebook helps copy important system configuration files, creates folders and tables, and updates metadata of other Fabric items in your workspace. This saves an immense amount of time and allows you to quickly start managing your healthcare data.

## How do I get started?

If you meet the criteria defined in the scenario overview above, all you need to do to run the `git_integration_helper` notebook is import it into the **_source_** Fabric workspace, open the notebook, and run through the cells.