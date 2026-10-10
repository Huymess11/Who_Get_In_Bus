$content = [System.IO.File]::ReadAllText('D:\Project\BustripHK\Temp\gameUI.json')
# search for occurrences of targetController, sandBoard, or PersonView
$matches = [System.Text.RegularExpressions.Regex]::Matches($content, '\{[^{}]*"_name"\s*:\s*"[^"]*"[^{}]*\}')
foreach ($m in $matches) {
    if ($m.Value -match 'target|sand|person|board|road|people') {
        Write-Host $m.Value
    }
}
