# # Download and expand dcmtk
# mkdir 'C:\dcmtk\'
# $source = 'https://dicom.offis.de/download/dcmtk/dcmtk367/bin/dcmtk-3.6.7-win64-dynamic.zip'
# $destination = 'C:\dcmtk\dcmtk.zip'
# Invoke-RestMethod -Uri $source -OutFile $destination

# Expand-Archive -Path 'C:\dcmtk\dcmtk.zip' -DestinationPath 'C:\dcmtk\'


mkdir 'C:\gdcm\'
$source = 'https://github.com/malaterre/GDCM/releases/download/v3.0.20/GDCM-3.0.20-Windows-x86_64.zip?raw=true'
$destination = 'C:\gdcm\gdcm.zip'
Invoke-RestMethod -Uri $source -OutFile $destination

Expand-Archive -Path 'C:\gdcm\gdcm.zip' -DestinationPath 'C:\gdcm\'


# mkdir 'C:\dicoms\'
# $source = 'https://github.com/StevenBorg/ahds_demo_config/blob/main/dicoms.zip?raw=true'
# $destination = 'C:\dicoms\dicoms.zip'
# Invoke-RestMethod -Uri $source -OutFile $destination

# Expand-Archive -Path 'C:\dicoms\dicoms.zip' -DestinationPath 'C:\dicoms\'

mkdir 'C:\dicoms\'
$source = 'https://github.com/StevenBorg/ahds_demo_config/blob/main/new.zip?raw=true'
$destination = 'C:\dicoms\new.zip'
Invoke-RestMethod -Uri $source -OutFile $destination

Expand-Archive -Path 'C:\dicoms\new.zip' -DestinationPath 'C:\dicoms'

#mkdir 'C:\newdicoms\'
$source = 'https://github.com/StevenBorg/ahds_demo_config/blob/main/recent.zip?raw=true'
$destination = 'C:\dicoms\recent.zip'
Invoke-RestMethod -Uri $source -OutFile $destination

Expand-Archive -Path 'C:\dicoms\recent.zip' -DestinationPath 'C:\dicoms'

# Download convenience files for students
#mkdir 'C:\downloads'
$source = 'https://raw.githubusercontent.com/StevenBorg/ahds_dicom_service_demos/main/uploads/Orthanc.url'
$destination = 'C:\Users\Default\Desktop\Orthanc.url'
Invoke-RestMethod -Uri $source -OutFile $destination

$source = 'https://raw.githubusercontent.com/StevenBorg/ahds_dicom_service_demos/main/uploads/Qvera%20Interface%20Engine.url'
$destination = 'C:\Users\Default\Desktop\QveraInterfaceEngine.url'
Invoke-RestMethod -Uri $source -OutFile $destination

# $source = 'https://github.com/StevenBorg/ahds_dicom_service_demos/blob/main/uploads/qie_MicrosoftDICOM.qie?raw=true'
# $destination = 'C:\downloads\qie_MicrosoftDICOM.qie'
# Invoke-RestMethod -Uri $source -OutFile $destination

# $source = 'https://www.dicomlibrary.com?study=1.2.826.0.1.3680043.8.1055.1.20111102150758591.92402465.76095170'
# $destination = 'C:\Users\Default\Desktop\sample.dcm'
# Invoke-RestMethod -Uri $source -OutFile $destination

$source = 'https://github.com/StevenBorg/ahds_dicom_service_demos/blob/main/uploads/qie_MicrosoftDICOM_20221121.qie?raw=true'
$destination = 'C:\Users\Default\Desktop\qie_MicrosoftDICOM_20221121.qie'
Invoke-RestMethod -Uri $source -OutFile $destination

$source = 'https://github.com/StevenBorg/ahds_dicom_service_demos/blob/main/uploads/1-preload-orthanc.cmd?raw=true'
$destination = 'C:\Users\Default\Desktop\1-preload-orthanc.cmd'
Invoke-RestMethod -Uri $source -OutFile $destination

$source = 'https://github.com/StevenBorg/ahds_dicom_service_demos/blob/main/uploads/2-modality-pushing-to-orthanc.cmd?raw=true'
$destination = 'C:\Users\Default\Desktop\2-modality-pushing-to-orthanc.cmd'
Invoke-RestMethod -Uri $source -OutFile $destination

$source = 'https://github.com/StevenBorg/ahds_dicom_service_demos/blob/main/uploads/3-modality-pushing-to-qie.cmd?raw=true'
$destination = 'C:\Users\Default\Desktop\3-modality-pushing-to-qie.cmd'
Invoke-RestMethod -Uri $source -OutFile $destination

$source = 'https://github.com/StevenBorg/ahds_dicom_service_demos/blob/main/uploads/4-modality-pushing-to-qie-extra.cmd?raw=true'
$destination = 'C:\Users\Default\Desktop\4-modality-pushing-to-qie-extra.cmd'
Invoke-RestMethod -Uri $source -OutFile $destination