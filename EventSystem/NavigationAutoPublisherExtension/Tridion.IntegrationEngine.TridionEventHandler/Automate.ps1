#Stop-Service -Name "Tcm*" -Force

Stop-Service -Name "TcmServiceHost" -Force
Stop-Service -Name "TCMWorkflow" -Force
Stop-Service -Name "TcmSearchIndexer" -Force
Stop-Service -Name "TcmBatchProcessor" -Force
Stop-Service -Name "TcmPublisher" -Force


Stop-Service -Name "TridionTranslationManager" -Force

#iisreset /stop
iisreset
# Delete the existing files 


$filePath = "C:\sdkcode\EventSystem\NavigationAutoPublisherExtension.dll"

if (Test-Path -Path $filePath) {
    Write-Host "File exists! Safe to proceed."
	Remove-Item -Path "C:\sdkcode\EventSystem\NavigationAutoPublisherExtension.dll" -Force
	
} else {
    Write-Warning "File not found at $filePath"
}

$filePath = "C:\sdkcode\EventSystem\NavigationAutoPublisherExtension.pdb"

if (Test-Path -Path $filePath) {
    Write-Host "File exists! Safe to proceed."
	Remove-Item -Path "C:\sdkcode\EventSystem\NavigationAutoPublisherExtension.pdb" -Force
	
} else {
    Write-Warning "File not found at $filePath"
}
 
#Copy the file to destination
Copy-Item -Path "C:\sdkcode\EventSystem\NavigationAutoPublisherExtension\Tridion.IntegrationEngine.TridionEventHandler\bin\Debug\NavigationAutoPublisherExtension.pdb" -Destination "C:\sdkcode\EventSystem\" -Force

Copy-Item -Path "C:\sdkcode\EventSystem\NavigationAutoPublisherExtension\Tridion.IntegrationEngine.TridionEventHandler\bin\Debug\NavigationAutoPublisherExtension.dll" -Destination "C:\sdkcode\EventSystem\" -Force


# start the service 
#Start-Service -Name "Tcm*"
Start-Service -Name "TcmServiceHost" 
Start-Service -Name "TCMWorkflow" 
Start-Service -Name "TcmSearchIndexer" 
Start-Service -Name "TcmBatchProcessor" 
Start-Service -Name "TcmPublisher" 

#iisreset /start
