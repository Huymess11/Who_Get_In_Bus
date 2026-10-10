$s = [System.IO.File]::ReadAllText('D:\Project\BustripHK\Temp\gameUI.json')
$idx = $s.IndexOf("sandBoard")
if ($idx -ge 0) {
    $start = [Math]::Max(0, $idx - 500)
    $len = [Math]::Min(3000, $s.Length - $start)
    Write-Host $s.Substring($start, $len)
} else {
    Write-Host "Not found sandBoard"
}
