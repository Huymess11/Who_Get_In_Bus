$content = [System.IO.File]::ReadAllText('D:\Project\BustripHK\Temp\gameUI.json')
$firstClose = $content.IndexOf('],')
$secondClose = $content.IndexOf('],', $firstClose + 2)
Write-Host "Second array:"
Write-Host $content.Substring($firstClose + 2, [Math]::Min(1500, $secondClose - $firstClose))
