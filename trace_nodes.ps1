$json = Get-Content 'D:\Project\BustripHK\Temp\gameUI.json' -Raw | ConvertFrom-Json
$nodes = $json[5][7][0]
Write-Host "Total elements in $json[5][7][0]: $($nodes.Count)"
Write-Host "Element 8:"
Write-Host ($nodes[8] | ConvertTo-Json -Depth 6)
Write-Host "Element 21:"
Write-Host ($nodes[21] | ConvertTo-Json -Depth 6)
