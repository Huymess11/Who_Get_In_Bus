$json = Get-Content 'D:\Project\BustripHK\Temp\gameUI.json' -Raw | ConvertFrom-Json

for ($blockIdx = 0; $blockIdx -lt $json.Count; $blockIdx++) {
    $block = $json[$blockIdx]
    if ($block -is [Array]) {
        for ($i = 0; $i -lt $block.Count; $i++) {
            $item = $block[$i]
            if ($item -is [PSCustomObject] -and $item._name) {
                if ($item._name -match 'target|sand|person|road|bottom') {
                    Write-Host "Block $blockIdx, Item $i : Node Name = $($item._name)"
                    Write-Host ($item | ConvertTo-Json -Depth 4)
                }
            }
        }
    }
}
