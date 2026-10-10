$json = Get-Content 'D:\Project\BustripHK\Temp\gameUI.json' -Raw | ConvertFrom-Json
$gameUI = $json[5][7]
# $gameUI is an array of node/component definitions
Write-Host "Length of gameUI: $($gameUI.Count)"
for ($i=0; $i -lt $gameUI.Count; $i++) {
    $elem = $gameUI[$i]
    $str = ($elem | ConvertTo-Json -Compress)
    if ($str -match 'target|sand|person|road|bottom|park') {
        Write-Host "Index $i : $str"
    }
}
