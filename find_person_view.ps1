$content = [System.IO.File]::ReadAllText('D:\Project\BustripHK\Temp\gameUI.json')
# search for "sandBoard"
$pos = $content.IndexOf("sandBoard")
Write-Host "Position of sandBoard: $pos"
if ($pos -ge 0) {
    # Let's find the surrounding 5000 characters
    $start = [Math]::Max(0, $pos - 500)
    $len = [Math]::Min(5000, $content.Length - $start)
    [System.IO.File]::WriteAllText('D:\Project\Who_Get_In_Bus\sandboard_dump.txt', $content.Substring($start, $len))
    Write-Host "Dumped to sandboard_dump.txt"
}
