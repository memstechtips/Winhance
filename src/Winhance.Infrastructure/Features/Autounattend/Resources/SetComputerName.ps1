# Renames this PC to {{prefix}} plus a dash and four characters from its BIOS serial, else its first physical network
# card, else random hex. Setup writes TEMPNAME late in specialize, so this rewrites the name until the restart ends it.
$placeholders = {{placeholder-serials}}
$serial = ((Get-CimInstance Win32_BIOS).SerialNumber -replace '[^A-Za-z0-9]').ToUpper()
if ($serial.Length -ge 4 -and $serial -notmatch '^(.)\1*$' -and $serial -notin $placeholders) {
    $ending = $serial.Substring($serial.Length - 4)
} else {
    $card = Get-CimInstance Win32_NetworkAdapter -Filter 'PhysicalAdapter = TRUE AND MACAddress IS NOT NULL' | Sort-Object Index | Select-Object -First 1
    $mac = ($card.MACAddress -replace '[^0-9A-Fa-f]').ToUpper()
    $ending = if ($mac.Length -ge 4) { $mac.Substring($mac.Length - 4) } else { '{0:X4}' -f (Get-Random -Maximum 65536) }
}
$name = '{{prefix}}-' + $ending
while ($true) {
    Set-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\ComputerName\ComputerName' -Name ComputerName -Value $name.ToUpper()
    Set-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters' -Name Hostname -Value $name
    Set-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters' -Name 'NV Hostname' -Value $name
    Start-Sleep -Milliseconds 50
}
