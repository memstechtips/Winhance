param(
    [xml] $Document
);

# Picture files are shared by every user; only administrators and SYSTEM may change their landing folders.
$pictureRoots = @('C:\Wallpapers\Winhance', 'C:\Windows\Web\Wallpaper\Winhance');
$trustedOwners = @('S-1-5-18', 'S-1-5-32-544', 'S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464');
$pictureSecurity = [System.Security.AccessControl.DirectorySecurity]::new();
$pictureSecurity.SetSecurityDescriptorSddlForm('O:BAD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;0x1200a9;;;BU)');
$sidType = [System.Security.Principal.SecurityIdentifier];
$pictureRules = @($pictureSecurity.GetAccessRules($true, $true, $sidType));
$writeRights = [System.Security.AccessControl.FileSystemRights]'WriteData, AppendData, Delete, DeleteSubdirectoriesAndFiles, ChangePermissions, TakeOwnership';

function Get-PictureAttributes([string] $Path) {
    try { return [IO.File]::GetAttributes($Path); }
    catch [IO.FileNotFoundException] { return $null; }
    catch [IO.DirectoryNotFoundException] { return $null; }
}

function Test-ProtectedPictureAcl($Acl, [bool] $IsParent) {
    $descriptor = [System.Security.AccessControl.RawSecurityDescriptor]::new($Acl.GetSecurityDescriptorBinaryForm(), 0);
    if ($null -eq $descriptor.DiscretionaryAcl -or $Acl.GetOwner($sidType).Value -notin $trustedOwners) { return $false; }
    $rights = $writeRights;
    if ($IsParent) { $rights = $rights -band (-bnot [System.Security.AccessControl.FileSystemRights]::AppendData); }
    $usersRead = $false;
    foreach ($rule in $Acl.GetAccessRules($true, $true, $sidType)) {
        if (-not $IsParent -and $rule.AccessControlType -eq 'Deny') { return $false; }
        if ($rule.AccessControlType -eq 'Allow' -and -not ($rule.PropagationFlags -band [System.Security.AccessControl.PropagationFlags]::InheritOnly)) {
            if (($rule.FileSystemRights -band $rights) -and $rule.IdentityReference.Value -notin $trustedOwners) { return $false; }
            if ($rule.IdentityReference.Value -eq 'S-1-5-32-545' -and
                ($rule.FileSystemRights -band [System.Security.AccessControl.FileSystemRights]::ReadAndExecute) -eq [System.Security.AccessControl.FileSystemRights]::ReadAndExecute) { $usersRead = $true; }
        }
    }
    return ($IsParent -or $usersRead);
}

function Test-OwnedPictureDirectory($Acl) {
    if (-not $Acl.AreAccessRulesProtected -or $Acl.GetOwner($sidType).Value -ne 'S-1-5-32-544') { return $false; }
    $rules = @($Acl.GetAccessRules($true, $true, $sidType));
    if ($rules.Count -ne $pictureRules.Count) { return $false; }
    foreach ($want in $pictureRules) {
        if (-not ($rules | Where-Object { -not $_.IsInherited -and $_.IdentityReference -eq $want.IdentityReference -and
            $_.AccessControlType -eq $want.AccessControlType -and $_.InheritanceFlags -eq $want.InheritanceFlags -and
            $_.PropagationFlags -eq $want.PropagationFlags -and $_.FileSystemRights -eq $want.FileSystemRights })) { return $false; }
    }
    return $true;
}

function Test-PictureLanding([string] $Path) {
    $fullPath = [IO.Path]::GetFullPath($Path);
    $root = $pictureRoots | Where-Object { $fullPath.StartsWith($_ + '\', [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1;
    if (-not $root -or $Path -ne $fullPath) { return $false; }
    $parts = $fullPath.Substring($root.Length + 1).Split('\');
    if ($parts.Count -gt 2 -or [IO.Path]::GetExtension($fullPath) -notin @('.jpg','.jpeg','.jpe','.jfif','.png','.bmp','.dib','.gif','.tif','.tiff','.wdp','.jxr','.webp','.heic','.heif','.hif','.avif')) { return $false; }
    foreach ($part in $parts) {
        if (-not $part -or $part.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or $part -match '[. ]$' -or $part -match '^(CON|PRN|AUX|NUL|COM[1-9\u00B9\u00B2\u00B3]|LPT[1-9\u00B9\u00B2\u00B3])(\.|$)') { return $false; }
    }
    $parent = [IO.DirectoryInfo]::new($root).Parent;
    $firstParent = $parent;
    if ($root -eq $pictureRoots[0]) { $firstParent = $parent.Parent; }
    # Check ancestors from the drive down before inspecting or creating any owned directory.
    $ancestors = @();
    for ($dir = $firstParent; $null -ne $dir; $dir = $dir.Parent) { $ancestors = @($dir) + $ancestors; }
    foreach ($dir in $ancestors) {
        $attributes = Get-PictureAttributes $dir.FullName;
        if ($null -eq $attributes -or ($attributes -band [IO.FileAttributes]::ReparsePoint) -or -not ($attributes -band [IO.FileAttributes]::Directory) -or
            -not (Test-ProtectedPictureAcl ($dir.GetAccessControl()) $true)) { return $false; }
    }
    $owned = @([IO.DirectoryInfo]::new($root));
    if ($root -eq $pictureRoots[0]) { $owned = @($parent) + $owned; }
    if ($parts.Count -eq 2) { $owned += [IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName($fullPath)); }
    foreach ($dir in $owned) {
        $attributes = Get-PictureAttributes $dir.FullName;
        if ($null -eq $attributes) {
            $dir.Create($pictureSecurity);
            $attributes = Get-PictureAttributes $dir.FullName;
        }
        if ($null -eq $attributes -or ($attributes -band [IO.FileAttributes]::ReparsePoint) -or -not ($attributes -band [IO.FileAttributes]::Directory) -or
            -not (Test-OwnedPictureDirectory ($dir.GetAccessControl())) -or
            ((Get-PictureAttributes $dir.FullName) -band [IO.FileAttributes]::ReparsePoint)) { return $false; }
    }
    $attributes = Get-PictureAttributes $fullPath;
    return ($null -eq $attributes -or (-not ($attributes -band ([IO.FileAttributes]::ReparsePoint -bor [IO.FileAttributes]::Directory)) -and
        (Test-ProtectedPictureAcl ([IO.FileInfo]::new($fullPath).GetAccessControl()) $false) -and
        -not ((Get-PictureAttributes $fullPath) -band ([IO.FileAttributes]::ReparsePoint -bor [IO.FileAttributes]::Directory))));
}

foreach( $file in $Document.unattend.Extensions.File ) {
    $path = [System.Environment]::ExpandEnvironmentVariables(
        $file.GetAttribute( 'path' )
    );
    if( $file.GetAttribute( 'encoding' ) -eq 'base64' ) {
        try {
            if (-not (Test-PictureLanding $path)) { throw 'The wallpaper destination is not protected.'; }
            [System.IO.File]::WriteAllBytes( $path, [System.Convert]::FromBase64String( $file.InnerText.Trim() ) );
        } catch { Write-Warning "Skipped carried wallpaper: $($_.Exception.Message)"; }
        continue;
    }
    $fullPath = [IO.Path]::GetFullPath($path);
    if ($pictureRoots | Where-Object { $fullPath -eq $_ -or $fullPath.StartsWith($_ + '\', [StringComparison]::OrdinalIgnoreCase) }) {
        Write-Warning 'Skipped text entry in the shared wallpaper folder.';
        continue;
    }
    mkdir -Path ( [System.IO.Path]::GetDirectoryName( $path ) ) -ErrorAction 'SilentlyContinue';
    $encoding = switch( [System.IO.Path]::GetExtension( $path ) ) {
        { $_ -in '.ps1', '.xml' } { [System.Text.Encoding]::UTF8; }
        { $_ -in '.reg', '.vbs', '.js' } { [System.Text.UnicodeEncoding]::new( $false, $true ); }
        default { [System.Text.Encoding]::Default; }
    };
    $text = $file.InnerText.Trim() -replace '\r?\n', "`r`n";
    [System.IO.File]::WriteAllBytes( $path, ( $encoding.GetPreamble() + $encoding.GetBytes( $text ) ) );
}
