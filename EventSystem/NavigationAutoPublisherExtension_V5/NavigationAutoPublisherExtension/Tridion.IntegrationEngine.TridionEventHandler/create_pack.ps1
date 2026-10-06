# Define source files and target location

#$filePath = "C:\Metrics_Development\Metrics-addon\Backend\eventsystem-extension\Tridion.IntegrationEngine.TridionEventHandler\"

$filePath = (Get-Item .).FullName		 
Write-Host "Files1 :::: $filePath"
$filePath = Split-Path -Path $filePath -Parent
Write-Host "Files2 :::: $filePath"
$filePath = Split-Path -Path $filePath -Parent
Write-Host "Files3 :::: $filePath"

$SourceFiles = @(
    "$filePath\manifest.json",
    "$filePath\bin\Debug\Tridion.IntegrationEngine.TridionEventHandler.dll"
	#"$filePath\bin\Debug\Tridion.IntegrationEngine.TridionEventHandler.pdb"
)


$addonConfigJsonFIle = "$filePath\IntegrationEngineEventHandler.json"
$DestinationFolder = "$filePath\Addon_Package\"
$ZipFileName = "IntegrationEngineEventHandler.zip"
$RenamedZipFileName = "IntegrationEngineEventHandler.zip"


 


# Ensure the destination folder exists
if (!(Test-Path -Path $DestinationFolder)) {
    New-Item -ItemType Directory -Path $DestinationFolder
}
else{
# Ensure the destination folder exists and its empty
	Remove-Item -Path $DestinationFolder\* -Recurse
}

# Copy files to the destination folder
foreach ($File in $SourceFiles) {
    if (Test-Path -Path $File) {
        Copy-Item -Path $File -Destination $DestinationFolder
    } else {
        Write-Warning "File not found: $File"
    }
}

if(Test-Path -Path $DestinationFolder+$RenamedZipFileName)
{
	 Remove-Item $DestinationFolder+$RenamedZipFileName -Force
}

# Create a zip file
$ZipFilePath = Join-Path -Path $DestinationFolder -ChildPath $ZipFileName
Compress-Archive -Path (Join-Path $DestinationFolder '*') -DestinationPath $ZipFilePath -Force


Copy-Item -Path $addonConfigJsonFIle -Destination $DestinationFolder

# Rename the zip file
#$RenamedZipFilePath = Join-Path -Path $DestinationFolder -ChildPath $RenamedZipFileName
#Rename-Item -Path $ZipFilePath -NewName $RenamedZipFileName

 

Write-Host "Files have been copied, zipped, and renamed successfully."
