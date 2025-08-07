param ($Configuration, $TargetName, $ProjectDir, $TargetPath, $TargetDir)
write-host $Configuration
write-host $TargetName
write-host $ProjectDir
write-host $TargetPath
write-host $TargetDir

# sign the dll
$thumbPrint = "e729567d4e9be8ffca04179e3375b7669bccf272"
$cert=Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert | Where { $_.Thumbprint -eq $thumbPrint}

Set-AuthenticodeSignature -FilePath $TargetPath -Certificate $cert -IncludeChain All -TimestampServer "http://timestamp.comodoca.com/authenticode"

function CopyToFolder($revitVersion, $addinFolder) {

    # Skip copying if we're already building to the target folder
    if ($TargetDir -eq $addinFolder) {
        Write-Host "Skipping copy - already building to target folder: $addinFolder"
        return
    }
    
    if (Test-Path $addinFolder) {
        try {
            # Remove previous versions
            Get-ChildItem -Path $addinFolder | Remove-Item -Recurse
            
            # Copy the addin file
            Write-Host "copy all files" + ($TargetDir) + $addinFolder
            xcopy ($TargetDir) ($addinFolder) /s /e /y
        }
        catch {
            Write-Host "An error occurred:"
            Write-Host $_
        }
    }
}


$revitVersion = $Configuration.replace('Debug','').replace('Release','')

# Copy to Addin folder for debug
$addinMainFolder = ($env:APPDATA + "\Autodesk\ApplicationPlugins\GroupClashes.BM42.bundle\")
$addinFolder = ($addinMainFolder + "Contents\" + $revitVersion + "\")

# Only copy package contents if we're not already building to the target
if ($TargetDir -ne $addinFolder) {
    xcopy /Y ($ProjectDir + "PackageContents.xml") $addinMainFolder
    Write-Host "addin folder" + $addinFolder
    CopyToFolder $revitVersion $addinFolder
} else {
    Write-Host "Skipping addin copy - already building to target folder: $addinFolder"
    # Still copy PackageContents.xml to the main bundle folder
    xcopy /Y ($ProjectDir + "PackageContents.xml") $addinMainFolder
}


# Copy to release folder for building the package
$ReleasePath="G:\My Drive\05 - Travail\Revit Dev\GroupClashes\Releases\Current Release\GroupClashes.BM42.bundle\"
xcopy /Y ($ProjectDir + "PackageContents.xml") $ReleasePath
$releaseFolder = ($ReleasePath + "Contents\" + $revitVersion + "\")
Write-Host "release folder" + $releaseFolder
CopyToFolder $revitVersion $releaseFolder


## Zip the package

$BundleFolder = (get-item $ReleasePath ).parent.FullName

$ReleaseZip = ($BundleFolder + "\" + $TargetName + ".zip")
if (Test-Path $ReleaseZip) { Remove-Item $ReleaseZip }

if ( Test-Path -Path $ReleasePath ) {
  7z a -tzip $ReleaseZip ($BundleFolder + "\GroupClashes.BM42.bundle\")
}