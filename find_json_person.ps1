Get-ChildItem -Path 'D:\Project\Douyin_Extracted' -Recurse -Filter '*.json' | ForEach-Object {
    $c = [System.IO.File]::ReadAllText($_.FullName)
    if ($c.Contains('1c3ecP01jlPHJz4nM/FXjaf')) {
        Write-Host "FOUND: $($_.FullName)"
    }
}
